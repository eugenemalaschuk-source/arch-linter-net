import { RelayDurableObject } from "./relay-do";
import { REGISTRY_OBJECT_NAME, RelayRegistryDurableObject } from "./registry-do";
import { validateRegistryEntry, isOpaqueAlias } from "./registry";
import { FIXED_GITHUB_ISSUER, FIXED_GITHUB_JWKS, MAX_REQUEST_BYTES, type RegistryEntry, type RelayEnvironment } from "./types";

export { RelayDurableObject };
export { RelayRegistryDurableObject, REGISTRY_OBJECT_NAME };
export * from "./types";
export * from "./registry";
export * from "./security";
export * from "./payload";

function entryForHeader(entry: RegistryEntry): string {
  return JSON.stringify(entry);
}

function json(status: number, body: unknown, headers: Record<string, string> = { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" }): Response {
  return new Response(JSON.stringify(body), { status, headers });
}

function unknownRoute(): Response { return json(404, { error: "unknown_route" }); }

function registryStub(env: RelayEnvironment): DurableObjectStub {
  if (!env.REGISTRY || typeof env.REGISTRY.idFromName !== "function") throw new Error("registry unavailable");
  return env.REGISTRY.get(env.REGISTRY.idFromName(REGISTRY_OBJECT_NAME));
}

async function registryCall(env: RelayEnvironment, operation: string, body: unknown): Promise<{ status: number; body: Record<string, unknown> }> {
  const stub = registryStub(env);
  const response = await (stub.fetch as unknown as (input: unknown) => Promise<Response>)(new Request(`https://relay.test/internal-registry/${operation}`, {
    method: "POST",
    headers: { "content-type": "application/json", "x-relay-internal": "1" },
    body: JSON.stringify(body)
  }));
  let parsed: Record<string, unknown> = {};
  try { parsed = await response.json() as Record<string, unknown>; } catch { /* generic response below */ }
  return { status: response.status, body: parsed };
}

async function adminRegister(request: Request, env: RelayEnvironment): Promise<Response> {
  const expected = env.ADMIN_TOKEN;
  const supplied = request.headers.get("authorization");
  if (!expected || supplied !== `Bearer ${expected}`) return json(401, { error: "unauthorized" });
  let body: unknown;
  try {
    const contentLength = request.headers.get("content-length");
    if (contentLength && (!/^\d+$/u.test(contentLength) || Number(contentLength) > MAX_REQUEST_BYTES)) return json(413, { error: "request_too_large" });
    const bytes = await request.arrayBuffer();
    if (bytes.byteLength > MAX_REQUEST_BYTES) return json(413, { error: "request_too_large" });
    body = JSON.parse(new TextDecoder().decode(bytes));
  } catch { return json(413, { error: "request_too_large" }); }
  const candidate = body && typeof body === "object" && !Array.isArray(body) && "registry_entry" in body
    ? (body as { registry_entry: unknown }).registry_entry
    : body;
  if (!validateRegistryEntry(candidate)) return json(413, { error: "invalid_registration" });
  const registeredEntry = candidate as RegistryEntry;
  if (typeof registeredEntry.audience !== "string" || registeredEntry.audience.length === 0 || registeredEntry.consent !== true) return json(413, { error: "invalid_registration" });
  const result = await registryCall(env, "register", { entry: registeredEntry });
  if (result.status !== 201) return json(result.status === 409 ? 409 : 503, { error: result.status === 409 ? "registration_conflict" : "storage_unavailable" });
  return json(201, { ok: true, registered: true });
}

async function adminRevoke(request: Request, env: RelayEnvironment): Promise<Response> {
  const expected = env.ADMIN_TOKEN;
  if (!expected || request.headers.get("authorization") !== `Bearer ${expected}`) return json(401, { error: "unauthorized" });
  const alias = new URL(request.url).pathname.split("/").filter(Boolean).at(-1) ?? "";
  if (!isOpaqueAlias(alias)) return unknownRoute();
  const result = await registryCall(env, "revoke", { alias });
  if (result.status !== 200) return result.status === 404 ? unknownRoute() : json(503, { error: "storage_unavailable" });
  return json(200, { ok: true });
}

function registryEntryFromConfig(env: RelayEnvironment, entry: RegistryEntry): RegistryEntry {
  // reference-config.json carries OIDC trust beside registry_entry. Keep that
  // trust private and flatten only the fixed audience into the internal pin.
  let config: unknown = env.RELAY_REGISTRY ?? env.REGISTRY;
  if (typeof config === "string") { try { config = JSON.parse(config); } catch { return entry; } }
  if (config && typeof config === "object" && !Array.isArray(config)) {
    const trust = (config as Record<string, unknown>).oidc_trust;
    if (trust && typeof trust === "object" && typeof (trust as Record<string, unknown>).audience === "string") return { ...entry, audience: (trust as Record<string, unknown>).audience as string };
  }
  return entry;
}

export default {
  async fetch(request: Request, env: RelayEnvironment): Promise<Response> {
    try {
      const url = new URL(request.url);
      const parts = url.pathname.split("/").filter(Boolean);
      // Admin mutations operate only on the private registry; they never use
      // idFromName and therefore cannot allocate an alias object accidentally.
      if (parts[0] === "badge-relay" && parts[1] === "v1" && parts[2] === "admin") {
        if (parts[3] === "register" && request.method === "POST") return await adminRegister(request, env);
        if (parts[3] === "revoke" && request.method === "POST") return await adminRevoke(request, env);
        return unknownRoute();
      }
      // Public GET/HEAD and a bare alias are intentionally outside this
      // change. The later read/render child owns that route; rejecting here
      // also prevents a public alias from allocating a Durable Object.
      if (parts.length < 4 || parts[0] !== "badge-relay" || parts[1] !== "v1" || !isOpaqueAlias(parts[2])) return unknownRoute();
      const lookup = await registryCall(env, "lookup", { alias: parts[2] });
      const configured = lookup.status === 200 && lookup.body.entry && typeof lookup.body.entry === "object" ? lookup.body.entry as RegistryEntry : undefined;
      if (!configured) return unknownRoute();
      // This lookup deliberately precedes idFromName. Unknown public aliases
      // are indistinguishable 404s and cannot allocate Durable Objects.
      if (!env.RELAY || typeof env.RELAY.idFromName !== "function") return json(503, { error: "storage_unavailable" });
      const entry = registryEntryFromConfig(env, configured);
      const id = env.RELAY.idFromName(parts[2]);
      const stub = env.RELAY.get(id);
      if (request.method !== "POST") return unknownRoute();
      const operation = parts[3];
      const target = new URL(request.url);
      target.pathname = `/internal/${parts[2]}/${operation}`;
      const headers = new Headers(request.headers);
      headers.set("x-relay-registry", entryForHeader(entry));
      return (stub.fetch as unknown as (input: unknown) => Promise<Response>)(new Request(target, { method: request.method, headers, body: request.body }));
    } catch {
      return json(503, { error: "storage_unavailable" });
    }
  }
};

// Explicit constants are useful to deployment checks and make accidental
// token-derived trust configuration difficult to introduce.
export const OIDC_TRUST = Object.freeze({ issuer: FIXED_GITHUB_ISSUER, jwks_uri: FIXED_GITHUB_JWKS, algorithms: ["RS256"] as const });
