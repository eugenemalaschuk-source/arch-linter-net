import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
import { decodeJwt, generateKeyPair, SignJWT, exportJWK } from "jose";
import { env, SELF, evictDurableObject, listDurableObjectIds, runInDurableObject } from "cloudflare:test";
import worker, {
  RelayDurableObject,
  REGISTRY_OBJECT_NAME,
  RelayRegistryDurableObject,
  canonicalPayloadDigest,
  canonicalizePayload,
  clearJwksCacheForTests,
  sha256Hex,
  type RegistryEntry
} from "../src/index";
import type { RelayEnvironment } from "../src/index";
import type { DurableObjectNamespace } from "@cloudflare/workers-types";

const alias = "a7f4k2m9";
const digestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const digestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
const digestC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
const entry: RegistryEntry = {
  repository_id: 700000042,
  repository_owner_id: 7000000042,
  owner: "synthetic-owner-042",
  repository: "synthetic-repo-042",
  destination_alias: alias,
  permitted_event: "push",
  permitted_ref: "refs/heads/main",
  job_workflow_ref: "synthetic-owner-042/synthetic-repo-042/.github/workflows/architecture-health-badge.yml@refs/heads/main",
  job_workflow_sha: "2222222222222222222222222222222222222222",
  disclosure_profile: "headline-only/v1",
  audience: "architecture-health-badge-relay-fixture"
};

const payload = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "PASS · HEALTHY · 0 ignores · 42 rules",
  color: "brightgreen"
}, "headline-only/v1");
const alternatePayload = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "PASS · HEALTHY · 1 ignores · 42 rules",
  color: "brightgreen"
}, "headline-only/v1");
const freshnessAlias = "a7f4k2n9";
const freshnessEntry: RegistryEntry = {
  ...entry,
  destination_alias: freshnessAlias,
  disclosure_profile: "headline-plus-freshness/v1"
};
const freshnessPayload = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "PASS · HEALTHY · 0 ignores · 42 rules",
  color: "brightgreen",
  verified_at: "2026-09-12T10:00:00Z",
  valid_until: "2026-09-12T10:30:00Z"
}, "headline-plus-freshness/v1");
const futureAlias = "a7f4k2p9";
const futureEntry: RegistryEntry = {
  ...freshnessEntry,
  destination_alias: futureAlias
};
const futurePayload = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "PASS · HEALTHY · 0 ignores · 42 rules",
  color: "brightgreen",
  verified_at: "2026-09-12T10:10:00Z",
  valid_until: "2026-09-12T10:30:00Z"
}, "headline-plus-freshness/v1");
const overLeasePayload = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "PASS · HEALTHY · 0 ignores · 42 rules",
  color: "brightgreen",
  verified_at: "2026-09-12T10:00:00Z",
  valid_until: "2026-09-12T11:30:00Z"
}, "headline-plus-freshness/v1");

