import { isOpaqueAlias, registryEntriesFromConfig, validateRegistryEntry } from "./registry";
import { MAX_REQUEST_BYTES, type RegistryEntry, type RelayStateLike } from "./types";
import { isIdentityPreserving, validateDisplayIdentity } from "./lifecycle";

export const REGISTRY_OBJECT_NAME = "__private_registry_v1__";

interface RegistryRow {
  alias: string;
  entry_json: string;
  tombstoned: number;
  revision: number;
  barrier_epoch: number;
  tombstoned_at: number | null;
}

export interface RegistryBinding {
  alias: string;
  entry: RegistryEntry;
  revision: number;
  barrier_epoch: number;
  tombstoned: boolean;
}

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" } });
}

function seedEntries(config: unknown): RegistryEntry[] {
  const entries = registryEntriesFromConfig(config);
  let parsed: unknown = config;
  if (typeof parsed === "string") {
    try { parsed = JSON.parse(parsed); } catch { return entries; }
  }
  if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
    const trust = (parsed as Record<string, unknown>).oidc_trust;
    if (trust && typeof trust === "object" && typeof (trust as Record<string, unknown>).audience === "string") {
      return entries.map((entry) => entry.audience ? entry : { ...entry, audience: (trust as Record<string, unknown>).audience as string });
    }
  }
  return entries;
}

async function boundedBody(request: Request): Promise<Record<string, unknown>> {
  const length = request.headers.get("content-length");
  if (length && (!/^\d+$/u.test(length) || Number(length) > MAX_REQUEST_BYTES)) throw new Error("request too large");
  const bytes = await request.arrayBuffer();
  if (bytes.byteLength > MAX_REQUEST_BYTES) throw new Error("request too large");
  const value: unknown = JSON.parse(new TextDecoder().decode(bytes));
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("invalid request");
  return value as Record<string, unknown>;
}

/**
 * A single fixed-name private registry object. Alias state objects are never
 * used as the registry: tombstones and immutable identity bindings therefore
 * survive Worker isolate eviction and are consulted before idFromName(alias).
 */
export class RelayRegistryDurableObject {
  private readonly state: RelayStateLike;
  private readonly sql: RelayStateLike["storage"]["sql"];
  private initialized!: Promise<void>;

  constructor(state: DurableObjectState, env: { RELAY_REGISTRY?: unknown }) {
    this.state = state as RelayStateLike;
    this.sql = this.state.storage.sql;
    this.startInitialization(env.RELAY_REGISTRY);
  }

  private startInitialization(config: unknown): void {
    this.initialized = this.state.blockConcurrencyWhile(() => {
      this.ensureSchema();
      this.seed(config);
    });
  }

  private ensureSchema(): void {
    this.sql.exec(`CREATE TABLE IF NOT EXISTS relay_registry (
      alias TEXT PRIMARY KEY,
      entry_json TEXT NOT NULL,
      tombstoned INTEGER NOT NULL DEFAULT 0,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      revision INTEGER NOT NULL DEFAULT 1,
      barrier_epoch INTEGER NOT NULL DEFAULT 1,
      tombstoned_at INTEGER
    )`).toArray();
    this.ensureColumn("revision", "INTEGER NOT NULL DEFAULT 1");
    this.ensureColumn("barrier_epoch", "INTEGER NOT NULL DEFAULT 1");
    this.ensureColumn("tombstoned_at", "INTEGER");
  }

  private ensureColumn(name: string, definition: string): void {
    const columns = this.sql.exec<{ name: string }>("PRAGMA table_info(relay_registry)").toArray();
    if (!columns.some((column) => column.name === name)) this.sql.exec(`ALTER TABLE relay_registry ADD COLUMN ${name} ${definition}`).toArray();
  }

  private seed(config: unknown): void {
    const now = Math.floor(Date.now() / 1000);
    for (const entry of seedEntries(config)) {
      if (!validateRegistryEntry(entry)) continue;
      // INSERT OR IGNORE is intentional: a seed cannot overwrite an entry or
      // resurrect a tombstone after a cold start or Worker isolate eviction.
      this.sql.exec("INSERT OR IGNORE INTO relay_registry (alias,entry_json,tombstoned,created_at,updated_at,revision,barrier_epoch,tombstoned_at) VALUES (?,?,0,?,?,1,1,NULL)", entry.destination_alias, JSON.stringify(entry), now, now).toArray();
    }
  }

