import { describe, expect, it } from "vitest";
import worker, {
  AuthorizationError,
  canonicalizePayload,
  getBearerToken,
  isOpaqueAlias,
  isTrustedContext,
  PayloadError,
  profileFromEntry,
  registryEntriesFromConfig,
  sha256Hex,
  validateBundleConfig,
  validateCanonicalPayload,
  validateOidcClaims,
  validateRegistryEntry,
  type OidcClaims,
  type RegistryEntry,
  type RelayEnvironment
} from "../src/index";

const entry: RegistryEntry = {
  repository_id: 700000042,
  repository_owner_id: 7000000042,
  owner: "synthetic-owner-042",
  repository: "synthetic-repo-042",
  destination_alias: "a7f4k2m9",
  permitted_event: "push",
  permitted_ref: "refs/heads/main",
  job_workflow_ref: "synthetic-owner-042/synthetic-repo-042/.github/workflows/architecture-health-badge.yml@refs/heads/main",
  job_workflow_sha: "2222222222222222222222222222222222222222",
  disclosure_profile: "headline-only/v1",
  audience: "architecture-health-badge-relay-fixture"
};

function claims(overrides: Partial<OidcClaims> = {}): OidcClaims {
  return {
    iss: "https://token.actions.githubusercontent.com",
    aud: entry.audience,
    sub: "repo:synthetic-owner-042/synthetic-repo-042:ref:refs/heads/main",
    repository_id: entry.repository_id,
    repository_owner_id: entry.repository_owner_id,
    event_name: "push",
    ref: "refs/heads/main",
    job_workflow_ref: entry.job_workflow_ref,
    job_workflow_sha: entry.job_workflow_sha,
    iat: 1_000,
    nbf: 1_000,
    exp: 1_300,
    jti: "fixture-jti",
    ...overrides
  };
}

