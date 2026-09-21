## Context

The existing `architecture_pr_report_producer` already owns the isolated checkout, restore, strict
coverage artifacts, Health/change/report inputs, and inert publication manifests. Its current
sequence builds CLI/Testing separately, invokes the canonical strict gate, repeats read-only
projections, and invokes `change snapshot` independently of Health. The preceding
`governance-snapshot-change-projection` change provides `health --change-snapshot` and is now the
authoritative one-process seam for current evidence.

## Goals / Non-Goals

**Goals:**

- Make candidate preparation an explicit single boundary and bind projections to its identity.
- Overlap independent read-only work without sharing mutable output paths.
- Preserve strict/public-API/coverage/Health/report exit and artifact semantics.
- Produce evidence sufficient for three comparable hosted runs.

**Non-Goals:**

- No Core analyzer or architecture-policy changes.
- No persisted cross-process prepared-analysis cache.
- No larger runners, blind parallelism increase, release publication, or base-evidence persistence.
- No weakening of required gates or publication trust checks.

## Decisions

### Use the canonical strict target as the authority after one explicit build

The producer restores once, builds the solution once, and then invokes the existing
`make lint-architecture` authority with an explicit already-prepared-build switch. The default
local target continues to build and verify the project graph, so the repository's canonical gate
does not change; CI only avoids repeating the already completed build. A candidate manifest records
the checked-out source/tree identity, policy digest, and hashes of the CLI/Testing assemblies.

Alternatives rejected: relying on timestamps or only the checkout SHA would not prove the tool
artifact consumed by every projection; changing the canonical target to never build would weaken
local fail-closed behavior.

### Fan out in one hosted job with per-projection ownership

The producer remains one job because its projections share the prepared checkout and build outputs.
A single shell step starts strict, public-API, coverage, and Health/current/report workers after
candidate and base preparation, gives each worker a private log/status path, waits for every worker,
and aggregates outcomes. This avoids cross-job artifact transport and duplicate builds while still
making overlap observable. The existing producer/gate split remains the authority boundary.

Alternatives rejected: separate jobs would require transporting and re-verifying the build outputs;
sharing the repository's root JSON filenames between background commands would introduce races.

### Use Health's current snapshot output and keep rendering downstream

The current-side report process runs one base snapshot, then one current `health --change-snapshot`
invocation, validates the Health schema, and invokes badge/report renderers only after validation.
The existing standalone base snapshot is retained because it analyzes a distinct immutable base
checkout. The candidate manifest and DAG evidence carry the candidate identity alongside the
existing report/badge PR/tree/run bindings; publication manifests remain schema-stable and are
produced only after the verified report inputs succeed.

### Treat timing evidence as descriptive and fail-closed

The producer writes a versioned DAG evidence document containing the candidate identity, dependency
classification, start/end/duration data, outcomes, base preparation, and target comparison. It is
uploaded as evidence but never used to decide the architecture gate. A failed projection remains a
failed outcome even if another projection produced valid artifacts.

## Risks / Trade-offs

- [Risk] Parallel CLI processes may still contend on build outputs. → The build completes before
  fan-out, all consumers use `--no-build`, and the candidate verifier rejects missing/stale outputs.
- [Risk] A strict failure could hide a useful report. → Projection aggregation retains valid report
  artifacts and the dependent gate evaluates the strict outcome separately.
- [Risk] Hosted-run timing is noisy. → Record runner/source/tool identity, at least three successful
  comparable samples, median/range, and governance span separately from command-sum.
- [Risk] The base snapshot remains a separate analysis cost. → Record it separately and do not add
  a persistence subsystem before the measured residual justifies one.
