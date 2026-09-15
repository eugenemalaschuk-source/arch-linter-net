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
  | "revoke-prepare"
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
  | "revoke_pending"
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

function redactedString(value: unknown, fallback: string): string {
  return typeof value === "string" ? value : fallback;
}

function redactedNullableString(value: unknown): string | null {
  return typeof value === "string" ? value : null;
}

function redactedSafeInteger(value: unknown, fallback: number): number {
  return Number.isSafeInteger(value) ? (value as number) : fallback;
}

function redactedDigest(value: unknown): string | null {
  return isBundleDigest(value) ? value : null;
}

function redactedDisplayIdentity(value: unknown): string | null {
  return validateDisplayIdentity(value) ? value : null;
}

/**
 * Keep the private status contract deliberately small. This function is also
 * used by tests and adapters to guarantee that a future column cannot leak
 * payload, source, or provider provenance accidentally.
 */
export function redactStatus(value: Partial<LifecycleStatusSnapshot> & Record<string, unknown>): Record<string, unknown> {
  return {
    state: redactedString(value.state, "unavailable"),
    generation: redactedSafeInteger(value.generation, 0),
    revocation_epoch: redactedSafeInteger(value.revocation_epoch, 0),
    registry_revision: redactedSafeInteger(value.registry_revision, 0),
    barrier_epoch: redactedSafeInteger(value.barrier_epoch, 0),
    profile: redactedString(value.profile, "unknown"),
    bundle: SUPPORTED_BUNDLE,
    contract_version: SUPPORTED_CONTRACT_VERSION,
    compatibility_plan: SUPPORTED_COMPATIBILITY_PLAN,
    active_digest: redactedDigest(value.active_digest),
    staged_digest: redactedDigest(value.staged_digest),
    previous_verified_digest: redactedDigest(value.previous_verified_digest),
    display_owner: redactedDisplayIdentity(value.display_owner),
    display_repository: redactedDisplayIdentity(value.display_repository),
    verified_at: redactedNullableString(value.verified_at),
    valid_until: redactedNullableString(value.valid_until),
    tombstoned: value.tombstoned === true,
    last_operation: redactedNullableString(value.last_operation),
    last_reason: redactedNullableString(value.last_reason),
    updated_at: redactedString(value.updated_at, new Date(0).toISOString())
  };
}

export const LIFECYCLE_BOUNDS = Object.freeze({
  operationRetentionSeconds: OPERATION_RETENTION_SECONDS,
  operationHistoryLimit: OPERATION_HISTORY_LIMIT,
  tombstoneRetentionSeconds: TOMBSTONE_RETENTION_SECONDS
});
