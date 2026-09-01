## 1. Core health contract and projection

- [x] 1.1 Add versioned public Health model, dimension state/reason vocabulary, and a pure deterministic projector over canonical validation and debt-gate receipts; verify focused projector unit tests cover stable ordering and all precedence states.
- [x] 1.2 Add focused Core scenarios for clean, reviewed finding debt, waiver debt, new/broadened waiver, strict failure, applicability failure, topology/external evidence, and metric state; verify the targeted Core test filter passes.
- [x] 1.3 Add the read-only health request/service/engine entry point that orchestrates existing snapshot and debt-gate authorities without reimplementing their semantics; verify integration tests prove requested modes and shared receipts are forwarded correctly.

## 2. Consumer projections

- [x] 2.1 Add the Testing builder entry point that returns the Core-owned Health result and focused adapter tests; verify its typed result agrees with direct Core evaluation.
- [x] 2.2 Add the CLI `health` command, human/JSON formatters, exact 0/1/2 gate exit mapping, and focused CLI command/output tests; verify valid unassessable output differs from an invocation error while both exit 2.
- [x] 2.3 Document the command and `architecture-health/v1` output, including assessability and non-score boundaries; verify documentation lint passes.

## 3. Integration and lifecycle

- [x] 3.1 Review the public Core/Testing API drift, update the reviewed snapshots through the explicit lifecycle, and inspect the diff; verify `make public-api-check` passes afterward.
- [x] 3.2 Run the risk-based validation suite: focused Core, Testing, and CLI tests; formatting; implicated repository checks; and strict OpenSpec validation; verify every required command exits successfully.
- [x] 3.3 Synchronize the delta spec with the implemented behavior, archive the OpenSpec change, and validate all specs before opening the pull request; verify `openspec validate --all --strict` passes after archive.
