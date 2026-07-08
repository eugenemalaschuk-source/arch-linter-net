## Why

Baseline files today can only be generated from scratch (`baseline generate`). Once a baseline exists, keeping it in sync with a changing codebase — adding new debt, dropping resolved debt, and confirming nothing has drifted — requires hand-editing the YAML or blowing it away and regenerating (which discards `reason` metadata on entries that are still valid). Issue #63 asks for first-class lifecycle commands so baselines stay narrow, deterministic, and reviewable over time without manual YAML surgery.

## What Changes

- Add `baseline update`: regenerates a baseline against current violations while preserving the `reason` field verbatim on entries that still match a current violation, and adding new entries deterministically for new violations.
- Add `baseline prune`: removes baseline entries that no longer match any current violation (resolved debt) or reference an unknown contract id (configuration error), writing the pruned file and reporting exactly what was removed and why.
- Add `baseline diff`: read-only comparison of a baseline against current violations, categorizing results into new debt, existing (frozen) debt, resolved debt, and configuration errors. Does not write any file.
- Add `baseline verify`: read-only CI gate that exits non-zero if the baseline is out of sync (contains resolved entries or configuration errors), exits 0 if in sync. Does not write any file.
- Extend `baseline generate` with a `--contract <id>` (repeatable) flag so all five `baseline` subcommands consistently support `--policy`/`--config`, `--mode` (strict/audit/all), `--condition-set`, and `--contract`.
- All new/changed behavior stays backward compatible with the existing baseline file format, `ignored_violations` schema, and `validate --baseline` consumption path.

## Capabilities

### New Capabilities
(none — this extends the existing `baseline-generation` capability rather than introducing a new one)

### Modified Capabilities
- `baseline-generation`: adds `baseline update`, `baseline prune`, `baseline diff`, and `baseline verify` CLI subcommands and their behaviors, and adds `--contract` selected-contract filtering to `baseline generate` and the new subcommands.

## Impact

- `src/ArchLinterNet.Cli/Program.cs`: extend `RunBaselineCommand` to dispatch on `update`/`prune`/`diff`/`verify` sub-verbs (in addition to the existing `generate`), each with its own flag parsing, help text, and exit-code behavior.
- `src/ArchLinterNet.Core/Validation/`: new request/outcome records (e.g. `BaselineUpdateRequest`, `BaselinePruneRequest`, `BaselineDiffRequest`, `BaselineVerifyRequest` and matching outcomes) and new methods on `IArchitectureBaselineApplicationService` (or a sibling service) implementing update/prune/diff/verify semantics.
- `src/ArchLinterNet.Core/Contracts/`: baseline loading/merging code (`ArchitectureBaselineLoadingService`, `ArchitectureBaselineGenerator`) reused/extended to support comparing existing entries against current candidates instead of only generating fresh ones.
- `src/ArchLinterNet.Core/Composition/ArchitectureEngine.cs`: new entry points alongside `GenerateBaseline`.
- Tests: `tests/ArchLinterNet.Core.Tests` (new application-service/unit tests per verb) and `tests/ArchLinterNet.Cli.Tests` (new `CliIntegrationTests.Baseline*.cs` files) covering update, prune, diff, verify, `--condition-set`, `--contract`, and stale-ignore scenarios.
- Docs: `docs/guides/migration-baselines.md` and `docs/cli/index.md` updated to describe the new subcommands.
- No changes to the baseline file format, JSON schema keys, or `validate --baseline` merge behavior — fully backward compatible.
