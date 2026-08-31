## Context

Three SonarCloud New Code issues on `main` (confirmed via GitHub check-run output and the SonarCloud
issues list) fail the Quality Gate:

1. **Security/Blocker** — `tools/release/main_quality_coverage.py:118`, inside `_write_json`: writes
   to a caller-supplied `path` without validation.
2. **Security/High** — `tools/release/verify_restored_main_packages.py:21`, inside `_load_libraries`:
   reads a caller-supplied `assets_path` without validation.
3. **Reliability/Medium** — `ReportCoordinator.Rendering.cs:402`: `driver["rules"] = orderedRules;`
   reassigns a JSON key already set earlier in the same method without an intervening read.

`tools/release/_release_workspace.py` already exists (added by #658) with a `_safe_path(value, description)`
helper that confines a path to `Path.cwd().resolve()` or the repository root, raising `ValueError`
otherwise. 8 of 10 scripts in `tools/release/` already call it at their CLI entry point; the two flagged
here are the ones that were missed.

## Goals / Non-Goals

**Goals:**
- Bring both flagged Python scripts up to the same `_safe_path` confinement convention already used
  throughout `tools/release/`, closing the taint path Sonar's Python security rule traces from each
  CLI argument to its filesystem sink.
- Remove the dead-store JSON key reassignment in the C# SARIF renderer.
- Land all three fixes with zero behavior change for valid inputs — every existing test must pass
  unmodified in its assertions (only the fixture plumbing needed for `_safe_path` to accept
  `tmp_path`-rooted test fixtures changes).

**Non-Goals:**
- Auditing every other script in `tools/release/` for the same gap (only the two Sonar actually
  flagged are in scope).
- Any refactor of `AddApplicabilityFindingsToSarifRun`'s SARIF-building logic beyond the one dead
  line.
- Introducing a new shared abstraction for C# JSON-node building; the C# fix is a deletion, not a
  new pattern.

## Decisions

**Sanitize at the same call-graph depth the existing convention uses, not uniformly at `main()`.**
`main_quality_coverage.py`'s 4 subcommand handlers (`_canonicalize_shard`, `_assemble`,
`_verify_inventory_command`, `_verify_sonar`) are called directly by `tests/test_main_quality_coverage.py`
with hand-built `argparse.Namespace` objects, bypassing `main()`/argparse entirely — so sanitizing only
in `main()` would leave the tested code paths (and therefore Sonar's traced sink) unsanitized.
`_safe_path` is applied inside each of the 4 handlers instead, mirroring exactly where
`aggregate_checkpoint_b_evidence.py` sanitizes (right after argument access, before first filesystem
use), just one level down because of this file's dispatch shape.
`verify_restored_main_packages.py`'s only test entry point is `verify_restored_main_packages()` itself,
called directly and bypassing `main()` — so sanitizing in `main()` is sufficient and requires no test
changes, matching how `aggregate_checkpoint_b_evidence.py` sanitizes only in `main()` without its
lower-level `_read_manifest`/`_read_records` helpers doing it again.

**Reuse the existing `_release_workspace` test fixture pattern rather than inventing a new one.**
`test_aggregate_checkpoint_b_evidence.py` and `test_package_manifest.py` both already define
`@pytest.fixture(autouse=True) def _release_workspace(tmp_path, monkeypatch): monkeypatch.chdir(tmp_path)`
so `_safe_path`'s cwd-based allow-list accepts `tmp_path`-rooted fixtures. Copying this exact fixture
into `test_main_quality_coverage.py` is the minimal, precedented fix — no changes to individual test
bodies.

**Delete the dead store rather than restructure the method.**
In `AddApplicabilityFindingsToSarifRun`, `driver["rules"] = rules;` (right after
`JsonArray rules = driver["rules"] as JsonArray ?? new JsonArray();`) is read nowhere before the
method's unconditional final `driver["rules"] = orderedRules;`. `JsonArray` items only need their
containing array (`rules`) as a parent for `DeepClone()` to work in the reordering loop — they do not
need `rules` itself attached to `driver` first. Deleting the premature assignment removes the
Sonar-flagged pattern with a one-line diff and no behavior change, instead of restructuring the method
to build `orderedRules` before ever touching `driver`.

## Risks / Trade-offs

- [Adding `_safe_path` to `main_quality_coverage.py` handlers could reject a legitimate CI-invoked path
  if `cwd` differs from what CI expects] → CI always invokes `tools/release/*.py` from the repository
  root (per `Makefile`/workflow usage), which is one of `_safe_path`'s two allowed roots regardless of
  cwd; sibling scripts already run under the same CI invocation shape with `_safe_path` applied.
- [Forgetting to add the `monkeypatch.chdir(tmp_path)` fixture would break every existing test in
  `test_main_quality_coverage.py` with a workspace-confinement `ValueError`] → mitigated by copying the
  exact fixture already proven in two sibling test files, and by running `make acceptance` before
  committing.
- [Sonar's Python taint analysis might not recognize `_safe_path` as a sanitizer and re-flag the same
  lines] → mitigated by low risk: this is the exact helper and call shape that already closed identical
  Blocker/High findings for 8 other scripts, verified by `git log`/`grep` before writing this design.

## Migration Plan

No migration — this is a same-release bugfix. Land as one PR; no rollback concerns beyond a normal
revert (no schema, data, or API changes).
