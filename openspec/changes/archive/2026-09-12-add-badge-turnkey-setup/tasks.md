## 1. Typed setup contract and diagnostics

- [x] 1.1 Add the versioned setup configuration model, closed mode/profile/cadence validation, and stable diagnostic catalog; verify malformed, private-raw, unsupported-version, and cost-bound cases with focused unit tests.
- [x] 1.2 Add repository capability/visibility inspection and a pure setup-plan builder that defaults private repositories to `none`; verify it performs no external calls and reports explicit prerequisites for each supported mode.
- [x] 1.3 Add public-safe versus private doctor projections with stable machine-readable codes and concise fixes; verify token, source identity, provenance, and raw-provider details never appear in public output.

## 2. Deterministic generation and lifecycle safety

- [x] 2.1 Add allowlisted bundle/schema/manifest asset resolution with exact SHA-256 binding and deterministic rendering; verify output is byte-stable and source-checkout independent.
- [x] 2.2 Add managed-block workflow and README patch generation for producer, publisher, optional renewal, and stamped-SVG wiring; verify repeated generation is idempotent and conflicts are detected before writes.
- [x] 2.3 Add atomic setup writer with dry-run, existing-manual-edit preservation, rollback, and partial-failure handling; verify failed writes leave no half-authorized managed state.

## 3. CLI and shipped distribution

- [x] 3.1 Register `badge architecture-health setup` and `badge architecture-health doctor` with explicit help, input/output, dry-run, and format options; verify command parsing and exit semantics through CLI tests.
- [x] 3.2 Ship versioned setup schemas, templates, Relay deployment/migration assets, compatibility metadata, and a manifest in the CLI package; verify packed contents contain every required asset and no consumer source reference.
- [x] 3.3 Keep existing `architecture-health`, `architecture-policy`, public raw, and `none` behavior compatible; verify focused regression suites and public API checks remain clean.

## 4. Consumer documentation and integration evidence

- [x] 4.1 Add executable setup/doctor adoption documentation covering modes, disclosure preview, prerequisites, cost/cadence, expiry, recovery, revoke, cache limits, and manual approvals; verify examples match CLI help and shipped schema identifiers.
- [x] 4.2 Add packed-consumer and conformance tests for clean setup, repeated setup, conflict/partial failure, `none`, public raw, Relay, and redaction paths; verify with the focused packed-artifact test bucket.
- [x] 4.3 Synchronize the archived OpenSpec capability with the implemented behavior and run strict OpenSpec validation; verify the archived spec has Purpose/Requirements and no delta header.
