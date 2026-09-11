import { MAX_PUBLIC_PAYLOAD_BYTES, type CanonicalPayload, type DisclosureProfile } from "./types";
import { sha256Hex } from "./security";

const HEADLINE_KEYS = ["schemaVersion", "label", "message", "color"] as const;
const FRESHNESS_KEYS = [...HEADLINE_KEYS, "verified_at", "valid_until"] as const;
const MESSAGE = /^(PASS|FAIL) · (HEALTHY|DEBT|DEGRADING|FAILING) · (0|[1-9][0-9]{0,3}) ignores · (0|[1-9][0-9]{0,3}) rules$|^UNASSESSABLE · \? ignores · \? rules$/u;
const COLORS: Record<string, string> = {
  HEALTHY: "brightgreen",
  DEBT: "yellow",
  DEGRADING: "orange",
  FAILING: "red"
};
const UTC_TIMESTAMP = /^20[0-9]{2}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$/u;

export class PayloadError extends Error {
  readonly status = 413;
  constructor() { super("invalid_payload"); }
}

function escapeJsonString(value: string): string {
  let output = "\"";
  for (const character of value) {
    const code = character.codePointAt(0) as number;
    switch (character) {
      case "\"": output += "\\\""; break;
      case "\\": output += "\\\\"; break;
      case "\b": output += "\\b"; break;
      case "\f": output += "\\f"; break;
      case "\n": output += "\\n"; break;
      case "\r": output += "\\r"; break;
      case "\t": output += "\\t"; break;
      default:
        if (code < 0x20 || code > 0x7e) output += `\\u${code.toString(16).padStart(4, "0").toUpperCase()}`;
        else output += character;
    }
  }
  return `${output}\"`;
}

function hasExactKeys(value: Record<string, unknown>, expected: readonly string[]): boolean {
  const keys = Object.keys(value);
  return keys.length === expected.length && keys.every((key, index) => key === expected[index]);
}

function safeDate(value: unknown): Date | undefined {
  if (typeof value !== "string" || !UTC_TIMESTAMP.test(value)) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

/** Serialize the closed payload using the System.Text.Json-compatible wire shape. */
export function canonicalizePayload(payload: CanonicalPayload, profile: DisclosureProfile): string {
  const pairs: string[] = [
    `${escapeJsonString("schemaVersion")}:1`,
    `${escapeJsonString("label")}:${escapeJsonString("architecture")}`,
    `${escapeJsonString("message")}:${escapeJsonString(payload.message)}`,
    `${escapeJsonString("color")}:${escapeJsonString(payload.color)}`
  ];
  if (profile === "headline-plus-freshness/v1") {
    if (!payload.verified_at || !payload.valid_until) throw new PayloadError();
    pairs.push(`${escapeJsonString("verified_at")}:${escapeJsonString(payload.verified_at)}`);
    pairs.push(`${escapeJsonString("valid_until")}:${escapeJsonString(payload.valid_until)}`);
  }
  return `{${pairs.join(",")}}`;
}

export function validateCanonicalPayload(bytes: string, profile: DisclosureProfile): CanonicalPayload {
  if (typeof bytes !== "string" || new TextEncoder().encode(bytes).byteLength > MAX_PUBLIC_PAYLOAD_BYTES) throw new PayloadError();
  let parsed: unknown;
  try { parsed = JSON.parse(bytes); } catch { throw new PayloadError(); }
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) throw new PayloadError();
  const payload = parsed as Record<string, unknown>;
  const expectedKeys = profile === "headline-only/v1" ? HEADLINE_KEYS : FRESHNESS_KEYS;
  if (!hasExactKeys(payload, expectedKeys)) throw new PayloadError();
  if (payload.schemaVersion !== 1 || payload.label !== "architecture" || typeof payload.message !== "string" || payload.message.length > 96 || !MESSAGE.test(payload.message)) throw new PayloadError();
  const health = payload.message === "UNASSESSABLE · ? ignores · ? rules" ? undefined : payload.message.split(" · ")[1];
  const expectedColor = health ? COLORS[health] : "lightgrey";
  if (payload.color !== expectedColor) throw new PayloadError();
  if (profile === "headline-plus-freshness/v1") {
    const verifiedAt = safeDate(payload.verified_at);
    const validUntil = safeDate(payload.valid_until);
    if (!verifiedAt || !validUntil || validUntil.getTime() <= verifiedAt.getTime()) throw new PayloadError();
  }
  if (canonicalizePayload(payload as unknown as CanonicalPayload, profile) !== bytes) throw new PayloadError();
  return payload as unknown as CanonicalPayload;
}

export async function canonicalPayloadDigest(bytes: string): Promise<string> {
  return sha256Hex(new TextEncoder().encode(bytes));
}
