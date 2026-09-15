import { REGISTRY_OBJECT_NAME } from "./registry-do";
import { validateRegistryEntry, isOpaqueAlias } from "./registry";
import { FIXED_GITHUB_ISSUER, FIXED_GITHUB_JWKS, MAX_REQUEST_BYTES, type RegistryEntry, type RelayEnvironment } from "./types";
import { publicUnavailableResponse, type PublicRepresentation } from "./read";

export { RelayDurableObject } from "./relay-do";
export { RelayRegistryDurableObject } from "./registry-do";
export { REGISTRY_OBJECT_NAME };
export * from "./types";
export * from "./registry";
export * from "./security";
export * from "./payload";
export * from "./lifecycle";

function entryForHeader(entry: RegistryEntry): string {
  return JSON.stringify(entry);
}

const JSON_RESPONSE_HEADERS: Record<string, string> = { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" };

function json(status: number, body: unknown, headers: Record<string, string> = JSON_RESPONSE_HEADERS): Response {
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

async function readAdminBody(request: Request): Promise<Record<string, unknown> | undefined> {
  try {
    const contentLength = request.headers.get("content-length");
    if (contentLength && (!/^\d+$/u.test(contentLength) || Number(contentLength) > MAX_REQUEST_BYTES)) return undefined;
    const bytes = await request.arrayBuffer();
    if (bytes.byteLength > MAX_REQUEST_BYTES) return undefined;
    const value: unknown = JSON.parse(new TextDecoder().decode(bytes));
    return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
  } catch { return undefined; }
}

function adminAuthorized(request: Request, env: RelayEnvironment): boolean {
  return typeof env.ADMIN_TOKEN === "string" && env.ADMIN_TOKEN.length > 0 && request.headers.get("authorization") === `Bearer ${env.ADMIN_TOKEN}`;
}

function operationId(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 && value.length <= 128 ? value : undefined;
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

async function relayAdminCall(env: RelayEnvironment, alias: string, binding: RegistryLookup, operation: string, body: Record<string, unknown> = {}): Promise<{ status: number; body: Record<string, unknown> }> {
  if (!env.RELAY || typeof env.RELAY.idFromName !== "function" || !binding.entry) return { status: 503, body: { error: "storage_unavailable" } };
  const target = new URL(`https://relay.test/internal/admin/${alias}/${operation}`);
  const headers = new Headers({ "content-type": "application/json", "x-relay-registry": entryForHeader(binding.entry), "x-relay-registry-revision": String(binding.revision), "x-relay-barrier-epoch": String(binding.barrierEpoch), "x-relay-registry-tombstoned": String(binding.tombstoned), "x-relay-admin": "1" });
  const stub = env.RELAY.get(env.RELAY.idFromName(alias));
  const boundedBody = {
    ...body,
    operation_id: operationId(body.operation_id) ?? (operation === "status" || operation === "sync" ? `relay-${operation}-${binding.revision}-${binding.barrierEpoch}` : undefined),
    expected_registry_revision: Object.hasOwn(body, "expected_registry_revision") ? body.expected_registry_revision : binding.revision,
    expected_barrier_epoch: Object.hasOwn(body, "expected_barrier_epoch") ? body.expected_barrier_epoch : binding.barrierEpoch
  };
  const response = await (stub.fetch as unknown as (input: unknown) => Promise<Response>)(new Request(target, { method: "POST", headers, body: JSON.stringify(boundedBody) }));
  let parsed: Record<string, unknown> = {};
  try { parsed = await response.json() as Record<string, unknown>; } catch { /* generic response below */ }
  return { status: response.status, body: parsed };
}

function expectedRegistryStateMatches(body: Record<string, unknown>, binding: RegistryLookup): boolean {
  if (Object.hasOwn(body, "expected_registry_revision")
    && (!Number.isSafeInteger(body.expected_registry_revision) || body.expected_registry_revision !== binding.revision)) return false;
  if (Object.hasOwn(body, "expected_barrier_epoch")
    && (!Number.isSafeInteger(body.expected_barrier_epoch) || body.expected_barrier_epoch !== binding.barrierEpoch)) return false;
  return true;
}

function requireMutationOperationId(body: Record<string, unknown>): Response | undefined {
  return operationId(body.operation_id) ? undefined : json(413, { error: "invalid_operation_id" });
}

async function adminStatus(request: Request, env: RelayEnvironment, alias: string): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  if (!isOpaqueAlias(alias)) return unknownRoute();
  const lookup = await lookupEntry(env, alias, true);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  const result = await relayAdminCall(env, alias, lookup, "status");
  return json(result.status, result.body);
}

async function performRevokeCommit(env: RelayEnvironment, alias: string, lookup: RegistryLookup, body: Record<string, unknown>, operation: "revoke" | "uninstall" | "transfer", lifecycleOperation: "revoke" | "uninstall" | "transfer"): Promise<Response> {
  // Reserve the Relay state before the Registry CAS.  This is a two-phase
  // cross-object transition: the reservation atomically fences publishers
  // and clears the public payload, so a generation/epoch change cannot sneak
  // in after the old status preflight but before Registry tombstoning.
  const prepared = await relayAdminCall(env, alias, lookup, "revoke-prepare", { ...body, lifecycle_operation: lifecycleOperation });
  if (prepared.status !== 200) return json(prepared.status, prepared.body);
  const result = await registryCall(env, "revoke", {
    alias,
    operation_id: body.operation_id,
    expected_revision: lookup.revision,
    expected_barrier_epoch: lookup.barrierEpoch
  });
  if (result.status !== 200) {
    if (result.status === 404) return unknownRoute();
    if (result.status === 409) return json(409, { error: "registry_conflict" });
    return json(503, { error: "storage_unavailable" });
  }
  // The registry barrier is now committed. Finalization performs the single
  // irreversible Relay generation/epoch transition under the reservation.
  const revokedLookup = await lookupEntry(env, alias, true);
  if (revokedLookup.storageUnavailable || !revokedLookup.entry) return json(503, { error: "storage_unavailable" });
  const cleanupBody = {
    ...body,
    lifecycle_operation: lifecycleOperation,
    expected_registry_revision: revokedLookup.revision,
    expected_barrier_epoch: revokedLookup.barrierEpoch
  };
  const relay = await relayAdminCall(env, alias, revokedLookup, "revoke-finalize", cleanupBody);
  if (relay.status !== 200) return json(relay.status, relay.body);
  return json(200, { ok: true, state: "revoked", tombstoned: true, operation: operation === "transfer" ? "registration_required" : operation });
}

async function adminRevoke(request: Request, env: RelayEnvironment, alias: string, operation: "revoke" | "uninstall" | "transfer" = "revoke"): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  if (!isOpaqueAlias(alias)) return unknownRoute();
  const body = await readAdminBody(request);
  if (!body) return json(413, { error: "request_too_large" });
  if ((operation === "revoke" || operation === "uninstall" || operation === "transfer") && body.confirm !== true) return json(409, { error: "explicit_confirmation_required" });
  const operationError = requireMutationOperationId(body);
  if (operationError) return operationError;
  const lookup = await lookupEntry(env, alias, true);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  if (!expectedRegistryStateMatches(body, lookup)) return json(409, { error: "registry_conflict" });
  const lifecycleOperation = operation === "uninstall" || operation === "transfer" ? operation : "revoke";
  return performRevokeCommit(env, alias, lookup, body, operation, lifecycleOperation);
}

async function adminInvalidate(request: Request, env: RelayEnvironment, alias: string): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  const body = await readAdminBody(request);
  if (!body) return json(413, { error: "request_too_large" });
  const operationError = requireMutationOperationId(body);
  if (operationError) return operationError;
  const lookup = await lookupEntry(env, alias);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  const result = await relayAdminCall(env, alias, lookup, "invalidate", body);
  return json(result.status, result.body);
}