  private row(alias: string): RegistryRow | undefined {
    return this.sql.exec<RegistryRow>("SELECT alias, entry_json, tombstoned, revision, barrier_epoch, tombstoned_at FROM relay_registry WHERE alias=? LIMIT 1", alias).toArray()[0];
  }

  private parseBinding(row: RegistryRow): RegistryBinding | undefined {
    try {
      const entry = JSON.parse(row.entry_json) as RegistryEntry;
      if (!validateRegistryEntry(entry)) return undefined;
      return { alias: row.alias, entry: structuredClone(entry), revision: row.revision, barrier_epoch: row.barrier_epoch, tombstoned: row.tombstoned !== 0 };
    } catch { return undefined; }
  }

  async binding(alias: string, includeTombstoned = false): Promise<RegistryBinding | undefined> {
    await this.initialized;
    if (!isOpaqueAlias(alias)) return undefined;
    const row = this.row(alias);
    if (!row || (!includeTombstoned && row.tombstoned)) return undefined;
    return this.parseBinding(row);
  }

  async lookup(alias: string): Promise<RegistryEntry | undefined> {
    await this.initialized;
    if (!isOpaqueAlias(alias)) return undefined;
    return (await this.binding(alias))?.entry;
  }

  async registerEntry(entry: RegistryEntry): Promise<boolean> {
    await this.initialized;
    if (!validateRegistryEntry(entry)) return false;
    return this.state.storage.transactionSync(() => {
      const existing = this.row(entry.destination_alias);
      if (existing) return false;
      const now = Math.floor(Date.now() / 1000);
      this.sql.exec("INSERT INTO relay_registry (alias,entry_json,tombstoned,created_at,updated_at,revision,barrier_epoch,tombstoned_at) VALUES (?,?,0,?,?,1,1,NULL)", entry.destination_alias, JSON.stringify(entry), now, now).toArray();
      return true;
    });
  }

  async reconcileIdentity(alias: string, owner: string, repository: string, repositoryId: number, ownerId: number, expectedRevision?: number, expectedBarrierEpoch?: number): Promise<RegistryBinding | undefined> {
    await this.initialized;
    if (!isOpaqueAlias(alias) || !validateDisplayIdentity(owner) || !validateDisplayIdentity(repository)) return undefined;
    return this.state.storage.transactionSync(() => {
      const existing = this.row(alias);
      const binding = existing && this.parseBinding(existing);
      if (!binding || binding.tombstoned || !Number.isSafeInteger(expectedRevision ?? binding.revision) || (expectedRevision !== undefined && expectedRevision !== binding.revision) || (expectedBarrierEpoch !== undefined && expectedBarrierEpoch !== binding.barrier_epoch)) return undefined;
      if (!isIdentityPreserving({ repository_id: binding.entry.repository_id, repository_owner_id: binding.entry.repository_owner_id }, { repository_id: repositoryId, repository_owner_id: ownerId })) return undefined;
      const updated: RegistryEntry = { ...binding.entry, owner, repository };
      const revision = binding.revision + 1;
      this.sql.exec("UPDATE relay_registry SET entry_json=?, revision=?, updated_at=? WHERE alias=? AND tombstoned=0 AND revision=?", JSON.stringify(updated), revision, Math.floor(Date.now() / 1000), alias, binding.revision).toArray();
      return this.parseBinding(this.row(alias) as RegistryRow);
    });
  }

  async rotateEntry(alias: string, entry: RegistryEntry, expectedRevision?: number, expectedBarrierEpoch?: number): Promise<RegistryBinding | undefined> {
    await this.initialized;
    if (!isOpaqueAlias(alias) || !validateRegistryEntry(entry) || entry.destination_alias !== alias) return undefined;
    return this.state.storage.transactionSync(() => {
      const existing = this.row(alias);
      const binding = existing && this.parseBinding(existing);
      if (!binding || binding.tombstoned || (expectedRevision !== undefined && expectedRevision !== binding.revision) || (expectedBarrierEpoch !== undefined && expectedBarrierEpoch !== binding.barrier_epoch)) return undefined;
      if (!isIdentityPreserving(binding.entry, entry)) return undefined;
      const revision = binding.revision + 1;
      const barrier = binding.barrier_epoch + 1;
      this.sql.exec("UPDATE relay_registry SET entry_json=?, revision=?, barrier_epoch=?, updated_at=? WHERE alias=? AND tombstoned=0 AND revision=?", JSON.stringify(entry), revision, barrier, Math.floor(Date.now() / 1000), alias, binding.revision).toArray();
      return this.parseBinding(this.row(alias) as RegistryRow);
    });
  }

