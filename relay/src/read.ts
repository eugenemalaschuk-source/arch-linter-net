import { canonicalPayloadDigest, validateCanonicalPayload } from "./payload";
import { sha256Hex } from "./security";
import { LEASE_SECONDS, MAX_PUBLIC_PAYLOAD_BYTES, type CanonicalPayload, type DisclosureProfile, type RegistryEntry } from "./types";

/** The product-owned unavailable bytes. Keep this byte string stable. */
export const UNAVAILABLE_CANONICAL_BYTES = String.raw`{"schemaVersion":1,"label":"architecture","message":"UNASSESSABLE \u00B7 ? ignores \u00B7 ? rules","color":"lightgrey"}`;

export type PublicRepresentation = "json" | "svg";

export interface PublicReadState {
  status: unknown;
  profile: unknown;
  generation: unknown;
  payload: unknown;
  payload_digest: unknown;
  verified_at: unknown;
  valid_until: unknown;
  semantic_horizon: unknown;
  revocation_epoch: unknown;
  tombstoned: unknown;
}

interface ValidatedReadState {
  profile: DisclosureProfile;
  generation: number;
  payload: CanonicalPayload;
  payloadBytes: string;
  payloadDigest: string;
  verifiedAt: string;
  validUntil: string;
  validUntilSeconds: number;
}

const UTC_TIMESTAMP = /^20\d{2}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/u;
const COLOR_BY_NAME: Record<CanonicalPayload["color"], string> = {
  brightgreen: "#4c1",
  yellow: "#dfb317",
  orange: "#fe7d37",
  red: "#e05d44",
  lightgrey: "#9f9f9f"
};

function timestampSeconds(value: unknown): number | undefined {
  if (typeof value !== "string" || !UTC_TIMESTAMP.test(value)) return undefined;
  const milliseconds = Date.parse(value);
  if (!Number.isFinite(milliseconds)) return undefined;
  const seconds = milliseconds / 1000;
  if (!Number.isSafeInteger(seconds)) return undefined;
  // Date.parse normalizes some impossible calendar dates (for example
  // February 31). Round-tripping the exact UTC wire form rejects those rows
  // as malformed persisted state instead of accidentally serving them.
  return new Date(milliseconds).toISOString().replace(".000Z", "Z") === value ? seconds : undefined;
}

function escapeXml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&apos;");
}

function responseBody(request: Request, body: string | null, status: number, headers: Record<string, string>): Response {
  return new Response(request.method === "HEAD" ? null : body, { status, headers });
}

function unavailableStatus(state: PublicReadState | undefined, storageUncertain = false): number {
  if (storageUncertain) return 503;
  if (state?.status === "revoked" || state?.tombstoned === 1 || state?.tombstoned === true) return 410;
  return 404;
}

function unavailableSvg(): string {
  // This fallback is deliberately derived only from the fixed product-owned
  // unavailable payload. It has no timestamps because no current receipt was
  // validated, and therefore cannot imply bounded freshness.
  const payload = validateCanonicalPayload(UNAVAILABLE_CANONICAL_BYTES, "headline-only/v1");
  return renderSvg(payload, undefined, undefined);
}

function unavailableResponse(request: Request, kind: PublicRepresentation, status = 404): Response {
  const svg = kind === "svg";
  const headers: Record<string, string> = {
    "content-type": svg ? "image/svg+xml; charset=utf-8" : "application/json; charset=utf-8",
    "cache-control": "no-store",
    "x-content-type-options": "nosniff"
  };
  return responseBody(request, svg ? unavailableSvg() : UNAVAILABLE_CANONICAL_BYTES, status, headers);
}

