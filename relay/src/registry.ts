import { BUNDLE, FIXED_GITHUB_ISSUER, FIXED_GITHUB_JWKS, type DisclosureProfile, type RegistryEntry } from "./types";

const ALIAS = /^a[0-9a-z]{7}$/u;
const SHA = /^[0-9a-f]{40}$/u;
const PERMITTED_REF = /^refs\/heads\/[A-Za-z0-9][A-Za-z0-9._/-]{0,127}$/u;

export function isOpaqueAlias(value: unknown): value is string {
  return typeof value === "string" && ALIAS.test(value);
}

export function validateRegistryEntry(value: unknown): value is RegistryEntry {
  if (value === null || typeof value !== "object" || Array.isArray(value)) return false;
  const entry = value as Partial<RegistryEntry>;
  return Number.isSafeInteger(entry.repository_id) && (entry.repository_id as number) > 0
    && Number.isSafeInteger(entry.repository_owner_id) && (entry.repository_owner_id as number) > 0
    && isOpaqueAlias(entry.destination_alias)
    && entry.permitted_event === "push"
    && (entry.permitted_events === undefined || (Array.isArray(entry.permitted_events)
      && entry.permitted_events.length > 0
      && entry.permitted_events.length <= 2
      && entry.permitted_events.every((event) => event === "push" || event === "schedule")
      && new Set(entry.permitted_events).size === entry.permitted_events.length
      && entry.permitted_events.includes(entry.permitted_event)))
    && typeof entry.permitted_ref === "string" && PERMITTED_REF.test(entry.permitted_ref)
    && !entry.permitted_ref.includes("..") && !entry.permitted_ref.includes("//") && !entry.permitted_ref.includes("/.")
    && !entry.permitted_ref.endsWith("/") && !entry.permitted_ref.endsWith(".")
    && typeof entry.job_workflow_ref === "string" && entry.job_workflow_ref.length > 0 && entry.job_workflow_ref.length <= 512
    && typeof entry.job_workflow_sha === "string" && SHA.test(entry.job_workflow_sha)
    && (entry.disclosure_profile === "headline-only/v1" || entry.disclosure_profile === "headline-plus-freshness/v1")
    && (entry.consent === undefined || entry.consent === true);
}

export function registryEntriesFromConfig(config: unknown): RegistryEntry[] {
  if (!config) return [];
  let parsed: unknown = config;
  if (typeof parsed === "string") {
    try { parsed = JSON.parse(parsed); } catch { return []; }
  }
  if (validateRegistryEntry(parsed)) return [parsed];
  if (Array.isArray(parsed)) return parsed.filter(validateRegistryEntry);
  if (parsed && typeof parsed === "object") {
    const object = parsed as Record<string, unknown>;
    if (validateRegistryEntry(object.registry_entry)) return [object.registry_entry];
    if (Array.isArray(object.entries)) return object.entries.filter(validateRegistryEntry);
    return Object.values(object).filter(validateRegistryEntry);
  }
  return [];
}

export function validateBundleConfig(config: unknown): boolean {
  if (!config || typeof config !== "object" || Array.isArray(config)) return false;
  const candidate = config as Record<string, unknown>;
  return candidate.schema_id === undefined || candidate.schema_id === "architecture-health-badge-relay-config/v1"
    ? (candidate.mode === undefined || candidate.mode === "relay")
      && (candidate.bundle === undefined || candidate.bundle === BUNDLE)
      && (candidate.oidc_trust === undefined || validateTrustConfig(candidate.oidc_trust))
    : false;
}

function validateTrustConfig(value: unknown): boolean {
  if (!value || typeof value !== "object") return false;
  const trust = value as Record<string, unknown>;
  return trust.issuer === FIXED_GITHUB_ISSUER
    && trust.jwks_uri === FIXED_GITHUB_JWKS
    && typeof trust.audience === "string" && trust.audience.length > 0
    && (trust.allowed_algorithms === undefined || JSON.stringify(trust.allowed_algorithms) === '["RS256"]');
}

export function profileFromEntry(entry: RegistryEntry): DisclosureProfile {
  return entry.disclosure_profile;
}
