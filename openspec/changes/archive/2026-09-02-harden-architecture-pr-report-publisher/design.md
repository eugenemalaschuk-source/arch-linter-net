## Context

The initial split correctly isolated pull-request write authority, but a base snapshot assumes a
baseline file exists in every historical tree and the completed-CI publisher treats the overall CI
conclusion as the integrity state of its independent producer. Both assumptions are invalid:
baseline adoption is historical, and the architecture report must explain a failing architecture
gate without becoming a summary of unrelated CI results.

## Goals / Non-Goals

**Goals:**

- Generate each snapshot with the policy and baseline inputs available in that exact tree.
- Make producer transport readiness distinct from the strict architecture verdict and overall CI
  conclusion.
- Exercise the executable publisher JavaScript through controlled GitHub/event/artifact fixtures.

**Non-Goals:**

- Changing Core/CLI snapshot, Health, change, or report semantics.
- Giving pull-request code write permission or checking out source in the publisher.
- Optimizing the number of architecture traversals in this corrective change.

## Decisions

### Discover the baseline in the snapshot worktree

The producer will build the common `change snapshot` arguments in each current directory, append
`--baseline architecture/baseline.arch.yml` only when that file exists there, and run the base
snapshot while the base worktree is current. This handles baseline existence independently for
base-only, head-only, both, and neither without inventing a baseline from the other commit.

### Separate report production from gate enforcement

The report-producer job will complete successfully when it has generated and uploaded valid
canonical report inputs, even when the strict coverage command found an architecture violation.
A small dependent Architecture PR Report Gate job will fail the CI for that strict result. The
publisher will query the completed run's jobs and accept the artifact only when exactly the named
producer job succeeded; it will not consult `workflow_run.conclusion`. This lets a report explain a
failing architecture gate and ignores unrelated failed jobs, while producer failures/cancellation
remain fail-closed.

An overall-CI conclusion is not an alternative integrity signal: it conflates producer readiness,
architecture verdict, and unrelated CI outcomes. A separate workflow was rejected because the
existing CI run already gives the publisher a run-bound artifact inventory and job identity.

### Execute publisher scripts with mocked GitHub fixtures

Python tests will extract the `actions/github-script` bodies from the workflow and execute them in
Node with mocked GitHub REST operations, `core` outputs, event payloads, and bounded temporary
artifact files. The tests therefore cover the same producer-resolution, validation, and
comment-update branches that GitHub runs, without requiring a privileged `workflow_run` trigger on
the default branch before merge.

## Risks / Trade-offs

- [A renamed producer job breaks publisher lookup] → Use one stable job name and test the exact
  job-resolution contract.
- [Fixture mocks diverge from Actions APIs] → Keep mocks limited to the REST calls made by the
  scripts and retain workflow lint plus real post-merge event monitoring.
- [Historical base has no baseline] → Omit only that tree's baseline option; snapshot semantics
  remain governed by the committed policy.

## Migration Plan

1. Amend the producer and publisher together, preserving the existing artifact schema.
2. Push the corrective commit to PR #759 and let ordinary CI validate the unprivileged producer.
3. After merge, observe the first default-branch publisher trigger; rollback remains a revert of
   this corrective commit, with no write authority returned to pull-request CI.

## Open Questions

None.
