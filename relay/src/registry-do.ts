import { isOpaqueAlias, registryEntriesFromConfig, validateRegistryEntry } from "./registry";
import { MAX_REQUEST_BYTES, type RegistryEntry, type RelayStateLike } from "./types";

export const REGISTRY_OBJECT_NAME = "__private_registry_v1__";

interface RegistryRow {
  alias: string;
  entry_json: string;
  tombstoned: number;
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
  private readonly initialized: Promise<void>;

  constructor(state: DurableObjectState, env: { RELAY_REGISTRY?: unknown }) {
    this.state = state as RelayStateLike;
    this.sql = this.state.storage.sql;
    this.initialized = this.state.blockConcurrencyWhile(() => {
      this.ensureSchema();
      this.seed(env.RELAY_REGISTRY);
    });
  }

  private ensureSchema(): void {
    this.sql.exec(`CREATE TABLE IF NOT EXISTS relay_registry (
      alias TEXT PRIMARY KEY,
      entry_json TEXT NOT NULL,
      tombstoned INTEGER NOT NULL DEFAULT 0,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL
    )`).toArray();
  }

  private seed(config: unknown): void {
    const now = Math.floor(Date.now() / 1000);
    for (const entry of seedEntries(config)) {
      if (!validateRegistryEntry(entry)) continue;
      // INSERT OR IGNORE is intentional: a seed cannot overwrite an entry or
      // resurrect a tombstone after a cold start or Worker isolate eviction.
      this.sql.exec("INSERT OR IGNORE INTO relay_registry (alias,entry_json,tombstoned,created_at,updated_at) VALUES (?,?,0,?,?)", entry.destination_alias, JSON.stringify(entry), now, now).toArray();
    }
  }

  private row(alias: string): RegistryRow | undefined {
    return this.sql.exec<RegistryRow>("SELECT alias, entry_json, tombstoned FROM relay_registry WHERE alias=? LIMIT 1", alias).toArray()[0];
  }

  async lookup(alias: string): Promise<RegistryEntry | undefined> {
    await this.initialized;
    if (!isOpaqueAlias(alias)) return undefined;
    const row = this.row(alias);
    if (!row || row.tombstoned) return undefined;
    try {
      const entry = JSON.parse(row.entry_json) as RegistryEntry;
      return validateRegistryEntry(entry) ? structuredClone(entry) : undefined;
    } catch { return undefined; }
  }

  async registerEntry(entry: RegistryEntry): Promise<boolean> {
    await this.initialized;
    if (!validateRegistryEntry(entry)) return false;
    return this.state.storage.transactionSync(() => {
      const existing = this.row(entry.destination_alias);
      if (existing) return false;
      const now = Math.floor(Date.now() / 1000);
      this.sql.exec("INSERT INTO relay_registry (alias,entry_json,tombstoned,created_at,updated_at) VALUES (?,?,0,?,?)", entry.destination_alias, JSON.stringify(entry), now, now).toArray();
      return true;
    });
  }

  async revokeAlias(alias: string): Promise<boolean> {
    await this.initialized;
    if (!isOpaqueAlias(alias)) return false;
    return this.state.storage.transactionSync(() => {
      const existing = this.row(alias);
      if (!existing || existing.tombstoned) return false;
      this.sql.exec("UPDATE relay_registry SET tombstoned=1, updated_at=? WHERE alias=? AND tombstoned=0", Math.floor(Date.now() / 1000), alias).toArray();
      return true;
    });
  }

  async fetch(request: Request): Promise<Response> {
    try {
      await this.initialized;
      if (request.headers.get("x-relay-internal") !== "1" || request.method !== "POST") return json(404, { error: "unknown_route" });
      const operation = new URL(request.url).pathname.split("/").filter(Boolean).at(-1);
      const body = await boundedBody(request);
      if (operation === "lookup" && typeof body.alias === "string") {
        const entry = await this.lookup(body.alias);
        return entry ? json(200, { found: true, entry }) : json(404, { found: false });
      }
      if (operation === "register" && validateRegistryEntry(body.entry)) return json(await this.registerEntry(body.entry) ? 201 : 409, { ok: true });
      if (operation === "revoke" && typeof body.alias === "string") return json(await this.revokeAlias(body.alias) ? 200 : 404, { ok: true });
      return json(404, { error: "unknown_route" });
    } catch { return json(503, { error: "storage_unavailable" }); }
  }
}
