## Why

Baseline matching today keys on the display-shaped pair `(source_type, forbidden_reference)` — for dependency-style contracts this is an unqualified type name (colliding across assemblies), and for method-body/call contracts it embeds the source line number as the only occurrence discriminator. A single baseline entry can therefore silently suppress more than one real violation (or stop suppressing the intended one after an unrelated line shift), which breaks the safety guarantee of an architecture ratchet: known debt must stay suppressed while every additional occurrence must be reported as new. Issue #357 (P0 correctness) requires a versioned, structured violation identity and a deterministic migration path for existing baseline files.

## What Changes

- Add a new versioned, structured `ArchitectureViolationIdentity` model that captures contract family/kind, contract id, source/target assembly, type and member, and a stable non-line-based occurrence discriminator.
- **BREAKING (opt-in via version)**: introduce baseline document `version: 2` using structured identity per `ignored_violation`; `version: 1` (legacy flat pair) continues to be loaded and matched with its existing exact-string-pair semantics — no silent reinterpretation of existing files.
- Rework `ArchitectureBaselineComparer`, `ArchitectureBaselineGenerator`, and `ArchitectureBaselineLoadingService` merge/dedup logic to be version-aware: v1 entries match via the legacy pair key, v2 entries match via full structured-identity equality.
- Qualify dependency-style contract checks and method-body/call contracts with source/target assembly and target member/overload so same-named types in different assemblies and distinct call occurrences in one type never collide. Other baseline-capable families keep legacy-shaped (family + contract id + source/target type only) v2 identity in this change — full qualification for every family is out of scope (see Non-goals in design.md).
- Add a new `baseline migrate` CLI subcommand that deterministically upgrades a `version: 1` baseline file to `version: 2` by correlating each legacy entry against freshly collected candidates: one match migrates, zero matches is reported stale, more than one match is ambiguous and fails closed. Supports `--dry-run`/`--check`, never overwrites the source file, and requires an explicit `--output` for a real run.
- Extend `diff`/`verify` (and the new `migrate`) status reporting so `--json` output exposes `new`/`matched`/`stale`/`ambiguous` as a structured `status` field per entry, not just prose section headers. (Baseline comparison has no existing SARIF output or Testing API surface to extend — see design.md.)
- Update `schema/baseline.schema.json` to accept both `version: 1` and `version: 2` shapes, and update the baseline guide and AI guidance docs.

## Capabilities

### New Capabilities

(none — this extends the existing `baseline-generation` capability rather than introducing a separate one, since identity is the mechanism baseline generation/comparison already own)

### Modified Capabilities

- `baseline-generation`: baseline entry identity becomes versioned and structured instead of an exact `(source_type, forbidden_reference)` pair; adds the `baseline migrate` command; adds structured new/matched/stale/ambiguous status to diff/verify output.

## Impact

- `src/ArchLinterNet.Core/Model/ArchitectureBaselineComparisonEntry.cs`, `Contracts/ArchitectureBaselineModels.cs`, `Contracts/ArchitectureBaselineComparer.cs`, `Contracts/ArchitectureBaselineGenerator.cs`, `Contracts/ArchitectureBaselineLoadingService.cs`, `Validation/ArchitectureBaselineApplicationService.cs` and related request/outcome records.
- `src/ArchLinterNet.Core/Resolution/ArchitectureContractExecutionContext.cs` and the dependency-check / method-body scanner call sites that feed it.
- `src/ArchLinterNet.Cli/Commands/Baseline/*` (new `migrate` subcommand + handler).
- `schema/baseline.schema.json`, `docs/guides/migration-baselines.md`, relevant `docs/ai/*.md` files.
- Test suites under `tests/ArchLinterNet.Core.Tests/` and `tests/ArchLinterNet.Cli.Tests/` covering baseline comparison, generation, loading, merge, round-trip, and CLI integration.