describe("relay contract helpers", () => {
  it("recognizes only opaque aliases and complete registry entries", () => {
    expect(isOpaqueAlias(entry.destination_alias)).toBe(true);
    for (const invalid of ["a123", "A7f4k2m9", "b7f4k2m9", "a7f4k2m!", 7, null]) expect(isOpaqueAlias(invalid)).toBe(false);

    expect(validateRegistryEntry(entry)).toBe(true);
    for (const invalid of [
      null,
      [],
      { ...entry, repository_id: 0 },
      { ...entry, repository_owner_id: 1.5 },
      { ...entry, destination_alias: "alias" },
      { ...entry, permitted_event: "pull_request" },
      { ...entry, permitted_ref: "refs/heads/release" },
      { ...entry, job_workflow_ref: "" },
      { ...entry, job_workflow_sha: "bad" },
      { ...entry, disclosure_profile: "unbounded" },
      { ...entry, consent: false }
    ]) expect(validateRegistryEntry(invalid)).toBe(false);
  });

  it("loads valid registry entries from every supported config shape", () => {
    expect(registryEntriesFromConfig(undefined)).toEqual([]);
    expect(registryEntriesFromConfig("not-json")).toEqual([]);
    expect(registryEntriesFromConfig(entry)).toEqual([entry]);
    expect(registryEntriesFromConfig(JSON.stringify(entry))).toEqual([entry]);
    expect(registryEntriesFromConfig([entry, { ...entry, destination_alias: "bad" }])).toEqual([entry]);
    expect(registryEntriesFromConfig({ registry_entry: entry })).toEqual([entry]);
    expect(registryEntriesFromConfig({ entries: [entry] })).toEqual([entry]);
    expect(registryEntriesFromConfig({ named: entry, invalid: {} })).toEqual([entry]);
    expect(registryEntriesFromConfig({ entries: "not-an-array" })).toEqual([]);
  });

  it("enforces the fixed bundle and OIDC configuration", () => {
    expect(validateBundleConfig({})).toBe(true);
    expect(validateBundleConfig({ schema_id: "architecture-health-badge-relay-config/v1", mode: "relay", bundle: "badge-relay/v1", oidc_trust: {
      issuer: "https://token.actions.githubusercontent.com",
      jwks_uri: "https://token.actions.githubusercontent.com/.well-known/jwks",
      audience: entry.audience,
      allowed_algorithms: ["RS256"]
    } })).toBe(true);
    for (const invalid of [null, [], { schema_id: "other" }, { mode: "other" }, { bundle: "other" }, { oidc_trust: {} }, { oidc_trust: { issuer: "https://issuer.invalid", jwks_uri: "https://token.actions.githubusercontent.com/.well-known/jwks", audience: "a" } }, { oidc_trust: { issuer: "https://token.actions.githubusercontent.com", jwks_uri: "https://token.actions.githubusercontent.com/.well-known/jwks", audience: "", allowed_algorithms: ["HS256"] } }]) {
      expect(validateBundleConfig(invalid)).toBe(false);
    }
    expect(profileFromEntry(entry)).toBe("headline-only/v1");
  });

  it("accepts only canonical closed payloads", () => {
    const headline = canonicalizePayload({ schemaVersion: 1, label: "architecture", message: "PASS · HEALTHY · 0 ignores · 42 rules", color: "brightgreen" }, "headline-only/v1");
    expect(validateCanonicalPayload(headline, "headline-only/v1").message).toContain("HEALTHY");
    const freshness = canonicalizePayload({ schemaVersion: 1, label: "architecture", message: "FAIL · DEBT · 1 ignores · 2 rules", color: "yellow", verified_at: "2026-09-11T12:00:00Z", valid_until: "2026-09-11T12:30:00Z" }, "headline-plus-freshness/v1");
    expect(validateCanonicalPayload(freshness, "headline-plus-freshness/v1").valid_until).toBe("2026-09-11T12:30:00Z");
    expect(canonicalizePayload({ schemaVersion: 1, label: "architecture", message: "π", color: "brightgreen" }, "headline-only/v1")).toContain("\\u03C0");

    for (const invalid of [
      "not-json",
      "[]",
      JSON.stringify({ label: "architecture" }),
      JSON.stringify({ schemaVersion: 2, label: "architecture", message: "PASS · HEALTHY · 0 ignores · 42 rules", color: "brightgreen" }),
      JSON.stringify({ schemaVersion: 1, label: "other", message: "PASS · HEALTHY · 0 ignores · 42 rules", color: "brightgreen" }),
      JSON.stringify({ schemaVersion: 1, label: "architecture", message: "bad", color: "brightgreen" }),
      JSON.stringify({ schemaVersion: 1, label: "architecture", message: "PASS · HEALTHY · 0 ignores · 42 rules", color: "red" }),
      headline.replace("schemaVersion", "zchemaVersion"),
      headline + " ",
      "x".repeat(17 * 1024)
    ]) expect(() => validateCanonicalPayload(invalid, "headline-only/v1")).toThrow(PayloadError);
    expect(() => canonicalizePayload({ schemaVersion: 1, label: "architecture", message: "PASS · HEALTHY · 0 ignores · 42 rules", color: "brightgreen" }, "headline-plus-freshness/v1")).toThrow(PayloadError);
    expect(() => validateCanonicalPayload(freshness.replace("12:30", "11:30"), "headline-plus-freshness/v1")).toThrow(PayloadError);
  });

  it("validates bearer and OIDC claims against immutable identity", async () => {
    expect(getBearerToken(new Request("https://relay.test", { headers: { authorization: "Bearer token" } }))).toBe("token");
    for (const header of [undefined, "Basic token", "Bearer ", `Bearer ${"x".repeat(17 * 1024)}`]) {
      const request = new Request("https://relay.test", { headers: header ? { authorization: header } : {} });
      expect(() => getBearerToken(request)).toThrow(AuthorizationError);
    }

    validateOidcClaims(claims(), entry, 1_100);
    validateOidcClaims(claims({ aud: ["another", entry.audience] }), entry, 1_100);
    for (const invalid of [
      { iss: "https://issuer.invalid" }, { aud: "other" }, { iat: "1000" }, { iat: 1_500 }, { nbf: 1_500 }, { exp: 900 }, { exp: 2_000 }, { jti: "" },
      { repository_id: 1 }, { repository_owner_id: 1 }, { event_name: "workflow_dispatch" }, { ref: "refs/heads/dev" }, { job_workflow_ref: "other" }, { job_workflow_sha: "other" }, { sub: "other" }
    ]) expect(() => validateOidcClaims(claims(invalid), entry, 1_100)).toThrow(AuthorizationError);
    expect(() => validateOidcClaims(claims(), entry, 1_100, { issuer: "https://issuer.invalid", jwks_uri: "https://token.actions.githubusercontent.com/.well-known/jwks", audience: "architecture-health-badge-relay-fixture" })).toThrow(AuthorizationError);
    expect(isTrustedContext({ valid: true, kind: "github-pr-authoritative/v1" })).toBe(true);
    expect(isTrustedContext({ valid: true, kind: "other" })).toBe(false);
    expect(await sha256Hex("relay")).toHaveLength(64);
  });

  it("fails closed for malformed or unavailable public routes", async () => {
    const unavailable = {} as RelayEnvironment;
    expect((await worker.fetch(new Request("https://relay.test/not-a-route"), unavailable)).status).toBe(404);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9.json"), unavailable)).status).toBe(503);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9.svg"), unavailable)).status).toBe(503);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9/svg"), unavailable)).status).toBe(503);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9/json"), unavailable)).status).toBe(503);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9x.svg"), unavailable)).status).toBe(404);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9.txt"), unavailable)).status).toBe(404);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/admin/unknown"), unavailable)).status).toBe(404);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/a7f4k2m9/prepare", { method: "POST" }), unavailable)).status).toBe(503);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/admin/register", { method: "POST" }), unavailable)).status).toBe(401);
    const authorized = { ...unavailable, ADMIN_TOKEN: "admin" };
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/admin/register", { method: "POST", headers: { authorization: "Bearer admin" }, body: "not-json" }), authorized)).status).toBe(413);
    expect((await worker.fetch(new Request("https://relay.test/badge-relay/v1/admin/revoke/not-an-alias", { method: "POST", headers: { authorization: "Bearer admin" } }), authorized)).status).toBe(404);
  });
});