describe("badge-relay/v1 local SQLite Durable Object", () => {
  let privateKey: CryptoKey;
  let publicJwk: Record<string, unknown>;

  afterEach(() => vi.useRealTimers());

  beforeEach(async () => {
    clearJwksCacheForTests();
    const keys = await generateKeyPair("RS256");
    privateKey = keys.privateKey;
    publicJwk = await exportJWK(keys.publicKey);
    publicJwk.kid = "local-key";
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      expect(String(input)).toBe("https://token.actions.githubusercontent.com/.well-known/jwks");
      return new Response(JSON.stringify({ keys: [publicJwk] }), { headers: { "content-type": "application/json" } });
    }));
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    await runInDurableObject(relay.get(relay.idFromName(alias)), async (_instance, state) => {
      state.storage.sql.exec("DELETE FROM relay_challenges");
      state.storage.sql.exec("DELETE FROM relay_replay_keys");
      state.storage.sql.exec("UPDATE relay_state SET status='unavailable', generation=1, revocation_epoch=1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=0, last_renewed_at=NULL WHERE id=1");
    });
  });

  async function token(overrides: Record<string, unknown> = {}, kid = "local-key"): Promise<string> {
    const now = Math.floor(Date.now() / 1000);
    return new SignJWT({
      iss: "https://token.actions.githubusercontent.com",
      aud: entry.audience,
      sub: "repo:synthetic-owner-042/synthetic-repo-042:ref:refs/heads/main",
      repository_id: entry.repository_id,
      repository_owner_id: entry.repository_owner_id,
      event_name: "push",
      ref: "refs/heads/main",
      job_workflow_ref: entry.job_workflow_ref,
      job_workflow_sha: entry.job_workflow_sha,
      iat: now,
      nbf: now,
      exp: now + 300,
      jti: crypto.randomUUID(),
      ...overrides
    }).setProtectedHeader({ alg: "RS256", kid }).sign(privateKey);
  }

  function relayUrl(operation: string): string { return `https://relay.test/badge-relay/v1/${alias}/${operation}`; }

  function futureHorizon(): string {
    return new Date(Math.floor((Date.now() + 30 * 60_000) / 1000) * 1000).toISOString().replace(".000Z", "Z");
  }

  async function prepareRemote(jwt: string, idempotencyKey: string, bytes = payload): Promise<{ response: Response; challenge?: { challenge_id: string; generation: number; revocation_epoch: number; deadline: string } }> {
    const digest = await canonicalPayloadDigest(bytes);
    const response = await SELF.fetch(relayUrl("prepare"), {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: bytes, canonical_digest: digest, profile: entry.disclosure_profile, idempotency_key: idempotencyKey, semantic_horizon: futureHorizon() })
    });
    if (!response.ok) return { response };
    return { response, challenge: await response.clone().json() as { challenge_id: string; generation: number; revocation_epoch: number; deadline: string } };
  }

  async function commitInternal(stub: DurableObjectStub, jwt: string, challenge: { challenge_id: string; generation: number; revocation_epoch: number }, operation: "publish" | "renew" | "recover" = "publish", bytes = payload, idempotencyKey = ""): Promise<Response> {
    const digest = await canonicalPayloadDigest(bytes);
    return runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).commitTrustedPublication({
      body: { operation, challenge_id: challenge.challenge_id, idempotency_key: idempotencyKey || `commit-${challenge.challenge_id}`, canonical_bytes: bytes, canonical_digest: digest, profile: entry.disclosure_profile, expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, semantic_horizon: futureHorizon() },
      entry,
      jtiHash: await sha256Hex((decodeJwt(jwt).jti as string)),
      proof: { valid: true, kind: "github-pr-authoritative/v1", digest }
    }));
  }

  it("fails closed for a required JWKS refresh without creating publication state", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("unavailable", { status: 503 })));
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(alias));
    const prepared = await prepareRemote(await token({}, "provider-outage"), `provider-outage-${crypto.randomUUID()}`);

    expect(prepared.response.status).toBe(503);
    const state = await runInDurableObject(stub, async (_instance, durableState) => ({
      challengeCount: durableState.storage.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM relay_challenges").toArray()[0]?.count,
      relayState: durableState.storage.sql.exec<{ status: string; payload: string | null }>("SELECT status, payload FROM relay_state WHERE id=1").toArray()[0]
    }));
    expect(state.challengeCount).toBe(0);
    expect(state.relayState).toMatchObject({ status: "unavailable", payload: null });
  });

  it("coalesces unknown-key refreshes across concurrent SELF.fetch requests", async () => {
    const pendingFetches: Array<(response: Response) => void> = [];
    let signalFetchStarted!: () => void;
    const fetchStarted = new Promise<void>((resolve) => { signalFetchStarted = resolve; });
    const fetcher = vi.fn(() => {
      signalFetchStarted();
      return new Promise<Response>((resolve) => pendingFetches.push(resolve));
    });
    vi.stubGlobal("fetch", fetcher);
    const tokens = await Promise.all(Array.from({ length: 32 }, (_, index) => token({}, `cross-request-invalid-${index}`)));
    const requests = tokens.map((jwt, index) => prepareRemote(jwt, `cross-request-${index}-${crypto.randomUUID()}`));

    try {
      await Promise.race([
        fetchStarted,
        new Promise<never>((_, reject) => setTimeout(() => reject(new Error("JWKS fetch did not start")), 2_000))
      ]);
      await new Promise((resolve) => setTimeout(resolve, 50));
      expect(fetcher).toHaveBeenCalledTimes(1);
    } finally {
      for (const release of pendingFetches) release(new Response(JSON.stringify({ keys: [publicJwk] }), { headers: { "content-type": "application/json" } }));
    }

    const results = await Promise.all(requests);
    expect(results.every(({ response }) => response.status === 401)).toBe(true);
  });

  it("rejects an unknown alias before Durable Object allocation", async () => {
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const before = (await listDurableObjectIds(relay)).length;
    const response = await SELF.fetch("https://relay.test/badge-relay/v1/a0000000");
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ error: "unknown_route" });
    expect(await listDurableObjectIds(relay)).toHaveLength(before);
  });

  it("expires a ready public GET before conditional handling without a publisher or scheduler", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:00:00Z"));
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(alias));
    const prepared = await prepareRemote(await token(), `public-read-${crypto.randomUUID()}`);
    expect(prepared.response.status).toBe(201);
    const digest = await canonicalPayloadDigest(payload);
    await runInDurableObject(stub, async (_instance, state) => {
      state.storage.sql.exec(
        "UPDATE relay_state SET status='ready', generation=7, payload=?, payload_digest=?, verified_at=?, valid_until=?, semantic_horizon=?, tombstoned=0 WHERE id=1",
        payload,
        digest,
        "2026-09-12T10:00:00Z",
        "2026-09-12T10:01:00Z",
        "2026-09-12T10:01:00Z");
    });
    const ready = await SELF.fetch(`https://relay.test/badge-relay/v1/${alias}`);
    const etag = ready.headers.get("etag") as string;
    expect(ready.status).toBe(200);
    expect(await ready.text()).toBe(payload);

    vi.setSystemTime(new Date("2026-09-12T10:01:00Z"));
    const expired = await SELF.fetch(`https://relay.test/badge-relay/v1/${alias}`, {
      headers: { "if-none-match": etag }
    });
    expect(expired.status).toBe(404);
    expect(await expired.text()).toContain("UNASSESSABLE");
    expect(expired.headers.get("etag")).toBeNull();
    expect(expired.headers.get("cache-control")).toBe("no-store");
  });

  it("persists private registry entries and tombstones across invocation eviction", async () => {
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const stub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    const registeredEntry: RegistryEntry = { ...entry, destination_alias: "a9f4k2m8", consent: true };
    const registered = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(registeredEntry));
    expect(registered).toBe(true);
    await evictDurableObject(stub);
    const lookedUp = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayRegistryDurableObject).lookup(registeredEntry.destination_alias));
    expect(lookedUp?.repository_id).toBe(registeredEntry.repository_id);
    const revoked = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayRegistryDurableObject).revokeAlias(registeredEntry.destination_alias));
    expect(revoked).toBe(true);
    await evictDurableObject(stub);
    const hidden = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayRegistryDurableObject).lookup(registeredEntry.destination_alias));
    expect(hidden).toBeUndefined();
    const reassigned = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(registeredEntry));
    expect(reassigned).toBe(false);
  });

  it("fails closed for admin registration without an injected secret", async () => {
    const response = await SELF.fetch("https://relay.test/badge-relay/v1/admin/register", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ registry_entry: { ...entry, destination_alias: "a9f4k2m7", consent: true } })
    });
    expect(response.status).toBe(401);
    expect(await response.text()).not.toContain("synthetic-owner");
  });

  it("executes authenticated lifecycle transitions with redacted status and tombstone precedence", async () => {
    const lifecycleAlias = "a833test";
    const lifecycleEntry: RegistryEntry = { ...entry, destination_alias: lifecycleAlias, bundle_digest: digestA, consent: true };
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(lifecycleEntry))).toBe(true);
    const testEnv = { ...(env as unknown as Record<string, unknown>), ADMIN_TOKEN: "admin" } as unknown as RelayEnvironment;
    const admin = (path: string, init: RequestInit = {}) => worker.fetch(
      new Request(`https://relay.test/badge-relay/v1/admin/${lifecycleAlias}/${path}`, {
        ...init,
        headers: { authorization: "Bearer admin", "content-type": "application/json", ...(init.headers ?? {}) },
      }),
      testEnv);

    const status = await admin("status", { method: "GET" });
    expect(status.status).toBe(200);
    const initialStatus = await status.json() as Record<string, unknown>;
    expect(initialStatus).toMatchObject({ state: "unavailable", tombstoned: false });
    expect(initialStatus).not.toHaveProperty("payload");

    const rename = await admin("reconcile-identity", {
      method: "POST",
      body: JSON.stringify({ operation_id: "rename-833", owner: "renamed-owner", repository: "renamed-repo", repository_id: entry.repository_id, repository_owner_id: entry.repository_owner_id }),
    });
    expect(rename.status).toBe(200);
    const renamedStatus = await admin("status", { method: "GET" });
    expect(await renamedStatus.json()).toMatchObject({ state: "unavailable", generation: initialStatus.generation, revocation_epoch: initialStatus.revocation_epoch, display_owner: "renamed-owner", display_repository: "renamed-repo" });

    const invalidRotation = await admin("rotate", {
      method: "POST",
      body: JSON.stringify({ operation_id: "rotate-bad-833", job_workflow_sha: "not-a-pin" }),
    });
    expect(invalidRotation.status).toBe(409);
    expect(await invalidRotation.json()).toEqual({ error: "invalid_pin" });

    const rotate = await admin("rotate", {
      method: "POST",
      body: JSON.stringify({ operation_id: "rotate-833", job_workflow_sha: "3333333333333333333333333333333333333333", audience: "rotated-audience" }),
    });
    expect(rotate.status).toBe(200);

    const upgrade = await admin("upgrade", {
      method: "POST",
      body: JSON.stringify({ operation_id: "upgrade-stage-833", bundle: "badge-relay/v1", contract_version: "v1", compatibility_plan: "architecture-health-badge-relay/v1", bundle_digest: digestB }),
    });
    expect(upgrade.status).toBe(200);
    expect(await upgrade.json()).toMatchObject({ active_digest: digestA, staged_digest: digestB });
    const activate = await admin("upgrade/activate", {
      method: "POST",
      body: JSON.stringify({ operation_id: "upgrade-activate-833", bundle: "badge-relay/v1", contract_version: "v1", compatibility_plan: "architecture-health-badge-relay/v1", bundle_digest: digestB }),
    });
    expect(activate.status).toBe(200);
    expect(await activate.json()).toMatchObject({ active_digest: digestB, previous_verified_digest: digestA });
    const unknown = await admin("upgrade", {
      method: "POST",
      body: JSON.stringify({ operation_id: "upgrade-unknown-833", bundle: "badge-relay/v1", contract_version: "v1", compatibility_plan: "architecture-health-badge-relay/v1", bundle_digest: digestC }),
    });
    expect(unknown.status).toBe(409);
    expect(await (await admin("status", { method: "GET" })).json()).toMatchObject({ active_digest: digestB, staged_digest: null, previous_verified_digest: digestA });
    const rollback = await admin("rollback", {
      method: "POST",
      body: JSON.stringify({ operation_id: "rollback-833", bundle: "badge-relay/v1", contract_version: "v1", compatibility_plan: "architecture-health-badge-relay/v1", bundle_digest: digestA, to_bundle: "badge-relay/v1" }),
    });
    expect(rollback.status).toBe(200);

    const missingOperationId = await admin("revoke", { method: "POST", body: JSON.stringify({ confirm: true }) });
    expect(missingOperationId.status).toBe(413);
    const beforeRevokeResponse = await admin("status", { method: "GET" });
    expect(beforeRevokeResponse.status).toBe(200);
    const beforeRevoke = await beforeRevokeResponse.json() as Record<string, number>;
    const staleRegistryRevoke = await admin("revoke", {
      method: "POST",
      body: JSON.stringify({
        confirm: true,
        operation_id: "revoke-stale-registry-833",
        expected_generation: beforeRevoke.generation,
        expected_revocation_epoch: beforeRevoke.revocation_epoch,
        expected_registry_revision: beforeRevoke.registry_revision + 1,
        expected_barrier_epoch: beforeRevoke.barrier_epoch
      })
    });
    expect(staleRegistryRevoke.status).toBe(409);
    const staleRevoke = await admin("revoke", {
      method: "POST",
      body: JSON.stringify({
        confirm: true,
        operation_id: "revoke-stale-833",
        expected_generation: beforeRevoke.generation - 1,
        expected_revocation_epoch: beforeRevoke.revocation_epoch - 1,
        expected_registry_revision: beforeRevoke.registry_revision,
        expected_barrier_epoch: beforeRevoke.barrier_epoch
      })
    });
    expect(staleRevoke.status).toBe(409);
    expect(await (await admin("status", { method: "GET" })).json()).toMatchObject({ state: "unavailable", tombstoned: false, generation: beforeRevoke.generation, revocation_epoch: beforeRevoke.revocation_epoch });

    const revoke = await admin("revoke", {
      method: "POST",
      body: JSON.stringify({
        confirm: true,
        operation_id: "revoke-833",
        expected_generation: beforeRevoke.generation,
        expected_revocation_epoch: beforeRevoke.revocation_epoch,
        expected_registry_revision: beforeRevoke.registry_revision,
        expected_barrier_epoch: beforeRevoke.barrier_epoch
      })
    });
    expect(revoke.status).toBe(200);
    expect((await SELF.fetch(`https://relay.test/badge-relay/v1/${lifecycleAlias}`)).status).toBe(404);

    const revokedStatus = await admin("status", { method: "GET" });
    expect(revokedStatus.status).toBe(200);
    expect(await revokedStatus.json()).toMatchObject({ state: "revoked", tombstoned: true, active_digest: digestA, previous_verified_digest: digestB });
  });

  it("rejects a stale caller without mutating newer ready state", async () => {
    const staleAlias = "a833stle";
    const staleEntry: RegistryEntry = { ...entry, destination_alias: staleAlias };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(staleAlias));
    const headers = { "x-relay-registry": JSON.stringify(staleEntry), "x-relay-registry-revision": "2", "x-relay-barrier-epoch": "1" };
    await runInDurableObject(stub, async (_instance, state) => {
      state.storage.sql.exec("DELETE FROM relay_state");
      state.storage.sql.exec("INSERT INTO relay_state (id,status,profile,generation,revocation_epoch,payload,payload_digest,verified_at,valid_until,semantic_horizon,tombstoned,registry_revision,barrier_epoch,bundle,contract_version,compatibility_plan,updated_at) VALUES (1,'ready','headline-only/v1',7,3,?,NULL,NULL,NULL,NULL,0,2,1,'badge-relay/v1','v1','architecture-health-badge-relay/v1',?)", payload, Math.floor(Date.now() / 1000));
    });
    const response = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/${staleAlias}/read/json`, { headers: { ...headers, "x-relay-registry-revision": "1" } })));
    expect(response.status).toBe(409);
    const state = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; registry_revision: number }>("SELECT status,generation,registry_revision FROM relay_state WHERE id=1").toArray()[0]);
    expect(state).toEqual({ status: "ready", generation: 7, registry_revision: 2 });
  });

  it("rejects a stale revoke before the Registry tombstone when a publisher wins first", async () => {
    const staleAlias = "a833rce1";
    const staleEntry: RegistryEntry = { ...entry, destination_alias: staleAlias, consent: true };
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(staleEntry))).toBe(true);
    const jwt = await token({ jti: "stale-revoke-publisher-jti" });
    const digest = await canonicalPayloadDigest(payload);
    const prepared = await SELF.fetch(`https://relay.test/badge-relay/v1/${staleAlias}/prepare`, {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: payload, canonical_digest: digest, profile: staleEntry.disclosure_profile, idempotency_key: "stale-revoke-publisher-key", semantic_horizon: futureHorizon() })
    });
    expect(prepared.status).toBe(201);
    const challenge = await prepared.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(staleAlias));
    const advanced = await commitInternal(stub, jwt, challenge, "publish", payload, "stale-revoke-publisher-key");
    expect(advanced.status).toBe(200);

    const testEnv = { ...(env as unknown as Record<string, unknown>), ADMIN_TOKEN: "admin" } as unknown as RelayEnvironment;
    const revoked = await worker.fetch(new Request(`https://relay.test/badge-relay/v1/admin/${staleAlias}/revoke`, {
      method: "POST",
      headers: { authorization: "Bearer admin", "content-type": "application/json" },
      body: JSON.stringify({ confirm: true, operation_id: "stale-revoke-before-registry", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, expected_registry_revision: 1, expected_barrier_epoch: 1 })
    }), testEnv);
    expect(revoked.status).toBe(409);
    const binding = await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).binding(staleAlias, true));
    expect(binding?.tombstoned).toBe(false);
    const row = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; tombstoned: number }>("SELECT status,generation,tombstoned FROM relay_state WHERE id=1").toArray()[0]);
    expect(row).toMatchObject({ status: "ready", generation: challenge.generation + 1, tombstoned: 0 });
  });

  it("fences a delayed publisher while revoke waits for the Registry CAS", async () => {
    const raceAlias = "a833rce2";
    const raceEntry: RegistryEntry = { ...entry, destination_alias: raceAlias, consent: true };
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(raceEntry))).toBe(true);
    const jwt = await token({ jti: "fenced-revoke-publisher-jti" });
    const digest = await canonicalPayloadDigest(payload);
    const prepared = await SELF.fetch(`https://relay.test/badge-relay/v1/${raceAlias}/prepare`, {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: payload, canonical_digest: digest, profile: raceEntry.disclosure_profile, idempotency_key: "fenced-revoke-publisher-key", semantic_horizon: futureHorizon() })
    });
    expect(prepared.status).toBe(201);
    const challenge = await prepared.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(raceAlias));
    const reservation = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/admin/${raceAlias}/revoke-prepare`, {
      method: "POST",
      headers: { "content-type": "application/json", "x-relay-admin": "1", "x-relay-registry": JSON.stringify(raceEntry), "x-relay-registry-revision": "1", "x-relay-barrier-epoch": "1", "x-relay-registry-tombstoned": "false" },
      body: JSON.stringify({ operation_id: "fenced-revoke-operation", lifecycle_operation: "revoke", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, expected_registry_revision: 1, expected_barrier_epoch: 1 })
    })));
    expect(reservation.status).toBe(200);
    expect(await reservation.json()).toMatchObject({ pending: true, generation: challenge.generation, revocation_epoch: challenge.revocation_epoch });

    const stale = await commitInternal(stub, jwt, challenge, "publish", payload, "fenced-revoke-publisher-key");
    expect(stale.status).toBe(409);
    const beforeRegistry = await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).binding(raceAlias, true));
    expect(beforeRegistry?.tombstoned).toBe(false);

    const testEnv = { ...(env as unknown as Record<string, unknown>), ADMIN_TOKEN: "admin" } as unknown as RelayEnvironment;
    const renamed = await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).reconcileIdentity(raceAlias, "fenced-owner", "fenced-repo", raceEntry.repository_id, raceEntry.repository_owner_id, 1, 1));
    expect(renamed?.revision).toBe(2);
    const staleRegistryAttempt = await worker.fetch(new Request(`https://relay.test/badge-relay/v1/admin/${raceAlias}/revoke`, {
      method: "POST",
      headers: { authorization: "Bearer admin", "content-type": "application/json" },
      body: JSON.stringify({ confirm: true, operation_id: "fenced-revoke-operation", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, expected_registry_revision: 1, expected_barrier_epoch: 1 })
    }), testEnv);
    expect(staleRegistryAttempt.status).toBe(409);
    const stillPending = await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).binding(raceAlias, true));
    expect(stillPending?.tombstoned).toBe(false);

    const finalized = await worker.fetch(new Request(`https://relay.test/badge-relay/v1/admin/${raceAlias}/revoke`, {
      method: "POST",
      headers: { authorization: "Bearer admin", "content-type": "application/json" },
      body: JSON.stringify({ confirm: true, operation_id: "fenced-revoke-operation", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, expected_registry_revision: 2, expected_barrier_epoch: 1 })
    }), testEnv);
    expect(finalized.status).toBe(200);
    const replay = await worker.fetch(new Request(`https://relay.test/badge-relay/v1/admin/${raceAlias}/revoke`, {
      method: "POST",
      headers: { authorization: "Bearer admin", "content-type": "application/json" },
      body: JSON.stringify({ confirm: true, operation_id: "fenced-revoke-operation", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, expected_registry_revision: 3, expected_barrier_epoch: 2 })
    }), testEnv);
    expect(replay.status).toBe(200);
    const afterRegistry = await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).binding(raceAlias, true));
    expect(afterRegistry?.tombstoned).toBe(true);
    const row = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; revocation_epoch: number; tombstoned: number; pending_operation_id: string | null }>("SELECT status,generation,revocation_epoch,tombstoned,pending_operation_id FROM relay_state WHERE id=1").toArray()[0]);
    expect(row).toMatchObject({ status: "revoked", generation: challenge.generation + 1, revocation_epoch: challenge.revocation_epoch + 1, tombstoned: 1, pending_operation_id: null });
  });

  it("honors caller registry CAS values and requires operation IDs for admin mutations", async () => {
    const casAlias = "a833cas1";
    const casEntry: RegistryEntry = { ...entry, destination_alias: casAlias, consent: true };
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(casEntry))).toBe(true);
    const testEnv = { ...(env as unknown as Record<string, unknown>), ADMIN_TOKEN: "admin" } as unknown as RelayEnvironment;
    const admin = (path: string, init: RequestInit = {}) => worker.fetch(
      new Request(`https://relay.test/badge-relay/v1/admin/${casAlias}/${path}`, {
        ...init,
        headers: { authorization: "Bearer admin", "content-type": "application/json", ...(init.headers ?? {}) },
      }),
      testEnv);

    const initial = await admin("status", { method: "GET" });
    expect(initial.status).toBe(200);
    const stateBefore = await initial.json() as Record<string, number>;
    const staleRevision = await admin("invalidate", {
      method: "POST",
      body: JSON.stringify({ operation_id: "cas-stale-833", expected_registry_revision: stateBefore.registry_revision + 1, expected_barrier_epoch: stateBefore.barrier_epoch })
    });
    expect(staleRevision.status).toBe(409);
    expect(await (await admin("status", { method: "GET" })).json()).toMatchObject({ state: "unavailable", generation: stateBefore.generation, revocation_epoch: stateBefore.revocation_epoch });

    for (const operation of ["invalidate", "recover/open", "upgrade", "rotate"]) {
      const missingOperationId = await admin(operation, {
        method: "POST",
        body: JSON.stringify({ confirm: true, bundle: "badge-relay/v1", contract_version: "v1", compatibility_plan: "architecture-health-badge-relay/v1" })
      });
      expect(missingOperationId.status).toBe(413);
    }
    expect(await (await admin("status", { method: "GET" })).json()).toMatchObject({ state: "unavailable", generation: stateBefore.generation, revocation_epoch: stateBefore.revocation_epoch });
  });

  it("bounds the private operation journal by age and count", async () => {
    const journalAlias = "a833jrn1";
    const journalEntry: RegistryEntry = { ...entry, destination_alias: journalAlias, consent: true };
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(journalEntry))).toBe(true);
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(journalAlias));
    const internalHeaders = { "x-relay-registry": JSON.stringify(journalEntry), "x-relay-registry-revision": "1", "x-relay-barrier-epoch": "1", "x-relay-admin": "1", "content-type": "application/json" };
    const status = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/admin/${journalAlias}/status`, { headers: internalHeaders })));
    expect(status.status).toBe(200);
    const now = Math.floor(Date.now() / 1000);
    await runInDurableObject(stub, async (_instance, state) => {
      for (let index = 0; index < 300; index++) state.storage.sql.exec("INSERT INTO relay_operations (operation,status,reason,operation_id,generation,revocation_epoch,created_at) VALUES (?,?,?,?,?,?,?)", "synthetic", "unavailable", "storage_unavailable", `synthetic-${index}`, 1, 1, now);
      state.storage.sql.exec("INSERT INTO relay_operations (operation,status,reason,operation_id,generation,revocation_epoch,created_at) VALUES (?,?,?,?,?,?,?)", "old", "unavailable", "storage_unavailable", "old-operation", 1, 1, now - 31 * 24 * 60 * 60);
    });
    const sync = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/admin/${journalAlias}/sync`, { method: "POST", headers: internalHeaders, body: JSON.stringify({ registry_revision: 1, barrier_epoch: 1 }) })));
    expect(sync.status).toBe(200);
    const count = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM relay_operations").toArray()[0].count);
    expect(count).toBeLessThanOrEqual(256);
    const oldCount = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM relay_operations WHERE operation='old'").toArray()[0].count);
    expect(oldCount).toBe(0);
  });

  it("retains a tombstone barrier after the 90-day diagnostic window", async () => {
    const tombstoneAlias = "a833tmb1";
    const tombstoneEntry: RegistryEntry = { ...entry, destination_alias: tombstoneAlias, bundle_digest: digestA, consent: true };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(tombstoneAlias));
    const internalHeaders = {
      "x-relay-registry": JSON.stringify(tombstoneEntry),
      "x-relay-registry-revision": "4",
      "x-relay-barrier-epoch": "3",
      "x-relay-admin": "1",
      "content-type": "application/json"
    };
    const initialize = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/admin/${tombstoneAlias}/status`, { headers: internalHeaders })));
    expect(initialize.status).toBe(200);

    const oldTimestamp = Math.floor(Date.now() / 1000) - 91 * 24 * 60 * 60;
    await runInDurableObject(stub, async (_instance, state) => {
      state.storage.sql.exec("UPDATE relay_state SET status='revoked', generation=9, revocation_epoch=7, tombstoned=1, registry_revision=4, barrier_epoch=3, updated_at=? WHERE id=1", oldTimestamp);
    });

    // Status invokes pruneRetention(), and ensureRegistered() runs before it.
    // An expired diagnostic timestamp must not delete the security barrier.
    const status = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/admin/${tombstoneAlias}/status`, { headers: internalHeaders })));
    expect(status.status).toBe(200);
    expect(await status.json()).toMatchObject({ state: "revoked", generation: 9, revocation_epoch: 7, registry_revision: 4, barrier_epoch: 3, tombstoned: true });
    const persisted = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; revocation_epoch: number; tombstoned: number }>("SELECT status,generation,revocation_epoch,tombstoned FROM relay_state WHERE id=1").toArray()[0]);
    expect(persisted).toEqual({ status: "revoked", generation: 9, revocation_epoch: 7, tombstoned: 1 });
  });

  it("uses SQLite persistence across object eviction", async () => {
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(alias));
    const generation = await runInDurableObject(stub, async (_instance, state) => {
      const sql = state.storage.sql;
      sql.exec("CREATE TABLE IF NOT EXISTS integration_probe (value INTEGER)");
      sql.exec("DELETE FROM integration_probe");
      sql.exec("INSERT INTO integration_probe (value) VALUES (42)");
      return sql.exec<{ value: number }>("SELECT value FROM integration_probe").toArray()[0].value;
    });
    expect(generation).toBe(42);
  });

  it("authenticates, prepares, and atomically publishes a canonical payload", async () => {
    const jwt = await token();
    const digest = await canonicalPayloadDigest(payload);
    const idempotency = `idem-${crypto.randomUUID()}`;
    const prepare = await SELF.fetch(relayUrl("prepare"), {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: payload, canonical_digest: digest, profile: entry.disclosure_profile, idempotency_key: idempotency, semantic_horizon: new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z") })
    });
    expect(prepare.status).toBe(201);
    const challenge = await prepare.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const publish = await SELF.fetch(relayUrl("publish"), {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "publish", challenge_id: challenge.challenge_id, idempotency_key: idempotency, canonical_bytes: payload, canonical_digest: digest, profile: entry.disclosure_profile, expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, semantic_horizon: new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z") })
    });
    expect(publish.status).toBe(200);
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(alias));
    const spoofedJwt = await token({ jti: `spoofed-${crypto.randomUUID()}` });
    const spoofedIdempotency = `spoofed-${crypto.randomUUID()}`;
    const spoofedPrepare = await SELF.fetch(relayUrl("prepare"), {
      method: "POST",
      headers: { authorization: `Bearer ${spoofedJwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: payload, canonical_digest: digest, profile: entry.disclosure_profile, idempotency_key: spoofedIdempotency, semantic_horizon: new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z") })
    });
    expect(spoofedPrepare.status).toBe(201);
    const spoofedChallenge = await spoofedPrepare.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const spoofedPublish = await SELF.fetch(relayUrl("publish"), {
      method: "POST",
      headers: { authorization: `Bearer ${spoofedJwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "publish", challenge_id: spoofedChallenge.challenge_id, idempotency_key: spoofedIdempotency, canonical_bytes: payload, canonical_digest: digest, profile: entry.disclosure_profile, expected_generation: spoofedChallenge.generation, expected_revocation_epoch: spoofedChallenge.revocation_epoch, semantic_horizon: new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z"), trusted_context: { valid: true, kind: "github-pr-authoritative/v1", digest } })
    });
    expect(spoofedPublish.status).toBe(403);
    const stored = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; payload_digest: string }>("SELECT status, payload_digest FROM relay_state WHERE id=1").toArray()[0]);
    expect(stored.status).toBe("ready");
    expect(stored.payload_digest).toBe(digest);
  });

  it("preserves product-owned freshness timestamps through trusted publish and public SVG reads", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:02:00Z"));
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(freshnessEntry))).toBe(true);

    const jwt = await token({ jti: "freshness-e2e-jti" });
    const digest = await canonicalPayloadDigest(freshnessPayload);
    const horizon = "2026-09-12T10:30:00Z";
    const prepare = await SELF.fetch(`https://relay.test/badge-relay/v1/${freshnessAlias}/prepare`, {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: freshnessPayload, canonical_digest: digest, profile: freshnessEntry.disclosure_profile, idempotency_key: "freshness-e2e-key", semantic_horizon: horizon })
    });
    expect(prepare.status).toBe(201);
    const challenge = await prepare.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(freshnessAlias));
    const committed = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).commitTrustedPublication({
      body: { operation: "publish", challenge_id: challenge.challenge_id, idempotency_key: "freshness-e2e-key", canonical_bytes: freshnessPayload, canonical_digest: digest, profile: freshnessEntry.disclosure_profile, expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, semantic_horizon: horizon },
      entry: freshnessEntry,
      jtiHash: await sha256Hex(decodeJwt(jwt).jti as string),
      proof: { valid: true, kind: "github-pr-authoritative/v1", digest }
    }));
    expect(committed.status).toBe(200);

    for (const path of [`${freshnessAlias}`, `${freshnessAlias}.svg`]) {
      const response = await SELF.fetch(`https://relay.test/badge-relay/v1/${path}`);
      expect(response.status).toBe(200);
      const body = await response.text();
      expect(body).toContain("verified at 2026-09-12T10:00:00Z");
      expect(body).toContain("valid until 2026-09-12T10:30:00Z");
    }
  });

  it("rejects a freshness publication whose verified_at is in the Relay future", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:02:00Z"));
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(futureEntry))).toBe(true);

    const jwt = await token({ jti: "future-freshness-e2e-jti" });
    const digest = await canonicalPayloadDigest(futurePayload);
    const horizon = "2026-09-12T10:30:00Z";
    const prepare = await SELF.fetch(`https://relay.test/badge-relay/v1/${futureAlias}/prepare`, {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: futurePayload, canonical_digest: digest, profile: futureEntry.disclosure_profile, idempotency_key: "future-freshness-e2e-key", semantic_horizon: horizon })
    });
    expect(prepare.status).toBe(201);
    const challenge = await prepare.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const committed = await runInDurableObject(relay.get(relay.idFromName(futureAlias)), async (instance) => (instance as unknown as RelayDurableObject).commitTrustedPublication({
      body: { operation: "publish", challenge_id: challenge.challenge_id, idempotency_key: "future-freshness-e2e-key", canonical_bytes: futurePayload, canonical_digest: digest, profile: futureEntry.disclosure_profile, expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, semantic_horizon: horizon },
      entry: futureEntry,
      jtiHash: await sha256Hex(decodeJwt(jwt).jti as string),
      proof: { valid: true, kind: "github-pr-authoritative/v1", digest }
    }));
    expect(committed.status).toBe(409);
  });

  it.each([
    { alias: "a7f4k2q9", bytes: freshnessPayload, horizon: "2026-09-12T10:15:00Z", jti: "over-horizon-jti", key: "over-horizon-key" },
    { alias: "a7f4k2r9", bytes: overLeasePayload, horizon: "2026-09-12T11:30:00Z", jti: "over-lease-jti", key: "over-lease-key" }
  ])("rejects freshness publication with a saved validity envelope outside $alias bounds", async ({ alias: targetAlias, bytes, horizon, jti, key }) => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:02:00Z"));
    const targetEntry: RegistryEntry = { ...freshnessEntry, destination_alias: targetAlias };
    const registry = (env as unknown as { REGISTRY: DurableObjectNamespace }).REGISTRY;
    const registryStub = registry.get(registry.idFromName(REGISTRY_OBJECT_NAME));
    expect(await runInDurableObject(registryStub, async (instance) => (instance as unknown as RelayRegistryDurableObject).registerEntry(targetEntry))).toBe(true);

    const jwt = await token({ jti });
    const digest = await canonicalPayloadDigest(bytes);
    const prepare = await SELF.fetch(`https://relay.test/badge-relay/v1/${targetAlias}/prepare`, {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: bytes, canonical_digest: digest, profile: targetEntry.disclosure_profile, idempotency_key: key, semantic_horizon: horizon })
    });
    expect(prepare.status).toBe(201);
    const challenge = await prepare.json() as { challenge_id: string; generation: number; revocation_epoch: number };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const committed = await runInDurableObject(relay.get(relay.idFromName(targetAlias)), async (instance) => (instance as unknown as RelayDurableObject).commitTrustedPublication({
      body: { operation: "publish", challenge_id: challenge.challenge_id, idempotency_key: key, canonical_bytes: bytes, canonical_digest: digest, profile: targetEntry.disclosure_profile, expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, semantic_horizon: horizon },
      entry: targetEntry,
      jtiHash: await sha256Hex(decodeJwt(jwt).jti as string),
      proof: { valid: true, kind: "github-pr-authoritative/v1", digest }
    }));
    expect(committed.status).toBe(409);
    const state = await runInDurableObject(relay.get(relay.idFromName(targetAlias)), async (_instance, durableState) => durableState.storage.sql.exec<{ generation: number; payload: string | null }>("SELECT generation, payload FROM relay_state WHERE id=1").toArray()[0]);
    expect(state.generation).toBe(challenge.generation);
    expect(state.payload).toBeNull();
  });

  it("rejects issuer, algorithm, identity, and workflow-pin failures without mutation", async () => {
    const cases = [
      { iss: "https://issuer.invalid" },
      { repository_id: 99 },
      { job_workflow_sha: "3333333333333333333333333333333333333333" }
    ];
    for (const claims of cases) {
      const jwt = await token(claims);
      const digest = await canonicalPayloadDigest(payload);
      const response = await SELF.fetch(relayUrl("prepare"), {
        method: "POST",
        headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" },
        body: JSON.stringify({ operation: "prepare", canonical_bytes: payload, canonical_digest: digest, profile: entry.disclosure_profile, idempotency_key: `bad-${crypto.randomUUID()}`, semantic_horizon: new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z") })
      });
      expect([401, 403]).toContain(response.status);
      expect(await response.text()).not.toContain("synthetic-owner");
    }
    const badAlg = await new SignJWT({ iss: "https://token.actions.githubusercontent.com" }).setProtectedHeader({ alg: "HS256", kid: "local-key" }).sign(new TextEncoder().encode("wrong"));
    const rejected = await SELF.fetch(relayUrl("prepare"), { method: "POST", headers: { authorization: `Bearer ${badAlg}`, "content-type": "application/json" }, body: "{}" });
    expect(rejected.status).toBe(401);
  });

  it("redacts oversized bodies and invalid URL-bearing inputs", async () => {
    const jwt = await token();
    const oversized = await SELF.fetch(relayUrl("prepare"), { method: "POST", headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json" }, body: JSON.stringify({ operation: "prepare", source_url: "https://private.invalid/source", canonical_bytes: "x".repeat(17 * 1024) }) });
    expect(oversized.status).toBe(413);
    expect(await oversized.text()).not.toContain("private.invalid");
  });

  it("preserves an idempotent challenge and rejects a mismatched reuse", async () => {
    const jwt = await token({ jti: "idempotency-jti" });
    const idempotencyKey = "idempotency-key";
    const first = await prepareRemote(jwt, idempotencyKey);
    expect(first.response.status).toBe(201);
    const second = await prepareRemote(jwt, idempotencyKey);
    expect(second.response.status).toBe(200);
    expect(second.challenge?.challenge_id).toBe(first.challenge?.challenge_id);
    const mismatched = await prepareRemote(jwt, idempotencyKey, alternatePayload);
    expect(mismatched.response.status).toBe(409);
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const state = await runInDurableObject(relay.get(relay.idFromName(alias)), async (_instance, durableState) => durableState.storage.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM relay_challenges WHERE consumed=0").toArray()[0].count);
    expect(state).toBe(1);
  });

  it("loses a delayed writer on generation and epoch CAS after invalidation", async () => {
    const oldJwt = await token({ jti: "stale-writer-jti" });
    const idempotencyKey = "stale-writer-key";
    const prepared = await prepareRemote(oldJwt, idempotencyKey);
    expect(prepared.response.status).toBe(201);
    const challenge = prepared.challenge as { challenge_id: string; generation: number; revocation_epoch: number };
    const invalidator = await token({ jti: "invalidate-jti" });
    const invalidated = await SELF.fetch(relayUrl("invalidate"), {
      method: "POST",
      headers: { authorization: `Bearer ${invalidator}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "invalidate", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch })
    });
    expect(invalidated.status).toBe(200);
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stale = await commitInternal(relay.get(relay.idFromName(alias)), oldJwt, challenge, "publish", payload, idempotencyKey);
    expect(stale.status).toBe(409);
    const row = await runInDurableObject(relay.get(relay.idFromName(alias)), async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; revocation_epoch: number }>("SELECT status, generation, revocation_epoch FROM relay_state WHERE id=1").toArray()[0]);
    expect(row.status).toBe("unavailable");
    expect(row.generation).toBe(challenge.generation + 1);
    expect(row.revocation_epoch).toBe(challenge.revocation_epoch + 1);

    const freshJwt = await token({ jti: "fresh-writer-jti" });
    const freshKey = "fresh-writer-key";
    const freshPrepared = await prepareRemote(freshJwt, freshKey);
    expect(freshPrepared.response.status).toBe(201);
    const freshChallenge = freshPrepared.challenge as { challenge_id: string; generation: number; revocation_epoch: number };
    const freshCommit = await commitInternal(relay.get(relay.idFromName(alias)), freshJwt, freshChallenge, "publish", payload, freshKey);
    expect(freshCommit.status).toBe(200);
    const publicRead = await SELF.fetch(`https://relay.test/badge-relay/v1/${alias}`);
    expect(publicRead.status).toBe(200);
    expect(await publicRead.text()).toBe(payload);
  });

  it("gives revoke precedence over a delayed publish and preserves its tombstone", async () => {
    const oldJwt = await token({ jti: "revoke-writer-jti" });
    const idempotencyKey = "revoke-writer-key";
    const prepared = await prepareRemote(oldJwt, idempotencyKey);
    expect(prepared.response.status).toBe(201);
    const challenge = prepared.challenge as { challenge_id: string; generation: number; revocation_epoch: number };
    const revoker = await token({ jti: "revoker-jti" });
    const revoked = await SELF.fetch(relayUrl("revoke"), {
      method: "POST",
      headers: { authorization: `Bearer ${revoker}`, "content-type": "application/json" },
      body: JSON.stringify({ operation: "revoke", expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch })
    });
    expect(revoked.status).toBe(200);
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stale = await commitInternal(relay.get(relay.idFromName(alias)), oldJwt, challenge, "publish", payload, idempotencyKey);
    expect(stale.status).toBe(409);
    const row = await runInDurableObject(relay.get(relay.idFromName(alias)), async (_instance, state) => state.storage.sql.exec<{ status: string; tombstoned: number; payload: string | null }>("SELECT status, tombstoned, payload FROM relay_state WHERE id=1").toArray()[0]);
    expect(row.status).toBe("revoked");
    expect(row.tombstoned).toBe(1);
    expect(row.payload).toBeNull();
  });

  it("rejects one tenant's publisher against another tenant's object", async () => {
    const secondEntry: RegistryEntry = {
      ...entry,
      repository_id: 700000043,
      repository_owner_id: 7000000043,
      owner: "synthetic-owner-043",
      repository: "synthetic-repo-043",
      destination_alias: "a8f4k2m9",
      subject: "repo:synthetic-owner-043/synthetic-repo-043:ref:refs/heads/main"
    };
    const jwt = await token({ jti: "cross-tenant-jti" });
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const other = relay.get(relay.idFromName(secondEntry.destination_alias));
    const digest = await canonicalPayloadDigest(payload);
    const response = await (other.fetch as unknown as (input: unknown) => Promise<Response>)(new Request("https://relay.test/internal/a8f4k2m9/prepare", {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json", "x-relay-registry": JSON.stringify(secondEntry) },
      body: JSON.stringify({ operation: "prepare", canonical_bytes: payload, canonical_digest: digest, profile: secondEntry.disclosure_profile, idempotency_key: "cross-tenant-key", semantic_horizon: futureHorizon() })
    }));
    expect(response.status).toBe(403);
    const challengeCount = await runInDurableObject(other, async (_instance, state) => state.storage.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM relay_challenges").toArray()[0].count);
    expect(challengeCount).toBe(0);
  });

  it("does not let an allowlisted scheduled token reach initial publish", async () => {
    const scheduledEntry: RegistryEntry = { ...entry, permitted_events: ["push", "schedule"] };
    const jwt = await token({ event_name: "schedule" });
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const response = await (relay.get(relay.idFromName(alias)).fetch as unknown as (input: unknown) => Promise<Response>)(new Request("https://relay.test/internal/a7f4k2m9/publish", {
      method: "POST",
      headers: { authorization: `Bearer ${jwt}`, "content-type": "application/json", "x-relay-registry": JSON.stringify(scheduledEntry) },
      body: JSON.stringify({ operation: "publish" }),
    }));

    expect(response.status).toBe(403);
  });

  it("keeps the original challenge deadline immutable", async () => {
    const jwt = await token({ jti: "deadline-jti" });
    const idempotencyKey = "deadline-key";
    const prepared = await prepareRemote(jwt, idempotencyKey);
    expect(prepared.response.status).toBe(201);
    const challenge = prepared.challenge as { challenge_id: string; generation: number; revocation_epoch: number; deadline: string };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    await runInDurableObject(relay.get(relay.idFromName(alias)), async (_instance, state) => {
      state.storage.sql.exec("UPDATE relay_challenges SET deadline = ? WHERE id = ?", Math.floor(Date.now() / 1000) - 1, challenge.challenge_id);
    });
    const delayed = await commitInternal(relay.get(relay.idFromName(alias)), jwt, challenge, "publish", payload, idempotencyKey);
    expect(delayed.status).toBe(409);
    const challengeAfter = await runInDurableObject(relay.get(relay.idFromName(alias)), async (_instance, state) => state.storage.sql.exec<{ consumed: number; deadline: number }>("SELECT consumed, deadline FROM relay_challenges WHERE id = ?", challenge.challenge_id).toArray()[0]);
    expect(challengeAfter.consumed).toBe(0);
    expect(challengeAfter.deadline).toBeLessThan(Math.floor(Date.now() / 1000));
  });

  it("requires a recovery barrier before a fresh trusted commit", async () => {
    const firstJwt = await token({ jti: "recovery-first-jti" });
    const firstKey = "recovery-first-key";
    const first = await prepareRemote(firstJwt, firstKey);
    const firstChallenge = first.challenge as { challenge_id: string; generation: number; revocation_epoch: number };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const firstCommit = await commitInternal(relay.get(relay.idFromName(alias)), firstJwt, firstChallenge, "publish", payload, firstKey);
    expect(firstCommit.status).toBe(200);
    const stub = relay.get(relay.idFromName(alias));
    await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).markNeedsRecovery());
    const recoveryState = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; payload: string | null }>("SELECT status, payload FROM relay_state WHERE id=1").toArray()[0]);
    expect(recoveryState.status).toBe("needs-recovery");
    expect(recoveryState.payload).toBeNull();
    const recoveryJwt = await token({ jti: "recovery-second-jti" });
    const recoveryKey = "recovery-second-key";
    const prepared = await prepareRemote(recoveryJwt, recoveryKey);
    expect(prepared.response.status).toBe(201);
    const challenge = prepared.challenge as { challenge_id: string; generation: number; revocation_epoch: number };
    const recovered = await commitInternal(stub, recoveryJwt, challenge, "recover", payload, recoveryKey);
    expect(recovered.status).toBe(200);
    const recoveredState = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number }>("SELECT status, generation FROM relay_state WHERE id=1").toArray()[0]);
    expect(recoveredState.status).toBe("ready");
    expect(recoveredState.generation).toBe(challenge.generation + 1);
  });

  it("keeps admin recovery finalize as an authenticated proof-required guard", async () => {
    const recoveryAlias = "a833rcvr";
    const url = `https://relay.test/badge-relay/v1/admin/${recoveryAlias}/recover/finalize`;
    const testEnv = { ...(env as unknown as Record<string, unknown>), ADMIN_TOKEN: "admin" } as unknown as RelayEnvironment;
    const guardEntry: RegistryEntry = { ...entry, destination_alias: recoveryAlias };
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(recoveryAlias));
    const initialized = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).fetch(new Request(`https://relay.test/internal/admin/${recoveryAlias}/status`, {
      headers: { "x-relay-registry": JSON.stringify(guardEntry), "x-relay-registry-revision": "1", "x-relay-barrier-epoch": "1", "x-relay-admin": "1" }
    })));
    expect(initialized.status).toBe(200);
    const before = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; revocation_epoch: number }>("SELECT status,generation,revocation_epoch FROM relay_state WHERE id=1").toArray()[0]);
    const unauthorized = await worker.fetch(new Request(url, { method: "POST" }), testEnv);
    expect(unauthorized.status).toBe(401);
    const refused = await worker.fetch(new Request(url, { method: "POST", headers: { authorization: "Bearer admin" } }), testEnv);
    expect(refused.status).toBe(409);
    expect(await refused.json()).toEqual({ error: "fresh_publisher_proof_required" });
    const after = await runInDurableObject(stub, async (_instance, state) => state.storage.sql.exec<{ status: string; generation: number; revocation_epoch: number }>("SELECT status,generation,revocation_epoch FROM relay_state WHERE id=1").toArray()[0]);
    expect(after).toEqual(before);
  });
});
