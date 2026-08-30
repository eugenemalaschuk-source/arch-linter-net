## 1. Policy filter contract

- [x] 1.1 Add the typed `diagnostic_filter` policy model, root/fragment schema definitions, and
  provenance-aware validation for exact selector values, safe path prefixes, severity mappings,
  and duplicate/stale-match settings.
- [x] 1.2 Add focused policy loader/schema tests proving valid declarations, invalid values, and
  declaration-provenance failures without changing policies that omit the filter.

## 2. Trusted SARIF source projection

- [x] 2.1 Extend the bounded reader result models with immutable typed source diagnostic,
  location, severity, tag, project, and fingerprint facts that can retain #520 provenance.
- [x] 2.2 Parse selected-run source facts from the already bounded and trusted bytes, reject
  malformed consumed fields deterministically, and expose no selectable diagnostics on trust
  failure.
- [x] 2.3 Add focused reader tests for typed result preservation, absent optional source facts,
  rule-tag attachment, source-shape failure, and wrong-context non-selection.

## 3. Deterministic diagnostic selection

- [x] 3.1 Implement the Core selector and selection result models for trusted requirement/result
  pairs, conjunctive filter matching, strict/audit severity mapping, and explicit required-filter
  mismatch evidence.
- [x] 3.2 Implement stable source/fallback fingerprints, canonical identity, ordered provenance
  grouping, and duplicate suppression without display-text or input-order identity.
- [x] 3.3 Add focused NUnit scenarios for source fingerprints, fallback identity, stale filters,
  equivalent repeated runs, distinct locations/contexts, and input-order equivalence.

## 4. Documentation, reviewed API, and integration evidence

- [x] 4.1 Document the `diagnostic_filter` YAML shape, source-field transports, deterministic
  identity/provenance behavior, and explicit non-goals.
- [x] 4.2 Run the reviewed Core public-API update lifecycle and risk-based Core tests, formatter,
  schema/policy, OpenSpec, and architecture checks; resolve issue-related failures.
- [x] 4.3 Synchronize the implementation and specs, mark completed work, archive the OpenSpec
  change, and verify `openspec validate --all` before opening the pull request.
