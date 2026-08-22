## ADDED Requirements

### Requirement: Evidence-based input-testing decisions
The repository SHALL keep an internal assessment that ranks each accepted
untrusted-input surface by realistic robustness impact and custom-code exposure.
For each assessed surface, the record SHALL choose exactly one of
coverage-guided fuzzing justified (A), property-based testing justified (B), or
existing deterministic tests sufficient/fuzzing deferred (C), and SHALL state
the evidence and maintenance rationale. A Scorecard or similar external signal
alone SHALL NOT select a technique.

#### Scenario: Newly introduced byte parser
- **WHEN** a shipped capability introduces a custom parser for repository or
  restored bytes
- **THEN** the assessment explicitly records its A, B, or C outcome before a
  fuzzing claim or follow-up implementation work is made

### Requirement: Safe coverage-guided fuzzing contract
Every selected coverage-guided fuzz target SHALL define a bounded parser seam,
an oracle that accepts canonical success or fail-closed failure, maximum input
size, per-case time limit, process-memory containment, deterministic replay,
corpus ownership, minimization and triage steps, artifact-retention policy, and
execution cadence. The harness SHALL NOT require network access, secrets,
private repositories, or mutation of a live developer repository.

#### Scenario: Campaign discovers a failing input
- **WHEN** a bounded campaign reports a crash, hang, resource-limit breach, or
  unexpected partial success
- **THEN** the input is replayed under the recorded limits, minimized, reviewed
  for safe publication, and a confirmed bug receives a deterministic regression
  test before it is closed

### Requirement: Ordinary CI remains deterministic and bounded
Long-running coverage-guided fuzz campaigns SHALL run only on a scheduled or
manually dispatched path unless a separately approved bounded smoke execution
demonstrates deterministic, material PR value. Ordinary PR CI SHALL run the
normal deterministic regressions for every confirmed fuzzing finding.

#### Scenario: Normal pull request validation
- **WHEN** a pull request does not modify fuzzing infrastructure
- **THEN** its required CI validation does not depend on an unbounded or
  nondeterministic fuzz campaign
