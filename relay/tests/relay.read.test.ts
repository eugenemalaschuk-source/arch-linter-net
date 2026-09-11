import { afterEach, describe, expect, it, vi } from "vitest";
import { canonicalPayloadDigest, canonicalizePayload } from "../src/payload";
import { readPublicRepresentation, UNAVAILABLE_CANONICAL_BYTES, type PublicReadState } from "../src/read";

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
    tombstoned: 0
  };
}

describe("public Relay read seam", () => {
  afterEach(() => vi.useRealTimers());

  it("serves exact ready JSON and bounds cache by the remaining lease", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));
    const response = await readPublicRepresentation(new Request("https://relay.test/badge-relay/v1/a7f4k2m9/json"), await state(headlineBytes), "json");
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
    const first = await readPublicRepresentation(request, await state(headlineBytes), "json");
    const etag = first.headers.get("etag") as string;
    const conditional = await readPublicRepresentation(new Request(request, { headers: { "if-none-match": etag } }), await state(headlineBytes), "json");
    expect(conditional.status).toBe(304);
    const head = await readPublicRepresentation(new Request(request, { method: "HEAD" }), await state(headlineBytes), "json");
    expect(head.status).toBe(200);
    expect(await head.text()).toBe("");
    vi.setSystemTime(new Date("2026-09-12T11:00:00Z"));
    const expired = await readPublicRepresentation(new Request(request, { headers: { "if-none-match": etag } }), await state(headlineBytes), "json");
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
    const response = await readPublicRepresentation(new Request("https://relay.test"), corrupt, "json");
    expect(response.status).toBe(404);
    expect(await response.text()).toBe(UNAVAILABLE_CANONICAL_BYTES);
    const svg = await readPublicRepresentation(new Request("https://relay.test"), await state(headlineBytes), "svg");
    expect(svg.status).toBe(404);
    expect(svg.headers.get("content-type")).toContain("image/svg+xml");
    expect(await svg.text()).toContain("UNASSESSABLE");
  });

  it("renders a fixed freshness SVG with safe text and a visible UTC boundary", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T10:15:00Z"));
    const response = await readPublicRepresentation(new Request("https://relay.test"), await state(freshnessBytes, "headline-plus-freshness/v1"), "svg");
    const body = await response.text();
    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toContain("image/svg+xml");
    expect(body).toContain("verified at 2026-09-12T10:00:00Z");
    expect(body).toContain("valid until 2026-09-12T11:00:00Z");
    expect(body).not.toMatch(/<script|<foreignObject|<a\b|on[a-z]+=|href=|xlink:href=/iu);
  });
});
