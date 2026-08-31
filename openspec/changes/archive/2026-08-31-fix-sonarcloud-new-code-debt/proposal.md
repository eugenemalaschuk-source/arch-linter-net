## Why

SonarCloud's New Code Quality Gate on `main` is red (Security Rating E, Reliability Rating C),
blocking the gate `main` self-reports as healthy. Three New Code issues cause it: two release
scripts under `tools/release/` construct filesystem paths from CLI arguments without the
workspace-confinement sanitizer that 8 sibling scripts already adopted in #658 (`main_quality_coverage.py`
Blocker, `verify_restored_main_packages.py` High), and one C# SARIF-rendering method reassigns
a JSON key without an intervening read, which Sonar flags as a suspicious dead-store
(`ReportCoordinator.Rendering.cs` Medium). None of these were touched by #658 itself — they are
debt introduced by commits merged to `main` afterward.

## What Changes

- `tools/release/main_quality_coverage.py`: import and apply the existing `_release_workspace._safe_path`
  confinement helper to every `Path`-typed CLI argument in its 4 subcommand handlers
  (`_canonicalize_shard`, `_assemble`, `_verify_inventory_command`, `_verify_sonar`), matching the
  convention already used by `aggregate_checkpoint_b_evidence.py` and other sibling scripts.
- `tools/release/verify_restored_main_packages.py`: sanitize `--assets` via `_safe_path` in `main()`
  before it reaches `_load_libraries`.
- `tools/release/tests/test_main_quality_coverage.py`: add the standard
  `@pytest.fixture(autouse=True) def _release_workspace(tmp_path, monkeypatch): monkeypatch.chdir(tmp_path)`
  fixture (copied from `test_aggregate_checkpoint_b_evidence.py`) so existing tests, which call the
  handlers directly with `tmp_path`-based paths, keep passing `_safe_path`'s cwd-or-repo-root check.
- `src/ArchLinterNet.Cli/Commands/Validate/Application/ReportCoordinator.Rendering.cs`: remove the
  premature `driver["rules"] = rules;` assignment in `AddApplicabilityFindingsToSarifRun` — it is a
  dead store (nothing reads `driver["rules"]` before the method's final, unconditional
  `driver["rules"] = orderedRules;`), and its removal does not change the method's output.

No product-facing behavior changes: valid inputs produce identical output in all three cases. The
only observable change is that release scripts now reject a path argument that resolves outside the
working tree or repository root, instead of silently constructing it.

## Capabilities

### New Capabilities

- `release-tooling-workspace-confinement`: path-accepting scripts under `tools/release/` SHALL
  confine every `Path`-typed CLI argument to the working tree or repository root before it reaches
  the filesystem, rejecting one that resolves outside with an actionable error instead of silently
  reading or writing there. `main_quality_coverage.py` and `verify_restored_main_packages.py` join
  the 8 sibling scripts that already implement this via `_release_workspace._safe_path`.

### Modified Capabilities

(none — the `ReportCoordinator.Rendering.cs` fix removes a dead JSON-key write with no effect on
observable output, so it changes no existing requirement, including the ordering requirement already
documented under `sarif-diagnostics-output`)

## Impact

- `tools/release/main_quality_coverage.py` and its test file
- `tools/release/verify_restored_main_packages.py`
- `src/ArchLinterNet.Cli/Commands/Validate/Application/ReportCoordinator.Rendering.cs`
- SonarCloud New Code Security Rating and Reliability Rating on `main` (both currently below the
  required `A`)
- No public API, no architecture-governed dependency edges, no CI workflow changes
