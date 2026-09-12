import { afterEach, describe, expect, it, vi } from "vitest";
import { canonicalPayloadDigest, canonicalizePayload } from "../src/payload";
import { publicUnavailableResponse, readPublicRepresentation, UNAVAILABLE_CANONICAL_BYTES, type PublicReadState } from "../src/read";
import type { RegistryEntry } from "../src/types";

const headlineBytes = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "PASS · HEALTHY · 0 ignores · 42 rules",
  color: "brightgreen"
}, "headline-only/v1");

const freshnessBytes = canonicalizePayload({
  schemaVersion: 1,
  label: "architecture",
  message: "FAIL · DEBT · 1 ignores · 2 rules",
  color: "yellow",
  verified_at: "2026-09-12T10:00:00Z",
  valid_until: "2026-09-12T11:00:00Z"
}, "headline-plus-freshness/v1");

async function state(bytes: string, profile: "headline-only/v1" | "headline-plus-freshness/v1" = "headline-only/v1"): Promise<PublicReadState> {
  return {
    status: "ready",
    profile,
    generation: 7,
    payload: bytes,
    payload_digest: await canonicalPayloadDigest(bytes),
    verified_at: profile === "headline-plus-freshness/v1" ? "2026-09-12T10:00:00Z" : "2026-09-12T10:00:00Z",
    valid_until: "2026-09-12T11:00:00Z",
    semantic_horizon: "2026-09-12T11:00:00Z",
    revocation_epoch: 1,
    tombstoned: 0
  };
}

const entry = { disclosure_profile: "headline-only/v1", initial_state: { generation: 7, revocation_epoch: 1 } } as unknown as RegistryEntry;
const freshnessEntry = { disclosure_profile: "headline-plus-freshness/v1", initial_state: { generation: 7, revocation_epoch: 1 } } as unknown as RegistryEntry;

describe("public Relay read seam", () => {
  afterEach(() => vi.useRealTimers());

  it("serves exact ready JSON and bounds cache by the remaining lease", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));
    const response = await readPublicRepresentation(new Request("https://relay.test/badge-relay/v1/a7f4k2m9/json"), await state(headlineBytes), "json", entry);
    expect(response.status).toBe(200);
    expect(await response.text()).toBe(headlineBytes);
    expect(response.headers.get("cache-control")).toBe("public, max-age=2700, must-revalidate");
    expect(response.headers.get("x-content-type-options")).toBe("nosniff");
    expect(response.headers.get("etag")).toMatch(/^"[0-9a-f]{64}"$/u);
  });

  it("checks expiry before conditional and HEAD handling", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));
    const request = new Request("https://relay.test/badge-relay/v1/a7f4k2m9/json");
    const first = await readPublicRepresentation(request, await state(headlineBytes), "json", entry);
    const etag = first.headers.get("etag") as string;
    const conditional = await readPublicRepresentation(new Request(request, { headers: { "if-none-match": etag } }), await state(headlineBytes), "json", entry);
    expect(conditional.status).toBe(304);
    const head = await readPublicRepresentation(new Request(request, { method: "HEAD" }), await state(headlineBytes), "json", entry);
    expect(head.status).toBe(200);
    expect(await head.text()).toBe("");
    vi.setSystemTime(new Date("2026-09-12T11:00:00Z"));
    const expired = await readPublicRepresentation(new Request(request, { headers: { "if-none-match": etag } }), await state(headlineBytes), "json", entry);
    expect(expired.status).toBe(404);
    expect(await expired.text()).toBe(UNAVAILABLE_CANONICAL_BYTES);
    expect(expired.headers.get("etag")).toBeNull();
    expect(expired.headers.get("cache-control")).toBe("no-store");
  });

  it("fails closed on corrupt ready state and does not render headline-only as strict SVG", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));
    const corrupt = await state(headlineBytes);
    corrupt.payload_digest = "0".repeat(64);
    const response = await readPublicRepresentation(new Request("https://relay.test"), corrupt, "json", entry);
    expect(response.status).toBe(404);
    expect(await response.text()).toBe(UNAVAILABLE_CANONICAL_BYTES);
    const svg = await readPublicRepresentation(new Request("https://relay.test"), await state(headlineBytes), "svg", entry);
    expect(svg.status).toBe(404);
    expect(svg.headers.get("content-type")).toContain("image/svg+xml");
    expect(await svg.text()).toContain("UNASSESSABLE");
  });

  it("fails closed when registry identity or the validity envelope does not match", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));

    const wrongProfile = await state(headlineBytes, "headline-plus-freshness/v1");
    expect((await readPublicRepresentation(new Request("https://relay.test"), wrongProfile, "json", entry)).status).toBe(404);

    const wrongEpoch = await state(headlineBytes);
    wrongEpoch.revocation_epoch = 0;
    expect((await readPublicRepresentation(new Request("https://relay.test"), wrongEpoch, "json", entry)).status).toBe(404);

    const impossibleEpoch = await state(headlineBytes);
    impossibleEpoch.revocation_epoch = 2;
    expect((await readPublicRepresentation(new Request("https://relay.test"), impossibleEpoch, "json", entry)).status).toBe(404);

    const postInvalidationState = await state(headlineBytes);
    postInvalidationState.generation = 8;
    postInvalidationState.revocation_epoch = 2;
    expect((await readPublicRepresentation(new Request("https://relay.test"), postInvalidationState, "json", entry)).status).toBe(200);

    const beyondHorizon = await state(headlineBytes);
    beyondHorizon.valid_until = "2026-09-12T11:30:00Z";
    expect((await readPublicRepresentation(new Request("https://relay.test"), beyondHorizon, "json", entry)).status).toBe(404);

    const beyondLease = await state(headlineBytes);
    beyondLease.valid_until = "2026-09-12T11:30:00Z";
    beyondLease.semantic_horizon = "2026-09-12T12:00:00Z";
    expect((await readPublicRepresentation(new Request("https://relay.test"), beyondLease, "json", entry)).status).toBe(404);
  });

  it("renders a fixed freshness SVG with safe text and a visible UTC boundary", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));
    const response = await readPublicRepresentation(new Request("https://relay.test"), await state(freshnessBytes, "headline-plus-freshness/v1"), "svg", freshnessEntry);
    const body = await response.text();
    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toContain("image/svg+xml");
    expect(body).toContain("verified at 2026-09-12T10:00:00Z");
    expect(body).toContain("valid until 2026-09-12T11:00:00Z");
    expect(body).not.toMatch(/<script|<foreignObject|<a\b|on[a-z]+=|href=|xlink:href=/iu);
  });

  it("exposes a fixed unavailable response for outer-router storage failures", async () => {
    const response = publicUnavailableResponse(new Request("https://relay.test"), "json");
    expect(response.status).toBe(503);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(await response.text()).toBe(UNAVAILABLE_CANONICAL_BYTES);
  });
});