async function adminRecoverOpen(request: Request, env: RelayEnvironment, alias: string): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  const body = await readAdminBody(request);
  if (!body) return json(413, { error: "request_too_large" });
  const operationError = requireMutationOperationId(body);
  if (operationError) return operationError;
  if (body.confirm !== true) return json(409, { error: "explicit_confirmation_required" });
  const lookup = await lookupEntry(env, alias);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  const result = await relayAdminCall(env, alias, lookup, "recover-open", body);
  return json(result.status, result.body);
}

function adminRecoverFinalize(request: Request, env: RelayEnvironment): Response {
  // Administrator credentials can open the recovery barrier, but they cannot
  // manufacture the fresh publisher proof required to close it. Keep this
  // route as an authenticated, fixed refusal so callers do not mistake an
  // admin-only request for a completed recovery.
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  return json(409, { error: "fresh_publisher_proof_required" });
}

async function adminUpgrade(request: Request, env: RelayEnvironment, alias: string, operation: "upgrade" | "rollback", phase?: "stage" | "activate"): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  const body = await readAdminBody(request);
  if (!body) return json(413, { error: "request_too_large" });
  const operationError = requireMutationOperationId(body);
  if (operationError) return operationError;
  const lookup = await lookupEntry(env, alias);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  let requestedOperation: "upgrade-rollback" | "upgrade-activate" | "upgrade-stage";
  if (operation === "rollback") {
    requestedOperation = "upgrade-rollback";
  } else if (phase === "activate" || body.operation === "upgrade-activate" || body.operation === "activate") {
    requestedOperation = "upgrade-activate";
  } else {
    requestedOperation = "upgrade-stage";
  }
  const result = await relayAdminCall(env, alias, lookup, operation, { ...body, operation: requestedOperation });
  return json(result.status, result.body);
}

