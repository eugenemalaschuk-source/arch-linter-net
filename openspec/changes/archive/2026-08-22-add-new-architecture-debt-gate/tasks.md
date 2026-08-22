## 1. Core gate model and orchestration

- [x] 1.1 Add typed Core request, result, decision, and evaluation/persistent-debt/weakening section models.
- [x] 1.2 Implement the Core gate application service by composing baseline verification and optional policy-weakening comparison without baseline mutation.
- [x] 1.3 Add deterministic Human, JSON, and SARIF gate formatters that preserve separate typed sections.

## 2. CLI integration

- [x] 2.1 Add the composed top-level `gate` command definition, options, and handler with input/build-state guards.
- [x] 2.2 Extend the CLI runtime/composition seams and fakes for the typed gate operation.
- [x] 2.3 Add CLI unit and integration coverage for successful, new-debt, fail-closed, and weakening-only gate outcomes in every output format.

## 3. Testing adapter

- [x] 3.1 Expose the typed gate operation and context-artifact configuration through the Testing builder.
- [x] 3.2 Add Testing adapter coverage for exact identity, independent weakening, and final-decision parity.

## 4. Documentation and verification

- [x] 4.1 Update MkDocs CLI, CI, migration-baseline, output-format, and test-adapter guidance with gate usage, exit codes, examples, and limits.
- [x] 4.2 Run focused Core, CLI, and Testing tests; formatter; docs and OpenSpec validation; fix issue-related failures.
- [x] 4.3 Synchronize the implemented behavior with the change specs and archive the completed OpenSpec change.
