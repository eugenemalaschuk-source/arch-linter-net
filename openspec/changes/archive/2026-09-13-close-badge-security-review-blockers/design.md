## Context

The generated publisher and renewal workflows grant their caller job
`contents: write` and `id-token: write`, so the reusable workflow reference is
an authorization input rather than ordinary configuration. The current parser
only checks its shape. The capability-evidence file has freshness and identity
checks but no authenticated origin, and the OIDC inspector accepts any
three-segment JWT-shaped string after decoding its payload. Finally, lexical
path containment does not stop a pre-created `.github` symlink or Windows
reparse point from redirecting managed output.

## Decisions

1. **Ship one trusted publisher contract for v1.** `workflow_ref`,
   `workflow_sha`, and `action_ref` must either be absent (and receive the
   shipped defaults) or equal the exact `BadgeSetupContract.Default*` values.
   This keeps rotation package/version-bound and prevents an input file from
   choosing a write-capable reusable workflow. `bundle_digest` remains a
   generated Relay-bundle integrity binding, not a publisher selector.

2. **Do not accept unsigned capability assertions.** The current local JSON
   protocol has no authenticated issuer or key lifecycle. Rather than preserve
   a false security claim, v1 rejects `--capability-evidence` as proof and
   falls back only when the option is omitted to the bounded live inspector.
   A future signed evidence protocol must define its issuer, key rotation,
   canonicalization, and revocation before re-enabling file input.

3. **Authenticate OIDC before claim validation.** Live inspection accepts only
   a compact JWT with a JSON header declaring `alg=RS256` and a `kid`, fetches
   the JWKS from the fixed GitHub Actions issuer, imports the matching RSA
   public key, and verifies the exact `header.payload` bytes and signature.
   Claim checks run only after verification; unknown keys, unsupported
   algorithms, malformed keys, and failed verification are unavailable.

4. **Apply the existing containment guard at the write boundary.** Normalize
   the output root and candidate path, require lexical containment, and reject
   any existing reparse/symlink component between the root and candidate. Run
   the same check before the root is created and after parent creation, so
   README generation, conflict capture, temporary writes, and rollback do not
   bypass the guard.

5. **Keep the producer trust root independent of PR contents.** The generated
   producer creates a fixed NuGet configuration with `<clear />` and only the
   approved NuGet.org v3 feed, installs the pinned CLI before checking out PR
   contents, and passes `--configfile` explicitly. A repository `NuGet.Config`
   therefore cannot replace the evaluator used to create trusted evidence.

## Non-goals

- Adding a new signed capability-evidence issuer or package-signature policy.
- Changing the trusted promotion, Relay lifecycle, or OIDC claim contract
  beyond authenticating the token before its existing claim checks.
- Allowing arbitrary publisher/action pin rotation through consumer JSON.