async function adminReconcileIdentity(request: Request, env: RelayEnvironment, alias: string): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  const body = await readAdminBody(request);
  if (!body) return json(413, { error: "request_too_large" });
  if (!operationId(body.operation_id)) return json(413, { error: "invalid_operation_id" });
  const lookup = await lookupEntry(env, alias);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  if (!expectedRegistryStateMatches(body, lookup)) return json(409, { error: "registry_conflict" });
  const result = await registryCall(env, "reconcile-identity", { ...body, alias, repository_id: Number.isSafeInteger(body.repository_id) ? body.repository_id : lookup.entry.repository_id, repository_owner_id: Number.isSafeInteger(body.repository_owner_id) ? body.repository_owner_id : lookup.entry.repository_owner_id, expected_revision: Number.isSafeInteger(body.expected_registry_revision) ? body.expected_registry_revision : lookup.revision, expected_barrier_epoch: Number.isSafeInteger(body.expected_barrier_epoch) ? body.expected_barrier_epoch : lookup.barrierEpoch });
  if (result.status !== 200) return json(result.status, { error: "identity_mismatch" });
  const updatedEntry = validateRegistryEntry(result.body.entry) ? result.body.entry as RegistryEntry : lookup.entry;
  const revision = typeof result.body.revision === "number" ? result.body.revision : lookup.revision + 1;
  const barrierEpoch = typeof result.body.barrier_epoch === "number" ? result.body.barrier_epoch : lookup.barrierEpoch;
  const synced = await relayAdminCall(env, alias, {
    entry: updatedEntry,
    revision,
    barrierEpoch,
    tombstoned: false,
    storageUnavailable: false
  }, "sync", {
    registry_revision: revision,
    barrier_epoch: barrierEpoch
  });
  if (synced.status !== 200) return json(503, { error: "storage_unavailable" });
  return json(200, { ok: true, alias, operation: "reconcile-identity" });
}

function buildRotateCandidate(entry: RegistryEntry, body: Record<string, unknown>): RegistryEntry | undefined {
  const candidate: RegistryEntry = { ...entry, job_workflow_ref: typeof body.job_workflow_ref === "string" ? body.job_workflow_ref : entry.job_workflow_ref, job_workflow_sha: typeof body.job_workflow_sha === "string" ? body.job_workflow_sha : entry.job_workflow_sha, audience: typeof body.audience === "string" ? body.audience : entry.audience };
  if (!validateRegistryEntry(candidate) || typeof candidate.audience !== "string" || candidate.audience.length === 0 || candidate.audience.length > 256) return undefined;
  return candidate;
}

