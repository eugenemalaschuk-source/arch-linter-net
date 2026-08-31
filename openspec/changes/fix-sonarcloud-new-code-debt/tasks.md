## 1. Python — main_quality_coverage.py workspace confinement

- [x] 1.1 Import `_safe_path` from `_release_workspace` in `tools/release/main_quality_coverage.py`.
- [x] 1.2 In `_canonicalize_shard`, sanitize `arguments.coverage_root` and `arguments.output_root`
      into local variables and replace all their downstream usages in the function.
- [x] 1.3 In `_assemble`, sanitize `arguments.artifacts_root` and `arguments.output_root` into local
      variables (and `arguments.github_output` when not `None`) and replace all their downstream
      usages in the function.
- [x] 1.4 In `_verify_inventory_command`, sanitize `arguments.inventory_root` (and
      `arguments.github_output` when not `None`) into local variables and replace their downstream
      usages.
- [x] 1.5 In `_verify_sonar`, sanitize `arguments.inventory_root`, `arguments.scanner_log`, and
      `arguments.analysis_json` (and `arguments.github_output` when not `None`) into local variables
      and replace their downstream usages.
- [x] 1.6 Add the `@pytest.fixture(autouse=True) def _release_workspace(tmp_path, monkeypatch):
      monkeypatch.chdir(tmp_path)` fixture to `tools/release/tests/test_main_quality_coverage.py`
      (copy from `test_aggregate_checkpoint_b_evidence.py`).
- [x] 1.7 Run `tools/release/tests/test_main_quality_coverage.py` and confirm every existing test
      still passes unmodified.

## 2. Python — verify_restored_main_packages.py workspace confinement

- [x] 2.1 Import `_safe_path` from `_release_workspace` in
      `tools/release/verify_restored_main_packages.py`.
- [x] 2.2 In `main()`, sanitize `arguments.assets` into a local variable inside the existing
      `try`/`except ValueError` block, before calling `verify_restored_main_packages(...)`.
- [x] 2.3 Run `tools/release/tests/test_verify_restored_main_packages.py` and confirm it still passes
      unmodified (it calls `verify_restored_main_packages()` directly and should need no changes).

## 3. C# — ReportCoordinator.Rendering.cs dead-store removal

- [x] 3.1 In `AddApplicabilityFindingsToSarifRun`
      (`src/ArchLinterNet.Cli/Commands/Validate/Application/ReportCoordinator.Rendering.cs`), delete
      the premature `driver["rules"] = rules;` statement, keeping the final
      `driver["rules"] = orderedRules;` unconditional assignment.
- [x] 3.2 Run the existing SARIF-rendering tests covering `ReportCoordinator`/applicability findings
      and confirm output is unchanged.

## 4. Validation and delivery

- [x] 4.1 Run `make fmt`.
- [x] 4.2 Run `make acceptance` (lint + all tests) and confirm it is clean.
- [x] 4.3 Commit the change and open a pull request.
- [ ] 4.4 Run `openspec archive fix-sonarcloud-new-code-debt` and `openspec validate --all` once the
      change is merged (or per repository OpenSpec convention for this branch).
