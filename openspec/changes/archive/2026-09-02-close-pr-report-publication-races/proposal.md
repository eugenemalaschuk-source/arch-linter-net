## Why

PR CI proved that the producer invokes `health` without the baseline its CLI contract requires.
The publisher also has a stale-head write race, can replace a valid same-head report after a partial
rerun, and can change malformed artifact bytes while decoding them for a GitHub comment.

## What Changes

- Create a canonical empty baseline in the read-only producer's runner temporary directory when a
  repository baseline is absent, and reject a Health output that is not `architecture-health/v1`.
- Re-read the PR head immediately before a comment write and replace a just-written report with a
  fixed unavailable state if a later head is observed.
- Preserve a verified same-head sticky report when a partial rerun has no producer job, while
  retaining fail-closed behavior for failed or cancelled producers.
- Decode report bytes as fatal UTF-8 during validation and publication, then add behavioral
  regression fixtures for every new failure/race path.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-pr-report-publication`: Require valid Health creation without a repository
  baseline, close stale-head publication races, preserve valid same-head partial-rerun evidence,
  and preserve exact valid UTF-8 report text.
- `github-actions-ci`: Define the producer's ephemeral empty-baseline and Health-schema guard.

## Impact

- `.github/workflows/ci.yml` and `publish-architecture-pr-report.yml`
- `tools/release/tests/test_architecture_pr_report_publisher.py`
- Existing architecture PR publication and CI OpenSpec specs
