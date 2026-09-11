import {
  decodeProtectedHeader,
  importJWK,
  jwtVerify,
  type JWK,
} from "jose";
import {
  BUNDLE,
  CLOCK_SKEW_SECONDS,
  FIXED_GITHUB_ISSUER,
  FIXED_GITHUB_JWKS,
  MAX_TOKEN_BYTES,
  type OidcClaims,
  type OidcTrust,
  type RegistryEntry,
  type ValidatedPublisher
} from "./types";

const jwksCache = new Map<string, Map<string, JWK>>();

export class AuthorizationError extends Error {
  readonly status: number;
  readonly reason: string;

  constructor(status = 401, reason = "unauthorized") {
    super(reason);
    this.name = "AuthorizationError";
    this.status = status;
    this.reason = reason;
  }
}

function asPlainObject(value: unknown): Record<string, unknown> | undefined {
  if (value === null || typeof value !== "object" || Array.isArray(value)) return undefined;
  return value as Record<string, unknown>;
}

export function getBearerToken(request: Request): string {
  const header = request.headers.get("authorization");
  if (!header || !/^Bearer [^\s]+$/u.test(header)) throw new AuthorizationError(401);
  const token = header.slice(7);
  if (new TextEncoder().encode(token).byteLength > MAX_TOKEN_BYTES) throw new AuthorizationError(413);
  return token;
}

function audienceMatches(audience: unknown, expected: string): boolean {
  if (typeof audience === "string") return audience === expected;
  return Array.isArray(audience) && audience.length > 0 && audience.every((part) => typeof part === "string") && audience.includes(expected);
}

function integerClaim(claims: OidcClaims, key: "iat" | "exp" | "nbf"): number {
  const value = claims[key];
  if (typeof value !== "number" || !Number.isSafeInteger(value)) throw new AuthorizationError(401);
  return value;
}

function exactSubject(entry: RegistryEntry, trust: OidcTrust): string {
  if (entry.subject) return entry.subject;
  if (trust.subject) return trust.subject;
  const owner = entry.owner;
  const repository = entry.repository;
  if (!owner || !repository) throw new AuthorizationError(403);
  return `repo:${owner}/${repository}:ref:${entry.permitted_ref}`;
}

/**
 * Validate the signed claims against the immutable registry context. This is
 * exported separately so conformance fixtures can exercise the claim seam
 * without inventing a second cryptographic trust chain.
 */
export function validateOidcClaims(claims: OidcClaims, entry: RegistryEntry, now = Math.floor(Date.now() / 1000), trust?: OidcTrust): void {
  const oidcTrust: OidcTrust = trust ?? { issuer: FIXED_GITHUB_ISSUER, jwks_uri: FIXED_GITHUB_JWKS, audience: entry.audience ?? "" };
  if (oidcTrust.issuer !== FIXED_GITHUB_ISSUER || oidcTrust.jwks_uri !== FIXED_GITHUB_JWKS) throw new AuthorizationError(401);
  if (claims.iss !== FIXED_GITHUB_ISSUER) throw new AuthorizationError(401);
  if (!audienceMatches(claims.aud, oidcTrust.audience || entry.audience || "")) throw new AuthorizationError(401);

  const iat = integerClaim(claims, "iat");
  const exp = integerClaim(claims, "exp");
  const nbf = integerClaim(claims, "nbf");
  if (iat > now + CLOCK_SKEW_SECONDS || nbf > now + CLOCK_SKEW_SECONDS) throw new AuthorizationError(401);
  if (exp < now - CLOCK_SKEW_SECONDS || exp <= iat || exp - iat > 10 * 60) throw new AuthorizationError(401);
  if (typeof claims.jti !== "string" || claims.jti.length === 0 || claims.jti.length > 256) throw new AuthorizationError(401);

  if (claims.repository_id !== entry.repository_id || claims.repository_owner_id !== entry.repository_owner_id) throw new AuthorizationError(403);
  if (claims.event_name !== entry.permitted_event || claims.ref !== entry.permitted_ref) throw new AuthorizationError(403);
  if (claims.job_workflow_ref !== entry.job_workflow_ref || claims.job_workflow_sha !== entry.job_workflow_sha) throw new AuthorizationError(403);
  if (claims.sub !== exactSubject(entry, oidcTrust)) throw new AuthorizationError(403);
}