async function performRotateCommit(env: RelayEnvironment, alias: string, lookup: RegistryLookup, body: Record<string, unknown>, candidate: RegistryEntry, rotateOperationId: string): Promise<Response> {
  const invalidated = await relayAdminCall(env, alias, lookup, "invalidate", { ...body, operation_id: rotateOperationId });
  if (invalidated.status !== 200) return json(invalidated.status, invalidated.body);
  const result = await registryCall(env, "rotate", { alias, entry: candidate, expected_revision: Number.isSafeInteger(body.expected_registry_revision) ? body.expected_registry_revision : lookup.revision, expected_barrier_epoch: Number.isSafeInteger(body.expected_barrier_epoch) ? body.expected_barrier_epoch : lookup.barrierEpoch, operation_id: rotateOperationId });
  if (result.status !== 200) return json(409, { error: "rotation_conflict" });
  const revision = typeof result.body.revision === "number" ? result.body.revision : lookup.revision + 1;
  const barrierEpoch = typeof result.body.barrier_epoch === "number" ? result.body.barrier_epoch : lookup.barrierEpoch + 1;
  const synced = await relayAdminCall(env, alias, { ...lookup, revision, barrierEpoch }, "sync", { registry_revision: revision, barrier_epoch: barrierEpoch });
  if (synced.status !== 200) return json(503, { error: "storage_unavailable" });
  return json(200, { ok: true, state: "unavailable", alias, operation: "rotate" });
}

