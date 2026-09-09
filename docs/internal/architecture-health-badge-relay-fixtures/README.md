# Architecture Health Badge Relay fixtures

These versioned fixtures are synthetic conformance inputs for the planned
adopter-owned Badge Relay. They describe the contract only; they do not contain
relay code, a real adopter identity, a secret, or a JWT.

## Files

- `relay-fixtures.schema.json` is the JSON Schema Draft 7 suite. It constrains
  the safe v1 configuration, closed public payload dictionary, canonical
  headline-only and freshness representations, and vector envelopes.
- `reference-config.json` is a valid relay configuration. Its repository and
  owner IDs, alias, workflow reference, and workflow SHA are deliberately
  synthetic. The workflow reference and SHA are exact registry pins.
- `canonical-ready.json` and `canonical-unavailable.json` are the
  headline-only public representations. `canonical-freshness.json` is the
  corresponding exact-byte golden representation for
  `headline-plus-freshness/v1`. `canonical_bytes` is the byte string that a
  transport verifies and copies; its SHA-256 is the digest of those exact
  bytes. Both profiles retain default `System.Text.Json` escaping (including
  `\\u00B7`), fixed key order, and no provenance, URL, SHA, PR, run, or token
  data.
- `conformance-vectors.json` covers accepted bytes and malformed/disclosing
  bytes, OIDC claim and pin checks, replay/idempotency and CAS ordering,
  deadlines, expiry, semantic horizons, lifecycle/recovery, and cache/ETag/
  HEAD behavior. Protected JOSE headers (`alg`, `kid`) are separate from signed
  claims (`nbf`, `iat`, `exp`); `kid` selects a key only from the fixed GitHub
  JWKS endpoint, with at most one bounded refresh for an unknown key. The
  registry permits only `push` on `refs/heads/main`.
  `expected.public_response` is deliberately public-safe.

The relay never reconstructs Gate, Health, counts, message, or color. The
`headline-plus-freshness/v1` profile adds only `verified_at` and `valid_until`.
Freshness is bounded by the 60-minute lease, optional 30-minute renewal, and
the semantic horizon; OIDC validation alone has a five-minute clock skew.

## Checks

Every JSON file must parse, including the schema itself:

```bash
for file in docs/internal/architecture-health-badge-relay-fixtures/*.json; do
  python3 -m json.tool "$file" >/dev/null || exit 1
done
```

When a JSON Schema validator is available, validate `reference-config.json`,
both canonical representations, and `conformance-vectors.json` against
`relay-fixtures.schema.json`. JSON object order is not a schema property, so
canonical byte equality must additionally be checked against each
`canonical_bytes` string and its recorded `sha256`.
