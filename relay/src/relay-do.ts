import {
  CHALLENGE_SECONDS,
  COMPATIBILITY_PLAN,
  CONTRACT_VERSION,
  LEASE_SECONDS,
  MAX_MANIFEST_BYTES,
  MAX_PUBLIC_PAYLOAD_BYTES,
  MAX_REQUEST_BYTES,
  OPERATION_HISTORY_LIMIT,
  OPERATION_RETENTION_SECONDS,
  TOMBSTONE_RETENTION_SECONDS,
  RENEWAL_MINIMUM_SECONDS,
  type DisclosureProfile,
  type PublishRequest,
  type RegistryEntry,
  type RelayStateLike
} from "./types";
import { compatibilityReason, isKnownBundleDigest, redactStatus, SUPPORTED_BUNDLE, SUPPORTED_COMPATIBILITY_PLAN, SUPPORTED_CONTRACT_VERSION, type LifecycleOperation, type LifecycleReason } from "./lifecycle";
import { AuthorizationError, getBearerToken, isTrustedContext, sha256Hex, verifyOidcToken } from "./security";
import { canonicalPayloadDigest, PayloadError, validateCanonicalPayload } from "./payload";
import { readPublicRepresentation, type PublicRepresentation } from "./read";

const JSON_HEADERS = { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" };

interface StateRow {
  id: number;
  status: string;
  profile: DisclosureProfile;
  generation: number;
  revocation_epoch: number;
  payload: string | null;
  payload_digest: string | null;
  verified_at: string | null;
  valid_until: string | null;
  semantic_horizon: string | null;
  tree_sha: string | null;
  tombstoned: number;
  last_renewed_at: number | null;
  registry_revision: number;
  barrier_epoch: number;
  bundle: string;
  contract_version: string;
  compatibility_plan: string;
  bundle_digest: string | null;
  active_digest: string | null;
  staged_digest: string | null;
  previous_verified_digest: string | null;
  display_owner: string | null;
  display_repository: string | null;
  last_operation: string | null;
  last_reason: string | null;
  updated_at: number;
}

interface ChallengeRow {
  id: string;
  idempotency_hash: string;
  jti_hash: string;
  canonical_digest: string;
  requested_state: string;
  profile: DisclosureProfile;
  generation: number;
  revocation_epoch: number;
  deadline: number;
  semantic_horizon: string | null;
  consumed: number;
}

interface ReplayRow {
  hash: string;
  kind: string;
  canonical_digest: string;
  generation: number;
  revocation_epoch: number;
  challenge_id: string | null;
}

interface OperationRow {
  id: number;
  operation: string;
  status: string;
  reason: string;
  generation: number;
  revocation_epoch: number;
  created_at: number;
  operation_id: string;
}

function nowSeconds(): number { return Math.floor(Date.now() / 1000); }

function boundedString(value: unknown, maximum: number): value is string {
  return typeof value === "string" && new TextEncoder().encode(value).byteLength <= maximum;
}

function looksLikeUrl(value: string): boolean { return /(?:https?|ftp):\/\//iu.test(value); }

function containsUrl(value: unknown, depth = 0): boolean {
  if (depth > 5) return true;
  if (typeof value === "string") return looksLikeUrl(value);
  if (Array.isArray(value)) return value.some((item) => containsUrl(item, depth + 1));
  if (value && typeof value === "object") return Object.entries(value).some(([key, item]) => looksLikeUrl(key) || containsUrl(item, depth + 1));
  return false;
}

function safeInteger(value: unknown): value is number { return typeof value === "number" && Number.isSafeInteger(value); }

function response(status: number, body: unknown, headers: Record<string, string> = JSON_HEADERS): Response {
  return new Response(JSON.stringify(body), { status, headers });
}

function genericError(status: number): Response {
  const reason = status === 413 ? "request_too_large" : status === 409 ? "stale_or_replayed" : status === 403 ? "forbidden" : status === 503 ? "storage_unavailable" : "unauthorized";
  return response(status, { error: reason });
}

async function readBoundedJson(request: Request): Promise<Record<string, unknown>> {
  const contentLength = request.headers.get("content-length");
  if (contentLength && (!/^\d+$/u.test(contentLength) || Number(contentLength) > MAX_REQUEST_BYTES)) throw new PayloadError();
  const bytes = await request.arrayBuffer();
  if (bytes.byteLength > MAX_REQUEST_BYTES) throw new PayloadError();
  let parsed: unknown;
  try { parsed = JSON.parse(new TextDecoder().decode(bytes)); } catch { throw new PayloadError(); }
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || containsUrl(parsed)) throw new PayloadError();
  return parsed as Record<string, unknown>;
}

function asProfile(value: unknown): DisclosureProfile | undefined {
  return value === "headline-only/v1" || value === "headline-plus-freshness/v1" ? value : undefined;
}

function parseDateSeconds(value: unknown): number | undefined {
  if (typeof value !== "string") return undefined;
  const parsed = Date.parse(value);
  return Number.isFinite(parsed) ? Math.floor(parsed / 1000) : undefined;
}

function parseCanonicalDateSeconds(value: unknown): number | undefined {
  if (typeof value !== "string" || !/^20\d{2}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/u.test(value)) return undefined;
  const parsed = Date.parse(value);
  if (!Number.isFinite(parsed)) return undefined;
  const seconds = parsed / 1000;
  if (!Number.isSafeInteger(seconds)) return undefined;
  return new Date(parsed).toISOString().replace(".000Z", "Z") === value ? seconds : undefined;
}

export class RelayDurableObject {
  private readonly state: RelayStateLike;
  private readonly sql: RelayStateLike["storage"]["sql"];
  private readonly initialized: Promise<void>;
  private readonly shippedDigests: ReadonlySet<string>;

  constructor(state: DurableObjectState, env: unknown) {
    this.state = state as RelayStateLike;
    this.sql = this.state.storage.sql;
    const configured = (env as { RELAY_SHIPPED_BUNDLE_DIGESTS?: string | string[] } | undefined)?.RELAY_SHIPPED_BUNDLE_DIGESTS;
    const values = Array.isArray(configured) ? configured : typeof configured === "string" ? configured.split(",") : [];
    this.shippedDigests = new Set(values.map((value) => value.trim()).filter((value) => /^[0-9a-f]{64}$/u.test(value)));
    this.initialized = this.initializeState();
  }

  private initializeState(): Promise<void> {
    return this.state.blockConcurrencyWhile(() => { this.ensureSchema(); });
  }

  private ensureSchema(): void {
    this.sql.exec(`CREATE TABLE IF NOT EXISTS relay_state (
      id INTEGER PRIMARY KEY CHECK (id = 1),
      status TEXT NOT NULL,
      profile TEXT NOT NULL,
      generation INTEGER NOT NULL,
      revocation_epoch INTEGER NOT NULL,
      payload TEXT,
      payload_digest TEXT,
      verified_at TEXT,
      valid_until TEXT,
      semantic_horizon TEXT,
      tree_sha TEXT,
      tombstoned INTEGER NOT NULL DEFAULT 0,
      last_renewed_at INTEGER,
      registry_revision INTEGER NOT NULL DEFAULT 1,
      barrier_epoch INTEGER NOT NULL DEFAULT 1,
      bundle TEXT NOT NULL DEFAULT 'badge-relay/v1',
      contract_version TEXT NOT NULL DEFAULT 'v1',
      compatibility_plan TEXT NOT NULL DEFAULT 'architecture-health-badge-relay/v1',
      bundle_digest TEXT,
      active_digest TEXT,
      staged_digest TEXT,
      previous_verified_digest TEXT,
      display_owner TEXT,
      display_repository TEXT,
      last_operation TEXT,
      last_reason TEXT,
      updated_at INTEGER NOT NULL DEFAULT 0
    )`).toArray();
    this.ensureColumn("registry_revision", "INTEGER NOT NULL DEFAULT 1");
    this.ensureColumn("barrier_epoch", "INTEGER NOT NULL DEFAULT 1");
    this.ensureColumn("bundle", "TEXT NOT NULL DEFAULT 'badge-relay/v1'");
    this.ensureColumn("contract_version", "TEXT NOT NULL DEFAULT 'v1'");
    this.ensureColumn("compatibility_plan", "TEXT NOT NULL DEFAULT 'architecture-health-badge-relay/v1'");
    this.ensureColumn("bundle_digest", "TEXT");
    this.ensureColumn("active_digest", "TEXT");
    this.ensureColumn("staged_digest", "TEXT");
    this.ensureColumn("previous_verified_digest", "TEXT");
    this.ensureColumn("display_owner", "TEXT");
    this.ensureColumn("display_repository", "TEXT");
    this.ensureColumn("last_operation", "TEXT");
    this.ensureColumn("last_reason", "TEXT");
    this.ensureColumn("updated_at", "INTEGER NOT NULL DEFAULT 0");
    this.sql.exec(`CREATE TABLE IF NOT EXISTS relay_challenges (
      id TEXT PRIMARY KEY,
      idempotency_hash TEXT NOT NULL,
      jti_hash TEXT NOT NULL,
      canonical_digest TEXT NOT NULL,
      requested_state TEXT NOT NULL,
      profile TEXT NOT NULL,
      generation INTEGER NOT NULL,
      revocation_epoch INTEGER NOT NULL,
      deadline INTEGER NOT NULL,
      semantic_horizon TEXT,
      consumed INTEGER NOT NULL DEFAULT 0,
      created_at INTEGER NOT NULL
    )`).toArray();
    this.sql.exec("CREATE INDEX IF NOT EXISTS relay_challenges_idempotency ON relay_challenges(idempotency_hash)").toArray();
    this.sql.exec(`CREATE TABLE IF NOT EXISTS relay_replay_keys (
      hash TEXT PRIMARY KEY,
      kind TEXT NOT NULL,
      canonical_digest TEXT NOT NULL,
      generation INTEGER NOT NULL,
      revocation_epoch INTEGER NOT NULL,
      challenge_id TEXT,
      used_at INTEGER NOT NULL
    )`).toArray();
    this.sql.exec(`CREATE TABLE IF NOT EXISTS relay_operations (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      operation TEXT NOT NULL,
      status TEXT NOT NULL,
      reason TEXT NOT NULL,
      operation_id TEXT NOT NULL,
      generation INTEGER NOT NULL,
      revocation_epoch INTEGER NOT NULL,
      created_at INTEGER NOT NULL
    )`).toArray();
    const operationColumns = this.sql.exec<{ name: string }>("PRAGMA table_info(relay_operations)").toArray();
    if (!operationColumns.some((column) => column.name === "operation_id")) {
      this.sql.exec("ALTER TABLE relay_operations ADD COLUMN operation_id TEXT NOT NULL DEFAULT ''").toArray();
      const legacy = this.sql.exec<{ id: number }>("SELECT id FROM relay_operations WHERE operation_id = ''").toArray();
      for (const row of legacy) this.sql.exec("UPDATE relay_operations SET operation_id=? WHERE id=?", `legacy-${row.id}`, row.id).toArray();
    }
    this.sql.exec("CREATE UNIQUE INDEX IF NOT EXISTS relay_operations_operation_id ON relay_operations(operation_id)").toArray();
  }

  private ensureColumn(name: string, definition: string): void {
    const columns = this.sql.exec<{ name: string }>("PRAGMA table_info(relay_state)").toArray();
    if (!columns.some((column) => column.name === name)) this.sql.exec(`ALTER TABLE relay_state ADD COLUMN ${name} ${definition}`).toArray();
  }

  private row(): StateRow {
    const rows = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id = 1").toArray();
    if (rows.length === 0) throw new Error("state not registered");
    return rows[0];
  }

  private ensureRegistered(entry: RegistryEntry, registryRevision = 1, barrierEpoch = 1): StateRow {
    const existing = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id = 1").toArray();
    if (existing.length > 0) {
      if (existing[0].tombstoned !== 0 && existing[0].updated_at > 0 && existing[0].updated_at < nowSeconds() - TOMBSTONE_RETENTION_SECONDS) {
        this.sql.exec("DELETE FROM relay_state WHERE id=1").toArray();
      } else {
        return existing[0];
      }
    }
    const generation = entry.initial_state?.generation && entry.initial_state.generation > 0 ? Math.floor(entry.initial_state.generation) : 1;
    const epoch = entry.initial_state?.revocation_epoch && entry.initial_state.revocation_epoch > 0 ? Math.floor(entry.initial_state.revocation_epoch) : 1;
    const now = nowSeconds();
    this.sql.exec(`INSERT INTO relay_state (id,status,profile,generation,revocation_epoch,payload,payload_digest,verified_at,valid_until,semantic_horizon,tree_sha,tombstoned,last_renewed_at,registry_revision,barrier_epoch,bundle,contract_version,compatibility_plan,bundle_digest,active_digest,staged_digest,previous_verified_digest,display_owner,display_repository,last_operation,last_reason,updated_at)
      VALUES (1,'unavailable',?,?,?,NULL,NULL,NULL,NULL,?,?,0,NULL,?,?,?,?,?,?,?,?,?,?,?,'register','ok',?)`, entry.disclosure_profile, generation, epoch, entry.initial_state?.semantic_horizon ?? null, entry.initial_state?.tree_sha ?? null, registryRevision, barrierEpoch, entry.bundle ?? SUPPORTED_BUNDLE, entry.contract_version ?? SUPPORTED_CONTRACT_VERSION, entry.compatibility_plan ?? SUPPORTED_COMPATIBILITY_PLAN, entry.bundle_digest ?? null, entry.bundle_digest ?? null, null, null, entry.owner ?? null, entry.repository ?? null, now).toArray();
    return this.row();
  }

  private recordOperation(operation: LifecycleOperation | string, status: string, reason: LifecycleReason | string, row?: StateRow, operationId = `${operation}-${crypto.randomUUID()}`): void {
    const current = row ?? this.row();
    const now = nowSeconds();
    this.sql.exec("INSERT OR IGNORE INTO relay_operations (operation,status,reason,operation_id,generation,revocation_epoch,created_at) VALUES (?,?,?,?,?,?,?)", operation, status, reason, operationId, current.generation, current.revocation_epoch, now).toArray();
    this.sql.exec("DELETE FROM relay_operations WHERE created_at < ?", now - OPERATION_RETENTION_SECONDS).toArray();
    this.sql.exec("DELETE FROM relay_operations WHERE id IN (SELECT id FROM relay_operations ORDER BY created_at DESC, id DESC LIMIT -1 OFFSET ?)", OPERATION_HISTORY_LIMIT).toArray();
    this.sql.exec("UPDATE relay_state SET last_operation=?, last_reason=?, updated_at=? WHERE id=1", operation, reason, now).toArray();
  }

  private operationAlreadyApplied(operationId: string): OperationRow | undefined {
    return this.sql.exec<OperationRow>("SELECT * FROM relay_operations WHERE operation_id = ? LIMIT 1", operationId).toArray()[0];
  }

  private pruneRetention(): void {
    const now = nowSeconds();
    this.sql.exec("DELETE FROM relay_operations WHERE created_at < ?", now - OPERATION_RETENTION_SECONDS).toArray();
    this.sql.exec("DELETE FROM relay_operations WHERE id IN (SELECT id FROM relay_operations ORDER BY created_at DESC, id DESC LIMIT -1 OFFSET ?)", OPERATION_HISTORY_LIMIT).toArray();
    const current = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id=1").toArray()[0];
    if (current?.tombstoned !== 0 && current?.updated_at > 0 && current.updated_at < now - TOMBSTONE_RETENTION_SECONDS) {
      this.sql.exec("DELETE FROM relay_state WHERE id=1").toArray();
      this.sql.exec("DELETE FROM relay_challenges WHERE consumed=0").toArray();
    }
  }

  private checkRegistryBarrier(request: Request, current: StateRow, entry?: RegistryEntry): "ok" | "stale-caller" | "advanced" {
    const revisionHeader = request.headers.get("x-relay-registry-revision");
    const epochHeader = request.headers.get("x-relay-barrier-epoch");
    if (revisionHeader === null && epochHeader === null) return "ok";
    const revision = revisionHeader === null ? current.registry_revision : Number(revisionHeader);
    const epoch = epochHeader === null ? current.barrier_epoch : Number(epochHeader);
    if (!Number.isSafeInteger(revision) || !Number.isSafeInteger(epoch) || revision < current.registry_revision || epoch < current.barrier_epoch) return "stale-caller";
    if (revision === current.registry_revision && epoch === current.barrier_epoch) return "ok";
    const registryTombstoned = request.headers.get("x-relay-registry-tombstoned") === "true";
    const nextRevision = Number.isSafeInteger(revision) ? Math.max(current.registry_revision, revision) : current.registry_revision;
    const nextEpoch = Number.isSafeInteger(epoch) ? Math.max(current.barrier_epoch, epoch) : current.barrier_epoch;
    this.state.storage.transactionSync(() => {
      if (nextEpoch === current.barrier_epoch && nextRevision > current.registry_revision) {
        // A display-only rename advances the registry revision but not the
        // revocation barrier. Synchronize metadata without destroying ready data.
        this.sql.exec("UPDATE relay_state SET registry_revision=?, display_owner=?, display_repository=?, updated_at=? WHERE id=1", nextRevision, entry?.owner ?? current.display_owner, entry?.repository ?? current.display_repository, nowSeconds()).toArray();
        this.recordOperation("barrier-sync", current.status, "ok", this.row(), `barrier-revision-${nextRevision}`);
        return;
      }
      this.sql.exec("UPDATE relay_state SET status=?, generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=?, last_renewed_at=NULL, registry_revision=?, barrier_epoch=?, updated_at=? WHERE id=1", registryTombstoned ? "revoked" : "needs-recovery", registryTombstoned ? 1 : 0, nextRevision, nextEpoch, nowSeconds()).toArray();
      this.sql.exec("DELETE FROM relay_challenges WHERE consumed=0").toArray();
      this.recordOperation("barrier-check", registryTombstoned ? "revoked" : "needs-recovery", registryTombstoned ? "already_revoked" : "stale_registry_barrier", this.row(), `barrier-${nextRevision}-${nextEpoch}`);
    });
    return "advanced";
  }

  private rollback(): void { /* transactionSync rolls back automatically on throw */ }

  private async validatePublisher(request: Request, entry: RegistryEntry, operation: string): Promise<import("./types").ValidatedPublisher> {
    const token = getBearerToken(request);
    const audience = entry.audience ?? "";
    const publisher = await verifyOidcToken(token, entry, { trust: { issuer: "https://token.actions.githubusercontent.com", jwks_uri: "https://token.actions.githubusercontent.com/.well-known/jwks", audience } });
    const eventName = publisher.claims.event_name;
    // A scheduled token is a renewal-only capability. Requiring the operation
    // to agree with the event prevents an allowlisted scheduler from reaching
    // the initial publish, recovery, or lifecycle mutation paths.
    const expectedEvent = operation === "renew" ? "schedule" : "push";
    if (eventName !== expectedEvent) throw new AuthorizationError(403);
    return publisher;
  }

  /**
   * Complete the public publisher handoff inside the Relay trust boundary.
   *
   * The reusable workflow is the artifact/context verifier; its exact
   * workflow identity is established by the signed OIDC token before this
   * method is reached.  The Relay independently recomputes the canonical
   * payload digest in publish() and derives the narrow commit proof here.
   * Request JSON never supplies a valid flag or an internal proof object.
   */
  private trustedPublisherHandoff(body: Record<string, unknown>, publisher: import("./types").ValidatedPublisher): import("./types").TrustedContextProof {
    if (publisher.claims.job_workflow_ref === undefined || publisher.claims.job_workflow_sha === undefined) throw new AuthorizationError(403);
    return {
      valid: true,
      kind: "github-pr-authoritative/v1",
      digest: typeof body.canonical_digest === "string" ? body.canonical_digest : undefined,
      semantic_horizon: typeof body.semantic_horizon === "string" ? body.semantic_horizon : undefined
    };
  }

  private validateOperationBasics(body: Record<string, unknown>, operation: string): void {
    if (body.operation !== operation) throw new PayloadError();
    if (typeof body.idempotency_key === "string" && body.idempotency_key.length > 256) throw new PayloadError();
    if (typeof body.manifest === "string" && !boundedString(body.manifest, MAX_MANIFEST_BYTES)) throw new PayloadError();
  }

  private async prepare(body: Record<string, unknown>, entry: RegistryEntry, publisher: import("./types").ValidatedPublisher, operation: "prepare" | "renew"): Promise<Response> {
    this.validateOperationBasics(body, operation);
    const profile = asProfile(body.profile);
    if (!profile || profile !== entry.disclosure_profile || !boundedString(body.canonical_bytes, MAX_PUBLIC_PAYLOAD_BYTES) || !boundedString(body.canonical_digest, 128) || typeof body.idempotency_key !== "string" || body.idempotency_key.length === 0) throw new PayloadError();
    const payload = validateCanonicalPayload(body.canonical_bytes, profile);
    const digest = await canonicalPayloadDigest(body.canonical_bytes);
    if (digest !== body.canonical_digest) throw new PayloadError();
    const idempotencyHash = await sha256Hex(body.idempotency_key);
    const semanticHorizon = typeof body.semantic_horizon === "string" ? body.semantic_horizon : payload.valid_until;
    return this.state.storage.transactionSync(() => {
      try {
      const current = this.row();
      if (current.tombstoned || current.status === "revoked") return this.finishError(403);
      if (safeInteger(body.expected_generation) && body.expected_generation !== current.generation) return this.finishError(409);
      if (safeInteger(body.expected_revocation_epoch) && body.expected_revocation_epoch !== current.revocation_epoch) return this.finishError(409);
      const duplicate = this.sql.exec<ChallengeRow>("SELECT * FROM relay_challenges WHERE idempotency_hash = ? ORDER BY created_at DESC LIMIT 1", idempotencyHash).toArray()[0];
      if (duplicate) {
        if (duplicate.deadline <= nowSeconds() || duplicate.canonical_digest !== digest || duplicate.generation !== current.generation || duplicate.revocation_epoch !== current.revocation_epoch || duplicate.profile !== profile) return this.finishError(409);
        return response(200, { ok: true, challenge_id: duplicate.id, deadline: new Date(duplicate.deadline * 1000).toISOString(), generation: duplicate.generation, revocation_epoch: duplicate.revocation_epoch });
      }
      const jtiReplay = this.sql.exec<ReplayRow>("SELECT * FROM relay_replay_keys WHERE hash = ? LIMIT 1", publisher.jtiHash).toArray()[0];
      if (jtiReplay) return this.finishError(409);
      const active = this.sql.exec<{ count: number }>("SELECT COUNT(*) AS count FROM relay_challenges WHERE consumed = 0 AND deadline > ?", nowSeconds()).toArray()[0]?.count ?? 0;
      if (active >= 8) return this.finishError(429);
      const deadline = nowSeconds() + CHALLENGE_SECONDS;
      const challengeId = crypto.randomUUID();
      this.sql.exec(`INSERT INTO relay_challenges (id,idempotency_hash,jti_hash,canonical_digest,requested_state,profile,generation,revocation_epoch,deadline,semantic_horizon,consumed,created_at)
        VALUES (?,?,?,?,?,?,?,?,?,?,0,?)`, challengeId, idempotencyHash, publisher.jtiHash, digest, "ready", profile, current.generation, current.revocation_epoch, deadline, semanticHorizon ?? null, nowSeconds()).toArray();
      this.sql.exec("INSERT INTO relay_replay_keys (hash,kind,canonical_digest,generation,revocation_epoch,challenge_id,used_at) VALUES (?,?,?,?,?,?,?)", idempotencyHash, "idempotency", digest, current.generation, current.revocation_epoch, challengeId, nowSeconds()).toArray();
      this.sql.exec("INSERT INTO relay_replay_keys (hash,kind,canonical_digest,generation,revocation_epoch,challenge_id,used_at) VALUES (?,?,?,?,?,?,?)", publisher.jtiHash, "jti", digest, current.generation, current.revocation_epoch, challengeId, nowSeconds()).toArray();
      return response(201, { ok: true, challenge_id: challengeId, deadline: new Date(deadline * 1000).toISOString(), generation: current.generation, revocation_epoch: current.revocation_epoch });
      } catch (error) {
        throw error;
      }
    });
  }

  private finishError(status: number): Response {
    this.rollback();
    return genericError(status);
  }

  private async read(request: Request, kind: PublicRepresentation, entry: RegistryEntry): Promise<Response> {
    // Public reads are strictly read-only. In particular, do not call
    // ensureRegistered here: a registered alias may legitimately have no
    // publication row yet, and a GET must not create or repair one.
    let current: StateRow | undefined;
    try {
      current = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id = 1").toArray()[0];
    } catch {
      return readPublicRepresentation(request, undefined, kind, entry, true);
    }
    return readPublicRepresentation(request, current, kind, entry);
  }

  private async publish(body: Record<string, unknown>, entry: RegistryEntry, publisher: import("./types").ValidatedPublisher, operation: "publish" | "renew" | "recover", internalProof?: unknown): Promise<Response> {
    this.validateOperationBasics(body, operation);
    if ("trusted_context" in body) throw new AuthorizationError(403);
    const profile = asProfile(body.profile);
    if (!profile || profile !== entry.disclosure_profile || !boundedString(body.challenge_id, 128) || !boundedString(body.canonical_bytes, MAX_PUBLIC_PAYLOAD_BYTES) || !boundedString(body.canonical_digest, 128) || typeof body.idempotency_key !== "string") throw new PayloadError();
    const payload = validateCanonicalPayload(body.canonical_bytes, profile);
    if (await canonicalPayloadDigest(body.canonical_bytes) !== body.canonical_digest) throw new PayloadError();
    // Public HTTP calls receive a server-derived handoff after OIDC identity
    // validation. Caller-supplied trusted_context is rejected; the local
    // method remains available to internal verifier/recovery code that passes
    // an explicit typed proof.
    const trustedProof = internalProof ?? this.trustedPublisherHandoff(body, publisher);
    if (!isTrustedContext(trustedProof) || trustedProof.kind !== "github-pr-authoritative/v1" || trustedProof.digest !== body.canonical_digest) throw new AuthorizationError(403);
    const horizon = typeof body.semantic_horizon === "string" ? body.semantic_horizon : payload.valid_until;
    const horizonSeconds = parseDateSeconds(horizon);
    if (!horizonSeconds || horizonSeconds <= nowSeconds()) throw new AuthorizationError(409);
    if (profile === "headline-plus-freshness/v1") {
      const payloadHorizon = parseCanonicalDateSeconds(payload.valid_until);
      if (!payloadHorizon || payloadHorizon <= nowSeconds() || payloadHorizon > horizonSeconds || payloadHorizon > nowSeconds() + LEASE_SECONDS) return genericError(409);
    }
    if (!safeInteger(body.expected_generation) || !safeInteger(body.expected_revocation_epoch)) throw new PayloadError();
    const idempotencyHash = await sha256Hex(body.idempotency_key);
    return this.state.storage.transactionSync(() => {
      try {
      const current = this.row();
      if (operation === "recover" && current.status !== "needs-recovery") return this.finishError(409);
      const challenge = this.sql.exec<ChallengeRow>("SELECT * FROM relay_challenges WHERE id = ? LIMIT 1", body.challenge_id).toArray()[0];
      if (!challenge || challenge.consumed || challenge.deadline <= nowSeconds() || challenge.canonical_digest !== body.canonical_digest || challenge.profile !== profile || challenge.generation !== body.expected_generation || challenge.revocation_epoch !== body.expected_revocation_epoch) return this.finishError(409);
      if (idempotencyHash !== challenge.idempotency_hash) return this.finishError(409);
      if (challenge.jti_hash !== publisher.jtiHash || current.generation !== body.expected_generation || current.revocation_epoch !== body.expected_revocation_epoch || current.status === "revoked" || current.tombstoned) return this.finishError(409);
      if (operation === "renew" && current.last_renewed_at !== null && nowSeconds() - current.last_renewed_at < RENEWAL_MINIMUM_SECONDS) return this.finishError(409);
      const newGeneration = current.generation + 1;
      const verifiedAt = profile === "headline-plus-freshness/v1" && payload.verified_at
        ? payload.verified_at
        : new Date(nowSeconds() * 1000).toISOString().replace(".000Z", "Z");
      const verifiedAtSeconds = parseCanonicalDateSeconds(verifiedAt);
      if (!verifiedAtSeconds || verifiedAtSeconds > nowSeconds()) return this.finishError(409);
      const maxLease = nowSeconds() + LEASE_SECONDS;
      const validUntilSeconds = Math.min(maxLease, horizonSeconds);
      if (validUntilSeconds <= nowSeconds()) return this.finishError(409);
      const validUntil = profile === "headline-plus-freshness/v1" && payload.valid_until
        ? payload.valid_until
        : new Date(validUntilSeconds * 1000).toISOString().replace(".000Z", "Z");
      const persistedValidUntilSeconds = parseCanonicalDateSeconds(validUntil);
      if (!persistedValidUntilSeconds
        || persistedValidUntilSeconds <= nowSeconds()
        || persistedValidUntilSeconds > horizonSeconds
        || persistedValidUntilSeconds > maxLease
        || persistedValidUntilSeconds > verifiedAtSeconds + LEASE_SECONDS) return this.finishError(409);
      this.sql.exec("UPDATE relay_challenges SET consumed = 1 WHERE id = ? AND consumed = 0", challenge.id).toArray();
      this.sql.exec(`UPDATE relay_state SET status='ready', profile=?, generation=?, payload=?, payload_digest=?, verified_at=?, valid_until=?, semantic_horizon=?, tombstoned=0, last_renewed_at=?, updated_at=?
        WHERE id=1 AND generation=? AND revocation_epoch=? AND tombstoned=0 AND status <> 'revoked'`, profile, newGeneration, body.canonical_bytes, body.canonical_digest, verifiedAt, validUntil, horizon, nowSeconds(), nowSeconds(), body.expected_generation, body.expected_revocation_epoch).toArray();
      // SQLite UPDATE's result is not portable across the Workers cursor, so
      // re-read the row as the compare-and-set witness.
      const after = this.row();
      if (after.generation !== newGeneration || after.payload_digest !== body.canonical_digest) return this.finishError(409);
      this.recordOperation(operation, "ready", "ok", after);
      return response(200, { ok: true, generation: newGeneration, revocation_epoch: after.revocation_epoch, state: "ready", valid_until: validUntil });
      } catch (error) {
        throw error;
      }
    });
  }

  private async lifecycle(body: Record<string, unknown>, entry: RegistryEntry, publisher: import("./types").ValidatedPublisher, operation: "invalidate" | "revoke"): Promise<Response> {
    this.validateOperationBasics(body, operation);
    if (safeInteger(body.expected_generation) === false || safeInteger(body.expected_revocation_epoch) === false) throw new PayloadError();
    return this.state.storage.transactionSync(() => {
      try {
      const current = this.row();
      if (current.generation !== body.expected_generation || current.revocation_epoch !== body.expected_revocation_epoch || current.tombstoned) return this.finishError(409);
      if (publisher.claims.repository_id !== entry.repository_id || publisher.claims.repository_owner_id !== entry.repository_owner_id) return this.finishError(403);
      const status = operation === "revoke" ? "revoked" : "unavailable";
      this.sql.exec(`UPDATE relay_state SET status=?, generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=?, last_renewed_at=NULL WHERE id=1 AND generation=? AND revocation_epoch=? AND tombstoned=0`, status, operation === "revoke" ? 1 : 0, body.expected_generation, body.expected_revocation_epoch).toArray();
      this.sql.exec("DELETE FROM relay_challenges WHERE consumed = 0").toArray();
      const after = this.row();
      if (operation === "revoke" && (after.status !== "revoked" || !after.tombstoned)) return this.finishError(409);
      this.recordOperation(operation, after.status, operation === "invalidate" ? "expired" : "ok", after);
      return response(200, { ok: true, state: status, generation: after.generation, revocation_epoch: after.revocation_epoch });
      } catch (error) {
        throw error;
      }
    });
  }

  private adminStatus(): Response {
    this.pruneRetention();
    let current: StateRow;
    try { current = this.row(); } catch { return response(404, { error: "not_registered" }); }
    return response(200, redactStatus({
      state: current.status as import("./types").RelayState,
      generation: current.generation,
      revocation_epoch: current.revocation_epoch,
      registry_revision: current.registry_revision,
      barrier_epoch: current.barrier_epoch,
      profile: current.profile,
      bundle: current.bundle,
      contract_version: current.contract_version,
      compatibility_plan: current.compatibility_plan,
      active_digest: current.active_digest ?? current.bundle_digest,
      staged_digest: current.staged_digest,
      previous_verified_digest: current.previous_verified_digest,
      display_owner: current.display_owner,
      display_repository: current.display_repository,
      verified_at: current.verified_at,
      valid_until: current.valid_until,
      tombstoned: current.tombstoned !== 0,
      last_operation: current.last_operation as LifecycleOperation | null,
      last_reason: current.last_reason as LifecycleReason | null,
      updated_at: new Date((current.updated_at || nowSeconds()) * 1000).toISOString()
    }));
  }

  private adminMutation(body: Record<string, unknown>, operation: "admin-invalidate" | "admin-revoke" | "admin-recover-open" | "admin-uninstall"): Response {
    if (typeof body.operation_id !== "string" || body.operation_id.length === 0 || body.operation_id.length > 128) return genericError(413);
    return this.state.storage.transactionSync(() => {
      const current = this.row();
      const prior = this.operationAlreadyApplied(body.operation_id as string);
      if (prior) {
        if (prior.operation !== operation) return this.finishError(409);
        return response(200, { ok: true, state: prior.status, generation: prior.generation, revocation_epoch: prior.revocation_epoch, operation_id: body.operation_id });
      }
      if (safeInteger(body.expected_registry_revision) && body.expected_registry_revision !== current.registry_revision) return this.finishError(409);
      if (safeInteger(body.expected_barrier_epoch) && body.expected_barrier_epoch !== current.barrier_epoch) return this.finishError(409);
      if (safeInteger(body.expected_generation) && body.expected_generation !== current.generation) return this.finishError(409);
      if (safeInteger(body.expected_revocation_epoch) && body.expected_revocation_epoch !== current.revocation_epoch) return this.finishError(409);
      if (current.tombstoned !== 0 && operation !== "admin-revoke" && operation !== "admin-uninstall") return this.finishError(409);
      if (operation === "admin-revoke" || operation === "admin-uninstall") {
        this.sql.exec("UPDATE relay_state SET status='revoked', generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=1, last_renewed_at=NULL, updated_at=? WHERE id=1 AND generation=? AND revocation_epoch=? AND tombstoned=0", nowSeconds(), current.generation, current.revocation_epoch).toArray();
      } else if (operation === "admin-recover-open") {
        this.sql.exec("UPDATE relay_state SET status='needs-recovery', generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=0, last_renewed_at=NULL, updated_at=? WHERE id=1 AND generation=? AND revocation_epoch=?", nowSeconds(), current.generation, current.revocation_epoch).toArray();
      } else {
        this.sql.exec("UPDATE relay_state SET status='unavailable', generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=0, last_renewed_at=NULL, updated_at=? WHERE id=1 AND generation=? AND revocation_epoch=? AND tombstoned=0", nowSeconds(), current.generation, current.revocation_epoch).toArray();
      }
      this.sql.exec("DELETE FROM relay_challenges WHERE consumed=0").toArray();
      const after = this.row();
      const status = after.status;
      const reason: LifecycleReason = operation === "admin-recover-open" ? "recovery_required" : operation === "admin-invalidate" ? "expired" : "ok";
      this.recordOperation(operation, status, reason, after, body.operation_id as string);
      return response(200, { ok: true, state: status, generation: after.generation, revocation_epoch: after.revocation_epoch, operation_id: body.operation_id });
    });
  }

  private adminUpgrade(body: Record<string, unknown>): Response {
    const reason = compatibilityReason({ bundle: body.bundle, contract_version: body.contract_version, compatibility_plan: body.compatibility_plan, bundle_digest: body.bundle_digest });
    if (reason !== "ok") return response(409, { error: "compatibility_conflict" });
    if (!boundedString(body.operation_id, 128) || body.operation_id.length === 0) return genericError(413);
    const current = this.row();
    const operation = typeof body.operation === "string" ? body.operation : "upgrade-stage";
    if (operation !== "upgrade-stage" && operation !== "upgrade-activate" && operation !== "upgrade-rollback") return response(409, { error: "compatibility_conflict" });
    const operationIdValue = body.operation_id as string;
    return this.state.storage.transactionSync(() => {
      const before = this.row();
      const prior = this.operationAlreadyApplied(operationIdValue);
      if (prior) {
        if (prior.operation !== operation) return this.finishError(409);
        return response(200, { ok: true, state: prior.status, generation: prior.generation, revocation_epoch: prior.revocation_epoch, operation_id: operationIdValue });
      }
      const active = before.active_digest ?? before.bundle_digest;
      const requested = typeof body.bundle_digest === "string" ? body.bundle_digest : undefined;
      const known = requested === undefined || isKnownBundleDigest(requested, this.shippedDigests, active);
      if (!known || (operation === "upgrade-activate" && requested === undefined && !before.staged_digest)) return response(409, { error: "compatibility_conflict" });
      if (body.to_bundle !== undefined && body.to_bundle !== SUPPORTED_BUNDLE) return response(409, { error: "compatibility_conflict" });

      let target: string | null = requested ?? null;
      if (operation === "upgrade-stage") {
        if (target === null) {
          // Preserve the historical metadata-only upgrade for installations
          // that have not opted into digest pinning yet.
          this.sql.exec("UPDATE relay_state SET bundle=?, contract_version=?, compatibility_plan=?, updated_at=? WHERE id=1", SUPPORTED_BUNDLE, SUPPORTED_CONTRACT_VERSION, SUPPORTED_COMPATIBILITY_PLAN, nowSeconds()).toArray();
        } else {
          this.sql.exec("UPDATE relay_state SET bundle=?, contract_version=?, compatibility_plan=?, staged_digest=?, updated_at=? WHERE id=1", SUPPORTED_BUNDLE, SUPPORTED_CONTRACT_VERSION, SUPPORTED_COMPATIBILITY_PLAN, target, nowSeconds()).toArray();
        }
      } else {
        target = target ?? (operation === "upgrade-rollback" ? before.previous_verified_digest : before.staged_digest);
        if (!target || !isKnownBundleDigest(target, this.shippedDigests, active)) return response(409, { error: "compatibility_conflict" });
        if (operation === "upgrade-rollback" && target !== before.previous_verified_digest) return response(409, { error: "compatibility_conflict" });
        this.sql.exec("UPDATE relay_state SET status='unavailable', generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=0, last_renewed_at=NULL, bundle=?, contract_version=?, compatibility_plan=?, bundle_digest=?, active_digest=?, staged_digest=NULL, previous_verified_digest=?, updated_at=? WHERE id=1", SUPPORTED_BUNDLE, SUPPORTED_CONTRACT_VERSION, SUPPORTED_COMPATIBILITY_PLAN, target, target, active, nowSeconds()).toArray();
        this.sql.exec("DELETE FROM relay_challenges WHERE consumed=0").toArray();
      }
      const after = this.row();
      this.recordOperation(operation, after.status, "ok", after, operationIdValue);
      return response(200, { ok: true, state: after.status, generation: after.generation, revocation_epoch: after.revocation_epoch, bundle: SUPPORTED_BUNDLE, contract_version: SUPPORTED_CONTRACT_VERSION, compatibility_plan: SUPPORTED_COMPATIBILITY_PLAN, active_digest: after.active_digest ?? after.bundle_digest, staged_digest: after.staged_digest, previous_verified_digest: after.previous_verified_digest, operation_id: operationIdValue });
    });
  }

  private adminSyncBarrier(body: Record<string, unknown>, entry: RegistryEntry): Response {
    if (!safeInteger(body.registry_revision) || !safeInteger(body.barrier_epoch)) return genericError(413);
    const current = this.row();
    if (body.registry_revision < current.registry_revision || body.barrier_epoch < current.barrier_epoch) return this.finishError(409);
    this.sql.exec("UPDATE relay_state SET registry_revision=?, barrier_epoch=?, display_owner=?, display_repository=?, updated_at=? WHERE id=1", body.registry_revision, body.barrier_epoch, entry.owner ?? null, entry.repository ?? null, nowSeconds()).toArray();
    const after = this.row();
    this.recordOperation("barrier-sync", after.status, "ok", after, typeof body.operation_id === "string" ? body.operation_id : `barrier-sync-${body.registry_revision}-${body.barrier_epoch}`);
    return response(200, { ok: true, state: after.status, registry_revision: after.registry_revision, barrier_epoch: after.barrier_epoch });
  }

  async fetch(request: Request): Promise<Response> {
    try {
      await this.initialized;
      const entryHeader = request.headers.get("x-relay-registry");
      if (!entryHeader) return genericError(404);
      let entry: RegistryEntry;
      try { entry = JSON.parse(entryHeader) as RegistryEntry; } catch { return genericError(404); }
      const pathname = new URL(request.url).pathname;
      const pathParts = pathname.split("/").filter(Boolean);
      const rawOperation = pathParts.at(-1) ?? "";
      const operation = request.headers.get("x-relay-admin") === "1" && pathParts.at(-3) === "admin"
        ? `admin-${rawOperation}`
        : rawOperation;
      if ((request.method === "GET" || request.method === "HEAD") && pathParts.at(-2) === "read" && (operation === "json" || operation === "svg")) {
        const current = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id = 1").toArray()[0];
        if (current && this.checkRegistryBarrier(request, current, entry) === "stale-caller") return genericError(409);
        return await this.read(request, operation, entry);
      }
      const registryRevision = Number(request.headers.get("x-relay-registry-revision") ?? "1");
      const barrierEpoch = Number(request.headers.get("x-relay-barrier-epoch") ?? "1");
      const current = this.ensureRegistered(entry, Number.isSafeInteger(registryRevision) ? registryRevision : 1, Number.isSafeInteger(barrierEpoch) ? barrierEpoch : 1);
      if (request.headers.get("x-relay-admin") === "1" && operation.startsWith("admin-")) {
        if (operation === "admin-status") {
          if (this.checkRegistryBarrier(request, current, entry) === "stale-caller") return genericError(409);
          return this.adminStatus();
        }
        const body = await readBoundedJson(request);
        if (operation === "admin-sync") return this.adminSyncBarrier(body, entry);
        const barrierResult = this.checkRegistryBarrier(request, current, entry);
        if (barrierResult !== "ok" && request.headers.get("x-relay-registry-tombstoned") !== "true") return genericError(409);
        if (operation === "admin-upgrade" || operation === "admin-rollback") return this.adminUpgrade({ ...body, operation: operation === "admin-rollback" ? "upgrade-rollback" : body.operation ?? "upgrade-stage" });
        if (operation === "admin-invalidate" || operation === "admin-revoke" || operation === "admin-recover-open" || operation === "admin-uninstall") return this.adminMutation(body, operation);
      }
      if (this.checkRegistryBarrier(request, current, entry) !== "ok") return genericError(409);
      if (request.method !== "POST") return genericError(404);
      const body = await readBoundedJson(request);
      const publisher = await this.validatePublisher(request, entry, operation);
      switch (operation) {
        case "prepare": return await this.prepare(body, entry, publisher, "prepare");
        case "publish": return await this.publish(body, entry, publisher, "publish");
        case "renew": return "challenge_id" in body ? await this.publish(body, entry, publisher, "renew") : await this.prepare(body, entry, publisher, "renew");
        case "invalidate": return await this.lifecycle(body, entry, publisher, "invalidate");
        case "revoke": return await this.lifecycle(body, entry, publisher, "revoke");
        case "recover": return await this.publish(body, entry, publisher, "recover");
        default: return genericError(404);
      }
    } catch (error) {
      try {
        const current = this.row();
        const reason: LifecycleReason = error instanceof AuthorizationError && error.status === 409
          ? "expired"
          : error instanceof AuthorizationError
            ? "authorization_failed"
            : error instanceof PayloadError
              ? "quota_exceeded"
              : "storage_unavailable";
        this.state.storage.transactionSync(() => this.recordOperation("diagnostic", current.status, reason, this.row()));
      } catch {
        // Diagnostics must never turn a fixed generic response into a leak.
      }
      if (error instanceof PayloadError || error instanceof AuthorizationError) return genericError(error instanceof AuthorizationError ? error.status : 413);
      return genericError(503);
    }
  }

  /**
   * Internal-only commit seam reserved for the follow-up artifact/context
   * verifier. It is callable by local Durable Object introspection and by a
   * future in-Worker verifier, never by a public HTTP body.
   */
  async commitTrustedPublication(args: {
    body: PublishRequest;
    entry: RegistryEntry;
    jtiHash: string;
    proof: { valid: true; kind?: string; digest?: string; semantic_horizon?: string; tree_sha?: string };
  }): Promise<Response> {
    await this.initialized;
    return this.publish(args.body as unknown as Record<string, unknown>, args.entry, { claims: {}, jti: "", jtiHash: args.jtiHash }, args.body.operation === "renew" ? "renew" : args.body.operation === "recover" ? "recover" : "publish", args.proof);
  }

  /** Mark a restored object unavailable until a fresh proof is committed. */
  async markNeedsRecovery(): Promise<void> {
    await this.initialized;
    this.state.storage.transactionSync(() => {
      this.sql.exec("UPDATE relay_state SET status='needs-recovery', generation=generation+1, revocation_epoch=revocation_epoch+1, payload=NULL, payload_digest=NULL, verified_at=NULL, valid_until=NULL, semantic_horizon=NULL, tree_sha=NULL, tombstoned=0, last_renewed_at=NULL, updated_at=? WHERE id=1", nowSeconds()).toArray();
      this.sql.exec("DELETE FROM relay_challenges WHERE consumed=0").toArray();
      this.recordOperation("recover-open", "needs-recovery", "recovery_required", this.row());
    });
  }
}
