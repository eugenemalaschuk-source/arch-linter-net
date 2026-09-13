import {
  BUNDLE,
  COMPATIBILITY_PLAN,
  CONTRACT_VERSION,
  OPERATION_HISTORY_LIMIT,
  OPERATION_RETENTION_SECONDS,
  TOMBSTONE_RETENTION_SECONDS,
  type RelayState
} from "./types";

export const SUPPORTED_BUNDLE = BUNDLE;
export const SUPPORTED_CONTRACT_VERSION = CONTRACT_VERSION;
export const SUPPORTED_COMPATIBILITY_PLAN = COMPATIBILITY_PLAN;

export function isBundleDigest(value: unknown): value is string {
  return typeof value === "string" && /^[0-9a-f]{64}$/u.test(value);
}

export function isKnownBundleDigest(value: unknown, allowlist: ReadonlySet<string>, current?: string | null): value is string {
  return isBundleDigest(value) && (allowlist.has(value) || value === current);
}

export type LifecycleOperation =
  | "status"
  | "reconcile-identity"
  | "revoke"
  | "recover-open"
  | "recover-finalize"
  | "upgrade-stage"
  | "upgrade-activate"
  | "upgrade-rollback"
  | "uninstall";

export type LifecycleReason =
  | "ok"
  | "invalid_request"
  | "identity_mismatch"
  | "registration_required"
  | "compatibility_conflict"
  | "stale_registry_barrier"
  | "already_revoked"
  | "recovery_required"
  | "storage_unavailable"
  | "quota_exceeded"
  | "authorization_failed"
  | "expired";

export interface LifecycleStatusSnapshot {
  state: RelayState;
  generation: number;
  revocation_epoch: number;
  registry_revision: number;
  barrier_epoch: number;
  profile: string;
  bundle: string;
  contract_version: string;
  compatibility_plan: string;
  active_digest?: string | null;
  staged_digest?: string | null;
  previous_verified_digest?: string | null;
  display_owner?: string | null;
  display_repository?: string | null;
  verified_at: string | null;
  valid_until: string | null;
  tombstoned: boolean;
  last_operation: LifecycleOperation | null;
  last_reason: LifecycleReason | null;
  updated_at: string;
}

export interface CompatibilityDescriptor {
  bundle: unknown;
  contract_version: unknown;
  compatibility_plan: unknown;
  bundle_digest?: unknown;
}

export function isSupportedCompatibility(value: CompatibilityDescriptor): boolean {
  return value.bundle === SUPPORTED_BUNDLE
    && value.contract_version === SUPPORTED_CONTRACT_VERSION
    && value.compatibility_plan === SUPPORTED_COMPATIBILITY_PLAN
    && (value.bundle_digest === undefined || isBundleDigest(value.bundle_digest));
}

export function compatibilityReason(value: CompatibilityDescriptor): LifecycleReason {
  return isSupportedCompatibility(value) ? "ok" : "compatibility_conflict";
}

export function isIdentityPreserving(current: { repository_id: number; repository_owner_id: number }, next: { repository_id?: unknown; repository_owner_id?: unknown }): boolean {
  return next.repository_id === current.repository_id && next.repository_owner_id === current.repository_owner_id;
}

export function validateDisplayIdentity(value: unknown): value is string {
  return typeof value === "string" && value.length > 0 && value.length <= 100 && /^[A-Za-z0-9_.-]+$/u.test(value);
}

/**
 * Keep the private status contract deliberately small. This function is also
 * used by tests and adapters to guarantee that a future column cannot leak
 * payload, source, or provider provenance accidentally.
 */
export function redactStatus(value: Partial<LifecycleStatusSnapshot> & Record<string, unknown>): Record<string, unknown> {
  return {
    state: typeof value.state === "string" ? value.state : "unavailable",
    generation: Number.isSafeInteger(value.generation) ? value.generation : 0,
    revocation_epoch: Number.isSafeInteger(value.revocation_epoch) ? value.revocation_epoch : 0,
    registry_revision: Number.isSafeInteger(value.registry_revision) ? value.registry_revision : 0,
    barrier_epoch: Number.isSafeInteger(value.barrier_epoch) ? value.barrier_epoch : 0,
    profile: typeof value.profile === "string" ? value.profile : "unknown",
    bundle: SUPPORTED_BUNDLE,
    contract_version: SUPPORTED_CONTRACT_VERSION,
    compatibility_plan: SUPPORTED_COMPATIBILITY_PLAN,
    active_digest: isBundleDigest(value.active_digest) ? value.active_digest : null,
    staged_digest: isBundleDigest(value.staged_digest) ? value.staged_digest : null,
    previous_verified_digest: isBundleDigest(value.previous_verified_digest) ? value.previous_verified_digest : null,
    display_owner: typeof value.display_owner === "string" ? value.display_owner : null,
    display_repository: typeof value.display_repository === "string" ? value.display_repository : null,
    verified_at: typeof value.verified_at === "string" ? value.verified_at : null,
    valid_until: typeof value.valid_until === "string" ? value.valid_until : null,
    tombstoned: value.tombstoned === true,
    last_operation: typeof value.last_operation === "string" ? value.last_operation : null,
    last_reason: typeof value.last_reason === "string" ? value.last_reason : null,
    updated_at: typeof value.updated_at === "string" ? value.updated_at : new Date(0).toISOString()
  };
}

export const LIFECYCLE_BOUNDS = Object.freeze({
  operationRetentionSeconds: OPERATION_RETENTION_SECONDS,
  operationHistoryLimit: OPERATION_HISTORY_LIMIT,
  tombstoneRetentionSeconds: TOMBSTONE_RETENTION_SECONDS
});
