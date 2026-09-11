import type { DurableObjectNamespace, DurableObjectState } from "@cloudflare/workers-types";

export const FIXED_GITHUB_ISSUER = "https://token.actions.githubusercontent.com";
export const FIXED_GITHUB_JWKS = "https://token.actions.githubusercontent.com/.well-known/jwks";
export const BUNDLE = "badge-relay/v1";
export const MAX_TOKEN_BYTES = 16 * 1024;
export const MAX_PUBLIC_PAYLOAD_BYTES = 16 * 1024;
export const MAX_MANIFEST_BYTES = 16 * 1024;
export const MAX_REQUEST_BYTES = 64 * 1024;
export const CLOCK_SKEW_SECONDS = 300;
export const CHALLENGE_SECONDS = 5 * 60;
export const LEASE_SECONDS = 60 * 60;
export const RENEWAL_MINIMUM_SECONDS = 30 * 60;

export type DisclosureProfile = "headline-only/v1" | "headline-plus-freshness/v1";
export type RelayState = "unregistered" | "unavailable" | "ready" | "expired" | "revoked" | "needs-recovery";

export interface RegistryEntry {
  repository_id: number;
  repository_owner_id: number;
  owner?: string;
  repository?: string;
  destination_alias: string;
  permitted_event: "push";
  permitted_ref: "refs/heads/main";
  job_workflow_ref: string;
  job_workflow_sha: string;
  disclosure_profile: DisclosureProfile;
  subject?: string;
  audience?: string;
  consent?: boolean;
  initial_state?: {
    generation?: number;
    revocation_epoch?: number;
    tree_sha?: string;
    semantic_horizon?: string;
    ready_digest?: string;
  };
}

export interface OidcTrust {
  issuer?: string;
  jwks_uri?: string;
  audience: string;
  subject?: string;
}

export interface OidcClaims {
  iss?: unknown;
  aud?: unknown;
  sub?: unknown;
  repository_id?: unknown;
  repository_owner_id?: unknown;
  event_name?: unknown;
  ref?: unknown;
  job_workflow_ref?: unknown;
  job_workflow_sha?: unknown;
  iat?: unknown;
  exp?: unknown;
  nbf?: unknown;
  jti?: unknown;
  [claim: string]: unknown;
}

export interface ValidatedPublisher {
  readonly claims: OidcClaims;
  readonly jti: string;
  readonly jtiHash: string;
}

export interface RelayEnvironment {
  RELAY: DurableObjectNamespace;
  REGISTRY: DurableObjectNamespace;
  RELAY_REGISTRY?: string | RegistryEntry | RegistryEntry[] | Record<string, RegistryEntry>;
  ADMIN_TOKEN?: string;
  OIDC_JWKS_URL?: string;
  OIDC_FETCH?: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
}

export interface SqlCursor<T = Record<string, unknown>> {
  toArray(): T[];
}

export interface SqlStorageLike {
  exec<T = Record<string, unknown>>(query: string, ...bindings: unknown[]): SqlCursor<T>;
}

export interface RelayStateLike {
  storage: { sql: SqlStorageLike; transactionSync<T>(closure: () => T): T };
  blockConcurrencyWhile(callback: () => Promise<void> | void): Promise<void>;
}

export interface CanonicalPayload {
  schemaVersion: 1;
  label: "architecture";
  message: string;
  color: "brightgreen" | "yellow" | "orange" | "red" | "lightgrey";
  verified_at?: string;
  valid_until?: string;
}

export interface TrustedContextProof {
  /** Opaque assertion supplied by the later artifact/context verifier only. */
  valid: true;
  kind: "github-pr-authoritative/v1";
  digest?: string;
  tree_sha?: string;
  semantic_horizon?: string;
}

export interface PrepareRequest {
  operation: "prepare" | "renew";
  canonical_bytes: string;
  canonical_digest: string;
  profile: DisclosureProfile;
  state?: "ready";
  idempotency_key: string;
  expected_generation?: number;
  expected_revocation_epoch?: number;
  semantic_horizon?: string;
}

export interface PublishRequest {
  operation: "publish" | "renew" | "recover";
  challenge_id: string;
  idempotency_key: string;
  canonical_bytes: string;
  canonical_digest: string;
  profile: DisclosureProfile;
  expected_generation: number;
  expected_revocation_epoch: number;
  semantic_horizon?: string;
}

export interface LifecycleRequest {
  operation: "invalidate" | "revoke" | "recover";
  expected_generation?: number;
  expected_revocation_epoch?: number;
}

export type RelayRequest = PrepareRequest | PublishRequest | LifecycleRequest;

export interface WorkerResponse {
  status: number;
  body?: unknown;
  headers?: Record<string, string>;
}