async function fetchJwks(url: string, fetcher: typeof fetch): Promise<Map<string, JWK>> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 2_000);
  try {
    const response = await fetcher(url, { method: "GET", headers: { accept: "application/json" }, signal: controller.signal });
    if (!response.ok) throw new Error("jwks unavailable");
    const document = await response.json() as { keys?: unknown };
    if (!Array.isArray(document.keys) || document.keys.length > 32) throw new Error("jwks invalid");
    const keys = new Map<string, JWK>();
    for (const candidate of document.keys) {
      const key = asPlainObject(candidate) as JWK | undefined;
      if (!key || typeof key.kid !== "string" || key.kid.length > 128 || (key.alg !== undefined && key.alg !== "RS256") || key.kty !== "RSA") continue;
      keys.set(key.kid, key);
    }
    if (keys.size === 0) throw new Error("jwks has no supported keys");
    return keys;
  } finally {
    clearTimeout(timer);
  }
}

async function signingKey(kid: string, fetcher: typeof fetch): Promise<CryptoKey | Uint8Array> {
  const cached = jwksCache.get(FIXED_GITHUB_JWKS);
  let keys = cached;
  if (!keys || !keys.has(kid)) {
    // An unknown key may cause exactly one refresh of the fixed endpoint.
    keys = await fetchJwks(FIXED_GITHUB_JWKS, fetcher);
    jwksCache.set(FIXED_GITHUB_JWKS, keys);
  }
  const jwk = keys.get(kid);
  if (!jwk) throw new AuthorizationError(401);
  try {
    return await importJWK(jwk, "RS256");
  } catch {
    throw new AuthorizationError(401);
  }
}

export interface VerifyOptions {
  now?: number;
  fetcher?: typeof fetch;
  trust?: OidcTrust;
}

export async function verifyOidcToken(token: string, entry: RegistryEntry, options: VerifyOptions = {}): Promise<ValidatedPublisher> {
  if (new TextEncoder().encode(token).byteLength > MAX_TOKEN_BYTES) throw new AuthorizationError(413);
  let protectedHeader: { alg?: unknown; kid?: unknown };
  try {
    protectedHeader = decodeProtectedHeader(token);
  } catch {
    throw new AuthorizationError(401);
  }
  if (protectedHeader.alg !== "RS256" || typeof protectedHeader.kid !== "string" || protectedHeader.kid.length === 0 || protectedHeader.kid.length > 128) throw new AuthorizationError(401);

  const trust: OidcTrust = options.trust ?? {
    issuer: FIXED_GITHUB_ISSUER,
    jwks_uri: FIXED_GITHUB_JWKS,
    audience: entry.audience ?? ""
  };
  if (trust.issuer !== FIXED_GITHUB_ISSUER || trust.jwks_uri !== FIXED_GITHUB_JWKS) throw new AuthorizationError(401);
  const key = await signingKey(protectedHeader.kid, options.fetcher ?? fetch);
  const now = options.now ?? Math.floor(Date.now() / 1000);
  let claims: OidcClaims;
  try {
    const result = await jwtVerify(token, key, {
      algorithms: ["RS256"],
      issuer: FIXED_GITHUB_ISSUER,
      audience: trust.audience || entry.audience,
      clockTolerance: CLOCK_SKEW_SECONDS,
      currentDate: new Date(now * 1000)
    });
    claims = result.payload as OidcClaims;
  } catch {
    throw new AuthorizationError(401);
  }
  validateOidcClaims(claims, entry, now, trust);
  const jti = claims.jti as string;
  return { claims, jti, jtiHash: await sha256Hex(jti) };
}

export async function sha256Hex(value: string | ArrayBuffer | Uint8Array): Promise<string> {
  const bytes = typeof value === "string" ? new TextEncoder().encode(value) : value;
  const digest = await crypto.subtle.digest("SHA-256", bytes as BufferSource);
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

export function isTrustedContext(value: unknown): value is { valid: true; kind: "github-pr-authoritative/v1"; digest?: string; semantic_horizon?: string; tree_sha?: string } {
  const context = asPlainObject(value);
  return context?.valid === true && context.kind === "github-pr-authoritative/v1";
}

export function clearJwksCacheForTests(): void {
  jwksCache.clear();
}
