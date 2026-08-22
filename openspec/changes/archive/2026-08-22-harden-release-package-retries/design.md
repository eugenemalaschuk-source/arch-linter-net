## Context

The paired-subject release path invokes Bash variable expansion in matrix jobs
that default to PowerShell on Windows. NuGet's `--skip-duplicate` primary push
also does not guarantee an adjacent symbol upload when the primary already
exists.

## Goals / Non-Goals

**Goals:** use Bash explicitly for every relevant verification step and fail
the workflow when a primary package already exists, preventing a successful
partial rerun.

**Non-Goals:** independently pushing symbols or changing NuGet.org semantics.

## Decisions

- Set `shell: bash` on all manifest verification steps that expand `$...`.
- Remove `--skip-duplicate`; a duplicate primary is a release-integrity error
  requiring an operator to inspect primary/symbol state before retrying.
- Test the committed workflow contract statically, including Windows shell and
  duplicate-path assertions.

## Risks / Trade-offs

- A rerun after a partial publish stops rather than auto-healing → this is
  intentional fail-closed behavior and preserves the paired-subject invariant.