  async revokeAlias(alias: string, expectedRevision?: number, expectedBarrierEpoch?: number): Promise<boolean | "conflict"> {
    await this.initialized;
    if (!isOpaqueAlias(alias)) return false;
    return this.state.storage.transactionSync(() => {
      const existing = this.row(alias);
      if (!existing) return false;
      if ((expectedRevision !== undefined && expectedRevision !== existing.revision)
        || (expectedBarrierEpoch !== undefined && expectedBarrierEpoch !== existing.barrier_epoch)) return "conflict";
      if (existing.tombstoned) return true;
      const now = Math.floor(Date.now() / 1000);
      this.sql.exec("UPDATE relay_registry SET tombstoned=1, tombstoned_at=?, revision=revision+1, barrier_epoch=barrier_epoch+1, updated_at=? WHERE alias=? AND tombstoned=0 AND revision=? AND barrier_epoch=?", now, now, alias, existing.revision, existing.barrier_epoch).toArray();
      return true;
    });
  }

  private async handleLookup(body: Record<string, unknown>): Promise<Response | undefined> {
    if (typeof body.alias !== "string") return undefined;
    const binding = await this.binding(body.alias, body.include_tombstoned === true);
    return binding ? json(200, { found: true, ...binding }) : json(404, { found: false });
  }

  private async handleRegister(body: Record<string, unknown>): Promise<Response | undefined> {
    if (!validateRegistryEntry(body.entry)) return undefined;
    return json(await this.registerEntry(body.entry) ? 201 : 409, { ok: true });
  }

  private async handleRevoke(body: Record<string, unknown>): Promise<Response | undefined> {
    if (typeof body.alias !== "string") return undefined;
    const result = await this.revokeAlias(
      body.alias,
      Number.isSafeInteger(body.expected_revision) ? body.expected_revision as number : undefined,
      Number.isSafeInteger(body.expected_barrier_epoch) ? body.expected_barrier_epoch as number : undefined);
    let status: number;
    if (result === "conflict") status = 409;
    else if (result) status = 200;
    else status = 404;
    return json(status, { ok: result === true });
  }

  private async handleReconcileIdentity(body: Record<string, unknown>): Promise<Response | undefined> {
    if (typeof body.alias !== "string" || typeof body.owner !== "string" || typeof body.repository !== "string" || !Number.isSafeInteger(body.repository_id) || !Number.isSafeInteger(body.repository_owner_id)) return undefined;
    const binding = await this.reconcileIdentity(body.alias, body.owner, body.repository, body.repository_id as number, body.repository_owner_id as number, Number.isSafeInteger(body.expected_revision) ? body.expected_revision as number : undefined, Number.isSafeInteger(body.expected_barrier_epoch) ? body.expected_barrier_epoch as number : undefined);
    return binding ? json(200, { ok: true, ...binding }) : json(409, { error: "identity_mismatch" });
  }

  private async handleRotate(body: Record<string, unknown>): Promise<Response | undefined> {
    if (typeof body.alias !== "string" || !validateRegistryEntry(body.entry)) return undefined;
    const binding = await this.rotateEntry(body.alias, body.entry, Number.isSafeInteger(body.expected_revision) ? body.expected_revision as number : undefined, Number.isSafeInteger(body.expected_barrier_epoch) ? body.expected_barrier_epoch as number : undefined);
    return binding ? json(200, { ok: true, ...binding }) : json(409, { error: "rotation_conflict" });
  }

  private async dispatchOperation(operation: string | undefined, body: Record<string, unknown>): Promise<Response | undefined> {
    if (operation === "lookup") return this.handleLookup(body);
    if (operation === "register") return this.handleRegister(body);
    if (operation === "revoke") return this.handleRevoke(body);
    if (operation === "reconcile-identity") return this.handleReconcileIdentity(body);
    if (operation === "rotate") return this.handleRotate(body);
    return undefined;
  }

  async fetch(request: Request): Promise<Response> {
    try {
      await this.initialized;
      if (request.headers.get("x-relay-internal") !== "1" || request.method !== "POST") return json(404, { error: "unknown_route" });
      const pathParts = new URL(request.url).pathname.split("/").filter(Boolean);
      const operation = pathParts.at(-1);
      const body = await boundedBody(request);
      const response = await this.dispatchOperation(operation, body);
      return response ?? json(404, { error: "unknown_route" });
    } catch { return json(503, { error: "storage_unavailable" }); }
  }
}