function isConditionalMatch(request: Request, etag: string): boolean {
  const header = request.headers.get("if-none-match");
  if (!header) return false;
  return header.split(",").some((candidate) => {
    const normalized = candidate.trim();
    return normalized === "*" || normalized === etag || normalized.replace(/^W\//u, "") === etag;
  });
}

/**
 * Fixed local rendering. Only values from a validated closed payload are
 * interpolated, and every interpolation is escaped as defense in depth.
 */
function renderSvg(payload: CanonicalPayload, verifiedAt: string | undefined, validUntil: string | undefined): string {
  const message = escapeXml(payload.message);
  const label = escapeXml(payload.label);
  const color = COLOR_BY_NAME[payload.color];
  const freshness = verifiedAt && validUntil
    ? `<text x="10" y="42" fill="#555" font-family="Arial,sans-serif" font-size="10">verified at ${escapeXml(verifiedAt)}</text><text x="10" y="55" fill="#555" font-family="Arial,sans-serif" font-size="10">valid until ${escapeXml(validUntil)}</text>`
    : "";
  const height = freshness ? 66 : 34;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="420" height="${height}" viewBox="0 0 420 ${height}"><rect width="420" height="${height}" rx="3" fill="#fff" stroke="#bbb"/><rect width="112" height="34" rx="3" fill="#555"/><rect x="112" width="308" height="34" rx="3" fill="${color}"/><text x="56" y="22" fill="#fff" font-family="Arial,sans-serif" font-size="12" text-anchor="middle">${label}</text><text x="266" y="22" fill="#fff" font-family="Arial,sans-serif" font-size="12" text-anchor="middle">${message}</text>${freshness}</svg>`;
}

async function validateReadState(state: PublicReadState, entry: RegistryEntry): Promise<ValidatedReadState | undefined> {
  if (state.status !== "ready" || state.tombstoned !== 0) return undefined;
  const profile = state.profile === "headline-only/v1" || state.profile === "headline-plus-freshness/v1" ? state.profile : undefined;
  if (!profile || typeof state.payload !== "string" || typeof state.payload_digest !== "string" || typeof state.verified_at !== "string" || typeof state.valid_until !== "string") return undefined;
  if (profile !== entry.disclosure_profile) return undefined;
  const expectedEpoch = entry.initial_state?.revocation_epoch ?? 1;
  const stateEpoch = typeof state.revocation_epoch === "number" ? state.revocation_epoch : undefined;
  if (!Number.isSafeInteger(expectedEpoch) || stateEpoch === undefined || !Number.isSafeInteger(stateEpoch)) return undefined;
  if (stateEpoch < expectedEpoch) return undefined;
  if (typeof state.semantic_horizon !== "string") return undefined;
  if (state.payload.length === 0 || new TextEncoder().encode(state.payload).byteLength > MAX_PUBLIC_PAYLOAD_BYTES) return undefined;
  const generation = state.generation;
  if (typeof generation !== "number" || !Number.isSafeInteger(generation) || generation <= 0) return undefined;
  const payload = (() => {
    try { return validateCanonicalPayload(state.payload as string, profile); } catch { return undefined; }
  })();
  if (!payload) return undefined;
  const verifiedAtSeconds = timestampSeconds(state.verified_at);
  const validUntilSeconds = timestampSeconds(state.valid_until);
  const semanticHorizonSeconds = timestampSeconds(state.semantic_horizon);
  if (verifiedAtSeconds === undefined || validUntilSeconds === undefined || semanticHorizonSeconds === undefined || validUntilSeconds <= verifiedAtSeconds) return undefined;
  if (validUntilSeconds > semanticHorizonSeconds || validUntilSeconds > verifiedAtSeconds + LEASE_SECONDS) return undefined;
  if (profile === "headline-plus-freshness/v1" && (payload.verified_at !== state.verified_at || payload.valid_until !== state.valid_until)) return undefined;
  const payloadDigest = await canonicalPayloadDigest(state.payload);
  if (!/^[0-9a-f]{64}$/u.test(state.payload_digest) || payloadDigest !== state.payload_digest) return undefined;
  return {
    profile,
    generation,
    payload,
    payloadBytes: state.payload,
    payloadDigest,
    verifiedAt: state.verified_at,
    validUntil: state.valid_until,
    validUntilSeconds
  };
}

/** Build one public response after the state and request-time clock decision. */
export async function readPublicRepresentation(
  request: Request,
  state: PublicReadState | undefined,
  kind: PublicRepresentation,
  entry: RegistryEntry,
  storageUncertain = false
): Promise<Response> {
  if (storageUncertain || !state) return unavailableResponse(request, kind, storageUncertain ? 503 : 404);
  const validated = await validateReadState(state, entry);
  if (!validated) return unavailableResponse(request, kind, unavailableStatus(state));

  // Expiry is intentionally checked before constructing or comparing an ETag.
  const now = Date.now() / 1000;
  if (now >= validated.validUntilSeconds) return unavailableResponse(request, kind, 404);
  if (kind === "svg" && validated.profile !== "headline-plus-freshness/v1") return unavailableResponse(request, kind, 404);

  const representation = kind === "json"
    ? validated.payloadBytes
    : renderSvg(validated.payload, validated.verifiedAt, validated.validUntil);
  const representationDigest = await sha256Hex(
    `badge-relay-public-read/v1|generation=${validated.generation}|state=ready|profile=${validated.profile}|payload=${validated.payloadBytes}|payload_digest=${validated.payloadDigest}|valid_until=${validated.validUntil}|kind=${kind}`
  );
  const etag = `"${representationDigest}"`;
  const remaining = Math.max(0, Math.floor(validated.validUntilSeconds - now));
  const headers: Record<string, string> = {
    "content-type": kind === "json" ? "application/json; charset=utf-8" : "image/svg+xml; charset=utf-8",
    "cache-control": `public, max-age=${remaining}, must-revalidate`,
    etag,
    "x-content-type-options": "nosniff"
  };
  if (isConditionalMatch(request, etag)) return responseBody(request, null, 304, headers);
  return responseBody(request, representation, 200, headers);
}

export function publicUnavailableResponse(request: Request, kind: PublicRepresentation, status = 503): Response {
  return unavailableResponse(request, kind, status);
}
