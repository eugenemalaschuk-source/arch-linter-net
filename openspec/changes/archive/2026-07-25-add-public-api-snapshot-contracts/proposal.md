## Why

`public_api_surface` contracts today carry the intended exported surface as an inline `declared_api` YAML list. For a real library that list is hundreds of hand-copied signature strings, so adopting the contract on a large surface is prohibitively expensive, and the check is additions-only: a removed or re-signed public member is invisible.

Issue #94 (parent story #354, origin discussion #353) asks for a reviewed API-snapshot workflow — deterministic capture, a structured diff, a safe update path, and an exact comparison mode — so a large surface is reviewed as a file diff instead of maintained by hand.

## What Changes

- Add a versioned, deterministic, machine-independent public API snapshot text format (`@format`/`@version`/`@contract`/`@assembly` directives, one normalized signature per line, ordinal-sorted, LF-terminated).
- Add `api_snapshot` (repository-local, path-safe, bounded) and `api_comparison: additions_only | exact` to `strict_public_api_surface`/`audit_public_api_surface` contracts. The snapshot is resolved and parsed at policy-load time, so a missing/oversized/escaping/unparsable snapshot fails loudly before any analysis.
- Add exact comparison: additions, removals, and changed signatures become violations, with removals/changes correlated by a signature identity key (kind + qualified name + parameter arity) so a re-signed member reports as one `changed` delta rather than an unrelated add/remove pair.
- Extend `PublicApiSurfacePayload`/`PublicApiSurfaceDiagnostic` with `ApiDeltaKind` and `PreviousApiSignature`, exposed identically in human, `--json` CI artifact, and SARIF output.
- Add a `public-api` CLI command with `capture`, `diff`, `update`, and `migrate` subcommands over a new `IArchitecturePublicApiApplicationService` Core seam.
- `capture` never silently overwrites an existing, differing snapshot (requires `--force`); `update` supports `--dry-run` with a structured diff and a full file preview; `migrate` converts an inline `declared_api` list into a snapshot and refuses to write while drift against the live surface is unacknowledged.
- Inline `declared_api` policies keep working unchanged. Rewriting an inline declaration in place is explicitly refused with actionable guidance, because YAML round-tripping cannot preserve surrounding policy comments safely.
- Build-state preflight (missing / stale / wrong-TFM assemblies) runs before capture, diff, update, and migrate, exactly as it does for validation.

## Capabilities

### New Capabilities
- `public-api-snapshots`: deterministic capture, structured diff, safe update, and inline-list migration of a reviewed public API snapshot file, available through the CLI and the Core application seam.

### Modified Capabilities
- `public-api-surface-contracts`: `api_snapshot` and `api_comparison` fields; exact mode detecting removals and changed signatures; delta-aware diagnostics with human/JSON/SARIF parity.
- `cli-command-dispatch`: new `public-api` top-level command with four subcommands.

## Impact

- `src/ArchLinterNet.Core/Model/PublicApiSnapshotModels.cs` (new) — snapshot/delta records.
- `src/ArchLinterNet.Core/Model/PublicApiSurfacePayload.cs`, `PublicApiSurfaceDiagnostic.cs` — delta fields.
- `src/ArchLinterNet.Core/Contracts/PublicApiSnapshotFormat.cs` (new) — serialize/parse.
- `src/ArchLinterNet.Core/Contracts/PublicApiSnapshotDiffer.cs` (new) — delta computation.
- `src/ArchLinterNet.Core/Contracts/PublicApiSnapshotResolver.cs` (new) — load-time path safety + parse.
- `src/ArchLinterNet.Core/Contracts/Families/PublicApiSurfaceContractFamily.cs` — new fields.
- `src/ArchLinterNet.Core/Contracts/Validators/PublicApiSurfaceValidator.cs` — validate new fields.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs` — resolve snapshots after deserialization.
- `src/ArchLinterNet.Core/Execution/Checkers/PublicApiSurfaceChecker.cs` — exact-mode delta violations.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PublicApiSurface.cs` — surface capture entry point.
- `src/ArchLinterNet.Core/Validation/ArchitecturePublicApiApplicationService.cs` + requests/outcomes (new).
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs`, `ArchitectureSarifFormatter.cs` — delta parity.
- `src/ArchLinterNet.Cli/Commands/PublicApi/*` (new) — command modules and handlers.
- `schema/dependencies.arch.schema.json`, `archlinternet.capabilities.json`, `docs/contracts/public-api-surface.md`, `docs/cli/index.md`.
- Tests: format round-trip/determinism, differ, resolver path safety, exact-mode contract checks, formatter/SARIF parity, CLI handler behavior.
