## ADDED Requirements

### Requirement: Scheduled and manually dispatched Git-parser fuzzing
GitHub Actions SHALL provide a dedicated fuzzing workflow for the synthetic Git
binary-parser harness. The workflow SHALL run only on a schedule or explicit
manual dispatch, use fixed SharpFuzz and AFL++ image versions, and execute the
campaign with no container network access, a 100 ms per-case timeout, and a
512 MiB memory limit.

The workflow SHALL not run from ordinary pull-request CI. It SHALL upload
candidate crash or hang artifacts only when they exist and with short-lived
retention suitable for private review.

#### Scenario: Ordinary pull request validation
- **WHEN** a pull request changes code or the fuzzing harness
- **THEN** ordinary CI runs deterministic repository checks and regressions but
  does not start the coverage-guided campaign

#### Scenario: Scheduled campaign
- **WHEN** the fuzz workflow is triggered on its schedule or manually
- **THEN** it materializes the committed synthetic corpus, verifies the pinned
  toolchain, and executes the bounded no-network AFL++ campaign
