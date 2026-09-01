## Context

`main_quality_coverage.py verify-sonar` consumes three filesystem inputs. The coverage inventory and
Sonar analyses JSON are repository-controlled files and correctly stay confined to the release
workspace. The scanner output is captured by the same GitHub Actions job at
`$RUNNER_TEMP/sonar-end.log`; rejecting it with `_safe_path` breaks post-analysis verification.

The existing GitHub command-file boundary proves the pattern: an exceptional runner path needs an
explicit environment-backed validator, rather than an exemption based only on a command-line flag.

## Goals / Non-Goals

**Goals:**

- Permit only a resolved scanner-log path contained by the current process's resolved `RUNNER_TEMP`.
- Reject an unset runner context, paths outside that root, and symlink escapes before any log read.
- Preserve the existing fail-closed validation of canonical OpenCover imports and current-SHA Sonar
  analysis revision.

**Non-Goals:**

- Loosening `_safe_path` for repository or user-controlled inputs.
- Accepting arbitrary external logs based on the `--scanner-log` name or log filename.
- Changing Sonar quality-gate, Codecov, coverage inventory, or release authorization semantics.

## Decisions

### Add a dedicated runner-temp validator

Add a shared `_github_runner_temp_path(value, description, env_var)` helper beside the existing
GitHub command-file helper. It resolves both the candidate and current `RUNNER_TEMP` value with
`realpath`, then requires containment with `commonpath`. This rejects sibling roots, traversal, and
symlinks that point outside the runner-owned root.

**Alternative considered:** compare the candidate to a filename such as `sonar-end.log`. That does
not establish ownership and can be bypassed by an arbitrary external file with the same name.

### Limit the exception to the scanner log

`verify-sonar` alone uses the new helper for `--scanner-log`; it continues to call `_safe_path` for
the inventory and analyses JSON. The helper does not become a general replacement for workspace
confinement.

**Alternative considered:** copy the scanner log into the workspace before verification. This would
avoid a new boundary but adds a write/copy step to the workflow and leaves the requested trusted
runner-temp read-only verification case uncovered.

### Test positive and closed-failure boundaries through the verifier

Focused tests will exercise a valid synthetic runner-temp scanner log, a `/tmp`-style external log,
and a path rooted in a mismatched `RUNNER_TEMP`, as well as existing current-SHA and import proof
checks. The positive test verifies the code reads only the explicitly bound file and preserves
normal output behavior.

## Risks / Trade-offs

- [A process invokes the CLI with a forged `RUNNER_TEMP`] → The boundary intentionally relies on the
  workflow-provided environment of the trusted GitHub Actions job; production callers without it
  fail closed.
- [A path appears under `RUNNER_TEMP` but resolves outside through a link] → `realpath` is applied
  before containment.
- [The scanner no longer writes the log under `RUNNER_TEMP`] → The verifier fails closed with a
  diagnostic until the workflow and trust contract are deliberately updated.

## Migration Plan

No data migration is required. After merge, the existing main telemetry workflow supplies the
already-established `$RUNNER_TEMP/sonar-end.log` path; rollback restores the previous rejection
behavior if the new trust boundary needs to be withdrawn.

## Open Questions

None.
