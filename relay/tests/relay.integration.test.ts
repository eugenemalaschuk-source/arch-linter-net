import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
import { decodeJwt, generateKeyPair, SignJWT, exportJWK } from "jose";
import { env, SELF, evictDurableObject, listDurableObjectIds, runInDurableObject } from "cloudflare:test";
import {
  RelayDurableObject,
  REGISTRY_OBJECT_NAME,
  RelayRegistryDurableObject,
  canonicalPayloadDigest,
  canonicalizePayload,
  clearJwksCacheForTests,
  sha256Hex,
  type RegistryEntry
} from "../src/index";
import type { DurableObjectNamespace } from "@cloudflare/workers-types";

const alias = "a7f4k2m9";
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

  async function token(overrides: Record<string, unknown> = {}): Promise<string> {
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
    }).setProtectedHeader({ alg: "RS256", kid: "local-key" }).sign(privateKey);
  }

  function relayUrl(operation: string): string { return `https://relay.test/badge-relay/v1/${alias}/${operation}`; }

  function futureHorizon(): string {
    return new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z");
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

  it("rejects an unknown alias before Durable Object allocation", async () => {
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const before = (await listDurableObjectIds(relay)).length;
    const response = await SELF.fetch("https://relay.test/badge-relay/v1/a0000000");
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ error: "unknown_route" });
    expect((await listDurableObjectIds(relay)).length).toBe(before);
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
    expect(publish.status).toBe(403);
    const relay = (env as unknown as { RELAY: DurableObjectNamespace }).RELAY;
    const stub = relay.get(relay.idFromName(alias));
    const internalPublish = await runInDurableObject(stub, async (instance) => (instance as unknown as RelayDurableObject).commitTrustedPublication({
      body: { operation: "publish", challenge_id: challenge.challenge_id, idempotency_key: idempotency, canonical_bytes: payload, canonical_digest: digest, profile: entry.disclosure_profile, expected_generation: challenge.generation, expected_revocation_epoch: challenge.revocation_epoch, semantic_horizon: new Date(Date.now() + 30 * 60_000).toISOString().replace(".000Z", "Z") },
      entry,
      jtiHash: await sha256Hex((decodeJwt(jwt).jti as string)),
      proof: { valid: true, kind: "github-pr-authoritative/v1", digest }
    }));
    expect(internalPublish.status).toBe(200);
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
});
