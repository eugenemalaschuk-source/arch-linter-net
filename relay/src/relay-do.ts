import {
  CHALLENGE_SECONDS,
  LEASE_SECONDS,
  MAX_MANIFEST_BYTES,
  MAX_PUBLIC_PAYLOAD_BYTES,
  MAX_REQUEST_BYTES,
  RENEWAL_MINIMUM_SECONDS,
  type DisclosureProfile,
  type PublishRequest,
  type RegistryEntry,
  type RelayStateLike
} from "./types";
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

  constructor(state: DurableObjectState, _env: unknown) {
    this.state = state as RelayStateLike;
    this.sql = this.state.storage.sql;
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
      last_renewed_at INTEGER
    )`).toArray();
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
  }

  private row(): StateRow {
    const rows = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id = 1").toArray();
    if (rows.length === 0) throw new Error("state not registered");
    return rows[0];
  }

  private ensureRegistered(entry: RegistryEntry): StateRow {
    const existing = this.sql.exec<StateRow>("SELECT * FROM relay_state WHERE id = 1").toArray();
    if (existing.length > 0) return existing[0];
    const generation = entry.initial_state?.generation && entry.initial_state.generation > 0 ? Math.floor(entry.initial_state.generation) : 1;
    const epoch = entry.initial_state?.revocation_epoch && entry.initial_state.revocation_epoch > 0 ? Math.floor(entry.initial_state.revocation_epoch) : 1;
    this.sql.exec(`INSERT INTO relay_state (id,status,profile,generation,revocation_epoch,payload,payload_digest,verified_at,valid_until,semantic_horizon,tree_sha,tombstoned,last_renewed_at)
      VALUES (1,'unavailable',?,?,?,NULL,NULL,NULL,NULL,?,?,0,NULL)`, entry.disclosure_profile, generation, epoch, entry.initial_state?.semantic_horizon ?? null, entry.initial_state?.tree_sha ?? null).toArray();
    return this.row();
  }

  private rollback(): void { /* transactionSync rolls back automatically on throw */ }

  private async validatePublisher(request: Request, entry: RegistryEntry): Promise<import("./types").ValidatedPublisher> {
    const token = getBearerToken(request);
    const audience = entry.audience ?? "";
    return verifyOidcToken(token, entry, { trust: { issuer: "https://token.actions.githubusercontent.com", jwks_uri: "https://token.actions.githubusercontent.com/.well-known/jwks", audience } });
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
    const profile = asProfile(body.profile);
    if (!profile || profile !== entry.disclosure_profile || !boundedString(body.challenge_id, 128) || !boundedString(body.canonical_bytes, MAX_PUBLIC_PAYLOAD_BYTES) || !boundedString(body.canonical_digest, 128) || typeof body.idempotency_key !== "string") throw new PayloadError();
    const payload = validateCanonicalPayload(body.canonical_bytes, profile);
    if (await canonicalPayloadDigest(body.canonical_bytes) !== body.canonical_digest) throw new PayloadError();
    // The artifact/tree/PR proof is deliberately not accepted from HTTP JSON.
    // Only the follow-up publisher verifier may call this internal seam with a
    // typed assertion; until then every HTTP publish fails closed.
    const trustedProof = internalProof;
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
      this.sql.exec(`UPDATE relay_state SET status='ready', profile=?, generation=?, payload=?, payload_digest=?, verified_at=?, valid_until=?, semantic_horizon=?, tombstoned=0, last_renewed_at=?
        WHERE id=1 AND generation=? AND revocation_epoch=? AND tombstoned=0 AND status <> 'revoked'`, profile, newGeneration, body.canonical_bytes, body.canonical_digest, verifiedAt, validUntil, horizon, nowSeconds(), body.expected_generation, body.expected_revocation_epoch).toArray();
      // SQLite UPDATE's result is not portable across the Workers cursor, so
      // re-read the row as the compare-and-set witness.
      const after = this.row();
      if (after.generation !== newGeneration || after.payload_digest !== body.canonical_digest) return this.finishError(409);
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
      return response(200, { ok: true, state: status, generation: after.generation, revocation_epoch: after.revocation_epoch });
      } catch (error) {
        throw error;
      }
    });
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
      const operation = pathParts.at(-1) ?? "";
      if ((request.method === "GET" || request.method === "HEAD") && pathParts.at(-2) === "read" && (operation === "json" || operation === "svg")) {
        return await this.read(request, operation, entry);
      }
      this.ensureRegistered(entry);
      if (request.method !== "POST") return genericError(404);
      const body = await readBoundedJson(request);
      const publisher = await this.validatePublisher(request, entry);
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
    this.sql.exec("UPDATE relay_state SET status='needs-recovery', payload=NULL, payload_digest=NULL WHERE id=1").toArray();
  }
}