async function adminRotate(request: Request, env: RelayEnvironment, alias: string): Promise<Response> {
  if (!adminAuthorized(request, env)) return json(401, { error: "unauthorized" });
  const body = await readAdminBody(request);
  if (!body) return json(413, { error: "request_too_large" });
  const operationError = requireMutationOperationId(body);
  if (operationError) return operationError;
  const lookup = await lookupEntry(env, alias);
  if (lookup.storageUnavailable) return json(503, { error: "storage_unavailable" });
  if (!lookup.entry) return unknownRoute();
  if (!expectedRegistryStateMatches(body, lookup)) return json(409, { error: "registry_conflict" });
  const candidate = buildRotateCandidate(lookup.entry, body);
  if (!candidate) return json(409, { error: "invalid_pin" });
  const rotateOperationId = body.operation_id as string;
  return performRotateCommit(env, alias, lookup, body, candidate, rotateOperationId);
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

interface PublicRoute {
  alias: string;
  representation?: PublicRepresentation;
}

function publicRoute(pathname: string): PublicRoute | undefined {
  const parts = pathname.split("/").filter(Boolean);
  if (parts[0] !== "badge-relay" || parts[1] !== "v1") return undefined;
  // The extension form is accepted for compatibility with image URLs while
  // the segment form keeps the versioned route easy to compose.
  if (parts.length === 3 && isOpaqueAlias(parts[2])) return { alias: parts[2] };
  if (parts.length === 4 && isOpaqueAlias(parts[2]) && (parts[3] === "json" || parts[3] === "svg")) return { alias: parts[2], representation: parts[3] };
  if (parts.length === 3) {
    let representation: PublicRepresentation | undefined;
    let suffixLength = 0;
    if (parts[2].endsWith(".json")) {
      representation = "json";
      suffixLength = ".json".length;
    } else if (parts[2].endsWith(".svg")) {
      representation = "svg";
      suffixLength = ".svg".length;
    }
    const alias = parts[2].slice(0, -suffixLength);
    if (representation && isOpaqueAlias(alias)) return { alias, representation };
  }
  return undefined;
}

interface RegistryLookup {
  entry?: RegistryEntry;
  revision: number;
  barrierEpoch: number;
  tombstoned: boolean;
  storageUnavailable: boolean;
}

async function lookupEntry(env: RelayEnvironment, alias: string, includeTombstoned = false): Promise<RegistryLookup> {
  try {
    const result = await registryCall(env, "lookup", { alias, include_tombstoned: includeTombstoned });
    const candidate = result.status === 200 ? result.body.entry : undefined;
    return {
      entry: validateRegistryEntry(candidate) ? candidate as RegistryEntry : undefined,
      revision: typeof result.body.revision === "number" ? result.body.revision : 1,
      barrierEpoch: typeof result.body.barrier_epoch === "number" ? result.body.barrier_epoch : 1,
      tombstoned: result.body.tombstoned === true,
      storageUnavailable: result.status === 503
    };
  } catch {
    return { revision: 1, barrierEpoch: 1, tombstoned: false, storageUnavailable: true };
  }
}

function representationFor(entry: RegistryEntry, route: PublicRoute): PublicRepresentation {
  if (route.representation) return route.representation;
  return entry.disclosure_profile === "headline-plus-freshness/v1" ? "svg" : "json";
}

async function forwardPublicRead(request: Request, env: RelayEnvironment, route: PublicRoute): Promise<Response> {
  const lookup = await lookupEntry(env, route.alias);
  if (lookup.storageUnavailable) return publicUnavailableResponse(request, route.representation ?? "json", 503);
  if (!lookup.entry) return unknownRoute();
  const entry = registryEntryFromConfig(env, lookup.entry);
  const representation = representationFor(entry, route);
  if (!env.RELAY || typeof env.RELAY.idFromName !== "function") return publicUnavailableResponse(request, representation, 503);

  const target = new URL(request.url);
  target.pathname = "/internal/" + route.alias + "/read/" + representation;
  const headers = new Headers(request.headers);
  headers.set("x-relay-registry", entryForHeader(entry));
  headers.set("x-relay-registry-revision", String(lookup.revision));
  headers.set("x-relay-barrier-epoch", String(lookup.barrierEpoch));
  const stub = env.RELAY.get(env.RELAY.idFromName(route.alias));
  return (stub.fetch as unknown as (input: unknown) => Promise<Response>)(new Request(target, { method: request.method, headers }));
}

async function forwardMutation(request: Request, env: RelayEnvironment, parts: string[]): Promise<Response> {
  if (request.method !== "POST" || parts.length < 4 || !isOpaqueAlias(parts[2])) return unknownRoute();
  const alias = parts[2];
  const lookup = await lookupEntry(env, alias);
  if (lookup.storageUnavailable) throw new Error("registry unavailable");
  if (!lookup.entry) return unknownRoute();
  if (!env.RELAY || typeof env.RELAY.idFromName !== "function") return json(503, { error: "storage_unavailable" });

  const entry = registryEntryFromConfig(env, lookup.entry);
  const target = new URL(request.url);
  target.pathname = "/internal/" + alias + "/" + parts[3];
  const headers = new Headers(request.headers);
  headers.set("x-relay-registry", entryForHeader(entry));
  headers.set("x-relay-registry-revision", String(lookup.revision));
  headers.set("x-relay-barrier-epoch", String(lookup.barrierEpoch));
  const stub = env.RELAY.get(env.RELAY.idFromName(alias));
  return (stub.fetch as unknown as (input: unknown) => Promise<Response>)(new Request(target, { method: request.method, headers, body: request.body }));
}

async function handleAdminCompatRoutes(request: Request, env: RelayEnvironment, parts: string[]): Promise<Response | undefined> {
  if (parts[3] === "register" && request.method === "POST") return adminRegister(request, env);
  // Keep the original /admin/revoke/{alias} route as a compatibility alias,
  // while the versioned control plane uses /admin/{alias}/{operation}.
  if (parts[3] === "revoke" && request.method === "POST") return adminRevoke(request, env, parts[4] ?? "", "revoke");
  return undefined;
}

interface SimpleAdminRoute {
  operation: string;
  method: string;
  handler: (request: Request, env: RelayEnvironment, alias: string) => Promise<Response>;
}

const SIMPLE_ADMIN_ROUTES: SimpleAdminRoute[] = [
  { operation: "status", method: "GET", handler: adminStatus },
  { operation: "reconcile-identity", method: "POST", handler: adminReconcileIdentity },
  { operation: "revoke", method: "POST", handler: (r, e, a) => adminRevoke(r, e, a, "revoke") },
  { operation: "uninstall", method: "POST", handler: (r, e, a) => adminRevoke(r, e, a, "uninstall") },
  { operation: "transfer", method: "POST", handler: (r, e, a) => adminRevoke(r, e, a, "transfer") },
  { operation: "invalidate", method: "POST", handler: adminInvalidate },
  { operation: "rotate", method: "POST", handler: adminRotate }
];

async function handleAdminSimpleOperation(request: Request, env: RelayEnvironment, alias: string, operation: string): Promise<Response | undefined> {
  const route = SIMPLE_ADMIN_ROUTES.find((candidate) => candidate.operation === operation && candidate.method === request.method);
  return route ? route.handler(request, env, alias) : undefined;
}

async function handleAdminRecoverOperation(request: Request, env: RelayEnvironment, alias: string, operation: string, parts: string[]): Promise<Response | undefined> {
  if (operation !== "recover") return undefined;
  if (parts[5] === "open" && request.method === "POST") return adminRecoverOpen(request, env, alias);
  if (parts[5] === "finalize" && request.method === "POST") return adminRecoverFinalize(request, env);
  return undefined;
}

async function handleAdminUpgradeOperation(request: Request, env: RelayEnvironment, alias: string, operation: string, parts: string[]): Promise<Response | undefined> {
  if (operation === "upgrade" && request.method === "POST" && (parts[5] === undefined || parts[5] === "stage" || parts[5] === "activate")) return adminUpgrade(request, env, alias, "upgrade", parts[5] as "stage" | "activate" | undefined);
  if (operation === "upgrade" && parts[5] === "rollback" && request.method === "POST") return adminUpgrade(request, env, alias, "rollback");
  if (operation === "rollback" && request.method === "POST") return adminUpgrade(request, env, alias, "rollback");
  return undefined;
}

async function handleAdminOperation(request: Request, env: RelayEnvironment, alias: string, operation: string, parts: string[]): Promise<Response | undefined> {
  const simple = await handleAdminSimpleOperation(request, env, alias, operation);
  if (simple) return simple;
  const recover = await handleAdminRecoverOperation(request, env, alias, operation, parts);
  if (recover) return recover;
  return handleAdminUpgradeOperation(request, env, alias, operation, parts);
}

async function handleAdminRoute(request: Request, env: RelayEnvironment, parts: string[]): Promise<Response> {
  const compat = await handleAdminCompatRoutes(request, env, parts);
  if (compat) return compat;
  if (parts.length < 5 || !isOpaqueAlias(parts[3])) return unknownRoute();
  const alias = parts[3];
  const operation = parts[4];
  const result = await handleAdminOperation(request, env, alias, operation, parts);
  return result ?? unknownRoute();
}

async function handleRequest(request: Request, env: RelayEnvironment): Promise<Response> {
  const url = new URL(request.url);
  const parts = url.pathname.split("/").filter(Boolean);
  const isAdmin = parts[0] === "badge-relay" && parts[1] === "v1" && parts[2] === "admin";
  if (isAdmin) return handleAdminRoute(request, env, parts);
  if (request.method === "GET" || request.method === "HEAD") {
    const route = publicRoute(url.pathname);
    return route ? forwardPublicRead(request, env, route) : unknownRoute();
  }
  return forwardMutation(request, env, parts);
}

export default {
  async fetch(request: Request, env: RelayEnvironment): Promise<Response> {
    try {
      return await handleRequest(request, env);
    } catch {
      return json(503, { error: "storage_unavailable" });
    }
  }
};

// Explicit constants are useful to deployment checks and make accidental
// token-derived trust configuration difficult to introduce.
export const OIDC_TRUST = Object.freeze({ issuer: FIXED_GITHUB_ISSUER, jwks_uri: FIXED_GITHUB_JWKS, algorithms: ["RS256"] as const });
