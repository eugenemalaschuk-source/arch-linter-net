## Context

The `v0.7.0` tag is available and follows the `v0.6.5` release. The shipped
tool already supports canonical history analysis, policy context export,
architecture change reports, policy-weakening comparison, remediation hints,
and new-debt gating. Existing public documentation describes those surfaces
individually, while the earlier forensics dogfood record is contributor-only
and targets an older release range.

This change must make one real repository run understandable to external
adopters without presenting advisory analysis as an automatic refactoring
decision or a PR-shaped comparison as whole-repository validation.

## Goals / Non-Goals

**Goals:**

- Record a deterministic, public-safe `v0.6.5` to `v0.7.0` forensics run from
  the released tool, including command provenance and a canonical-report
  digest.
- Provide a single evergreen public guide that explains both forensics and
  AI-first drift-control commands, their inputs, and their trust boundaries.
- Classify material forensics findings and every self-policy candidate from
  actual results rather than adopting configuration for presentation value.
- Exercise real public repository revisions for the drift-control examples
  without modifying policy or baseline data.

**Non-Goals:**

- Change CLI, policy, scoring, public APIs, debt baselines, or product
  semantics.
- Turn a score, report, hint, or clean comparison into automatic design
  authority.
- Create a broad refactoring task without a concrete, stable-boundary finding.
- Make the documentation page identity release-version-specific.

## Decisions

### Use an installed v0.7.0 tool for recorded evidence

The run will use the released `arch-linter-net` package in an isolated tool
directory, while the repository supplies the public policy and Git objects.
This proves the consumer-facing command surface. A source-tree invocation can
support troubleshooting but cannot be the only proof.

Alternative considered: run only `dotnet run` from the current checkout. This
would be simpler but would not prove the released package or fixed release
semantics.

### Keep reproducible report identity and selected output excerpts

A canonical JSON report is public-safe because it derives only from this public
repository and its public policy. The evidence record cites its SHA-256 digest
and the public guide shows selected, concise excerpts; the exact command and
identity let a reviewer recreate the full machine artifact without presenting a
large release-specific JSON file as an evergreen documentation asset.

Alternative considered: document only a hand-written summary. That would make
review and external experimentation less credible.

### Use real tagged snapshots for AI workflow comparisons

The drift-control demonstration will produce complete snapshots from separate
public Git worktrees/revisions and compare their effective policies and
architecture results. The guide will call out that the comparison reports a
complete-result delta, whereas an individual analysis remains the authoritative
validation run.

Alternative considered: edit a copy of the policy to force a weakening finding.
This is rejected because it would manufacture a showcase and violate the issue
boundary.

### Separate public reference from contributor evidence

The public guide contains instructions, interpretation guidance, and portable
adaptation advice. A contributor-facing evidence record contains the exact
tool/source/range/digest, classifications, and self-policy decision ledger.
This keeps the external path cohesive while retaining reviewable project detail.

## Risks / Trade-offs

- [A later tool release changes defaults or output shape] → Pin the recorded
  tool version, source commit, ref operands, policy path, and artifact digest;
  instruct adopters to record their own versions.
- [A large JSON artifact obscures the guide] → Keep the full artifact adjacent
  to the evidence record and quote only concise, labeled excerpts in the guide.
- [A clean policy-weakening result is mistaken for proof that no policy risk
  exists] → Explain the comparison scope, advisory confidence, and review
  requirement explicitly.
- [Worktree/source builds introduce local paths into evidence] → Exclude all
  local paths and report only authored operands, resolved IDs, public policy
  identity, and output digest.
- [Findings tempt unrelated refactoring] → Record classifications and create a
  follow-up only when concrete evidence establishes a stable boundary; otherwise
  record no follow-up.
