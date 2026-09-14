import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
import { exportJWK, generateKeyPair, SignJWT } from "jose";
import {
  AuthorizationError,
  clearJwksCacheForTests,
  verifyOidcToken,
  type RegistryEntry
} from "../src/index";

const entry: RegistryEntry = {
  repository_id: 700000042,
  repository_owner_id: 7000000042,
  owner: "synthetic-owner-042",
  repository: "synthetic-repo-042",
  destination_alias: "a7f4k2m9",
  permitted_event: "push",
  permitted_ref: "refs/heads/main",
  job_workflow_ref: "synthetic-owner-042/synthetic-repo-042/.github/workflows/architecture-health-badge.yml@refs/heads/main",
  job_workflow_sha: "2222222222222222222222222222222222222222",
  disclosure_profile: "headline-only/v1",
  audience: "architecture-health-badge-relay-fixture"
};

const jwksUrl = "https://token.actions.githubusercontent.com/.well-known/jwks";

function jwksResponse(keys: Record<string, unknown>[]): Response {
  return new Response(JSON.stringify({ keys }), { headers: { "content-type": "application/json" } });
}

describe("OIDC JWKS refresh protection", () => {
  let privateKey: CryptoKey;
  let publicJwk: Record<string, unknown>;

  beforeEach(async () => {
    clearJwksCacheForTests();
    const keys = await generateKeyPair("RS256");
    privateKey = keys.privateKey;
    publicJwk = await exportJWK(keys.publicKey);
    publicJwk.kid = "local-key";
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function token(kid = "local-key"): Promise<string> {
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
      jti: crypto.randomUUID()
    }).setProtectedHeader({ alg: "RS256", kid }).sign(privateKey);
  }

  it("uses the cached valid key without another provider fetch", async () => {
    const fetcher = vi.fn(async (input: RequestInfo | URL) => {
      expect(String(input)).toBe(jwksUrl);
      return jwksResponse([publicJwk]);
    });

    await verifyOidcToken(await token(), entry, { fetcher });
    await verifyOidcToken(await token(), entry, { fetcher });

    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it("coalesces concurrent unknown-key refreshes", async () => {
    let releaseFetch!: (response: Response) => void;
    const fetcher = vi.fn(() => new Promise<Response>((resolve) => {
      releaseFetch = resolve;
    }));
    const tokens = await Promise.all(Array.from({ length: 32 }, (_, index) => token(`invalid-${index}`)));
    const verifications = Promise.allSettled(tokens.map((value) => verifyOidcToken(value, entry, { fetcher })));

    await Promise.resolve();
    expect(fetcher).toHaveBeenCalledTimes(1);
    releaseFetch(jwksResponse([publicJwk]));

    const results = await verifications;
    expect(results.every((result) => result.status === "rejected")).toBe(true);
    expect(results.every((result) => result.status === "rejected" && result.reason instanceof AuthorizationError)).toBe(true);
  });

  it("suppresses repeated distinct unknown keys during the protection window", async () => {
    const fetcher = vi.fn(async () => jwksResponse([publicJwk]));
    const tokens = await Promise.all(Array.from({ length: 128 }, (_, index) => token(`random-${index}`)));
    const results = await Promise.allSettled(tokens.map((value) => verifyOidcToken(value, entry, { fetcher })));

    expect(results.every((result) => result.status === "rejected")).toBe(true);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it("discovers a rotated provider key after the protection window", async () => {
    const baseTime = Date.now();
    vi.useFakeTimers();
    vi.setSystemTime(baseTime);
    const fetcher = vi.fn(async () => jwksResponse([publicJwk]));

    await verifyOidcToken(await token(), entry, { fetcher });
    const rotated = await generateKeyPair("RS256");
    privateKey = rotated.privateKey;
    publicJwk = await exportJWK(rotated.publicKey);
    publicJwk.kid = "rotated-key";
    const rotatedToken = await token("rotated-key");

    await expect(verifyOidcToken(rotatedToken, entry, { fetcher })).rejects.toBeInstanceOf(AuthorizationError);
    expect(fetcher).toHaveBeenCalledTimes(1);

    vi.setSystemTime(baseTime + 5_001);
    await verifyOidcToken(rotatedToken, entry, { fetcher });
    expect(fetcher).toHaveBeenCalledTimes(2);
  });

  it("fails closed and suppresses repeated provider outages", async () => {
    const fetcher = vi.fn(async () => new Response("unavailable", { status: 503 }));
    const unknown = await token("provider-outage");

    await expect(verifyOidcToken(unknown, entry, { fetcher })).rejects.toThrow("jwks unavailable");
    await expect(verifyOidcToken(unknown, entry, { fetcher })).rejects.toBeInstanceOf(AuthorizationError);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
});
