## 1. Snapshot model and format

- [x] 1.1 Add `PublicApiSnapshotModels.cs` to `Core.Model` (snapshot entry/document, delta kind, delta entry, delta)
- [x] 1.2 Add `PublicApiSnapshotFormat` to `Core.Contracts` with deterministic serialization (ordinal sort, LF, trailing newline, no environment data)
- [x] 1.3 Add parsing with version/directive/ordering validation and entry-count/line-length bounds
- [x] 1.4 Add `PublicApiSignatureIdentity` (kind + qualified name + parameter count, bracket-aware comma counting)
- [x] 1.5 Add `PublicApiSnapshotDiffer` computing added/removed/changed with deterministic ordering

## 2. Contract fields and policy loading

- [x] 2.1 Add `api_snapshot` and `api_comparison` to `ArchitecturePublicApiSurfaceContract`
- [x] 2.2 Add `PublicApiSnapshotResolver` performing repository-local path safety and load-time parse
- [x] 2.3 Resolve snapshots in `ArchitecturePolicyDocumentLoader` after deserialization
- [x] 2.4 Validate `api_comparison` values in `PublicApiSurfaceValidator`
- [x] 2.5 Update `schema/dependencies.arch.schema.json` and `archlinternet.capabilities.json`

## 3. Exact-mode validation and diagnostics

- [x] 3.1 Add capture of the exported surface to `ArchitectureAnalysisSession`
- [x] 3.2 Extend `PublicApiSurfaceChecker` with exact-mode delta violations
- [x] 3.3 Add `ApiDeltaKind`/`PreviousApiSignature` to payload and diagnostic
- [x] 3.4 Expose the delta record in human, JSON, and SARIF output

## 4. Application seam

- [x] 4.1 Add requests/outcomes for capture, diff, update, migrate
- [x] 4.2 Add `IArchitecturePublicApiApplicationService` and its implementation
- [x] 4.3 Run build-state preflight before every operation and fail when blocked
- [x] 4.4 Register in composition root and expose on `ArchitectureEngine`

## 5. CLI

- [x] 5.1 Add `public-api` command module and subcommand modules
- [x] 5.2 Add capture handler with overwrite protection and `--force`
- [x] 5.3 Add diff handler with human/JSON output and drift exit code
- [x] 5.4 Add update handler with `--dry-run` preview
- [x] 5.5 Add migrate handler with drift refusal and `--accept-drift`
- [x] 5.6 Add help text and usage hints

## 6. Documentation

- [x] 6.1 Update `docs/contracts/public-api-surface.md` with snapshot workflow and exact mode
- [x] 6.2 Update `docs/cli/index.md` with the `public-api` command
- [x] 6.3 Archive the OpenSpec change and update main specs

## 7. Tests

- [x] 7.1 Format determinism, round-trip, bounds, and rejection tests
- [x] 7.2 Differ tests (added, removed, changed, overload, enum, constant, ordering)
- [x] 7.3 Resolver path-safety and load-time failure tests
- [x] 7.4 Exact-mode contract tests
- [x] 7.5 Human/JSON/SARIF parity tests
- [x] 7.6 CLI handler tests (capture overwrite, diff drift, update dry-run, migrate drift)
