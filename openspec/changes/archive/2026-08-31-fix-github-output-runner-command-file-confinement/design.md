## Context

See proposal.md - Why. `_release_workspace._safe_path` is a shared sanitizer used by every
`Path`-typed CLI argument across `tools/release/`; `main_quality_coverage.py` applies it to
`--github-output` too, which breaks the real workflow because `$GITHUB_OUTPUT` legitimately lives
outside the checkout.

## Goals / Non-Goals

**Goals:**
- Make `main_quality_coverage.py assemble`/`verify-inventory`/`verify-sonar` work with the real
  `$GITHUB_OUTPUT` path a GitHub Actions runner supplies, without weakening confinement for any
  other argument.
- Give the exception an explicit, narrow, testable trust boundary rather than special-casing or
  loosening `_safe_path` itself.

**Non-Goals:**
- Changing `_safe_path`'s behavior or any of its other call sites.
- Adding the same trust boundary to `main_build.py`'s `--github-env`/`--github-output` — those were
  never passed through `_safe_path`, so they have no regression to fix here.
- Validating that the path is shaped like a runner temp path (e.g. contains
  `_runner_file_commands`) — see Decisions below for why that's not the chosen check.

## Decisions

**Anchor trust to the environment variable, not to path shape.** `_github_command_file_path(value,
description, env_var)` accepts `value` only when `os.path.realpath(value) ==
os.path.realpath(os.environ[env_var])`. The workflow already invokes the script as
`--github-output "$GITHUB_OUTPUT"`, so the CLI argument and the env var are the same value by
construction in the real run; the check simply refuses to trust the CLI argument as a
runner-file transport path on its label alone. Alternative considered: pattern-match the path
against `_runner_file_commands` or similar. Rejected — it's not deterministic across runner
versions/OSes, and the issue explicitly asks for "the narrowest deterministic validation
practical," which the environment-variable match satisfies without guessing at runner internals.

**Reuse `_safe_path`'s module, add a sibling function, don't touch `_safe_path` itself.** Keeps the
existing broad confinement requirement intact and testable independently (per #736's spec), and
keeps the new trust boundary a single well-named function other scripts can adopt for their own
`--github-env`/`--github-output` arguments later, without exceptions scattered through call sites.

**Scope the fix to the 3 `--github-output` call sites in `main_quality_coverage.py`.** These are
the only places `_safe_path` was ever applied to a runner command-file argument (confirmed by
`grep -rn _safe_path tools/release/*.py`). `main_build.py` never wrapped its own
`--github-env`/`--github-output` in `_safe_path`, so it isn't part of this regression.

## Risks / Trade-offs

- [The check now depends on `GITHUB_OUTPUT` being set in the process environment, not just passed
  as a CLI value] → This matches how the runner and the workflow invocation already work
  (`--github-output "$GITHUB_OUTPUT"` inherits the env var into the same value), and tests set it
  explicitly via `monkeypatch.setenv` before exercising the code path, so no production behavior
  or test coverage is lost.
- [A future script could add `--github-output` and forget the new function, reintroducing the
  `_safe_path` mismatch] → Mitigated by the MODIFIED spec requirement documenting the exception
  explicitly, and by `_github_command_file_path` being a small, discoverable, single-purpose
  function next to `_safe_path` in the same module.
