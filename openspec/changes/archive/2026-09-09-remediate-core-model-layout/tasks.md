## 1. Baseline and layout repair

- [x] 1.1 Preserve the frozen 24 diagnostic identities and the responsibility-to-target-path map in the change design; verify the map contains 2 classes, 5 enums, and 17 records.
- [x] 1.2 Move the six owned `ArchLinterNet.Core.Model` source files intact from `src/ArchLinterNet.Core/Model` to `src/ArchLinterNet.Core/Models`; verify namespaces and type declarations are byte-for-byte unchanged apart from the paths.

## 2. Regression evidence

- [x] 2.1 Extend the self-policy repository fixture with an isolated temporary model source outside `Models`; verify teardown removes the fixture from every run.
- [x] 2.2 Add a focused audit regression proving that the temporary misplaced model emits the exact `models-live-in-models-directories-record` identity; verify the focused NUnit test passes.

## 3. Reconciliation and validation

- [x] 3.1 Run a comparable audit and verify that precisely the 24 frozen Core/Model identities are absent with no newly introduced layout diagnostics.
- [x] 3.2 Run the Core focused test family, `make fmt`, `make lint-code-size`, `make lint-architecture`, `make public-api-check`, and `openspec validate --all`; verify each required command passes.
- [x] 3.3 Synchronize only the applicable shared `decompose-god-classes` task hunk, archive this completed OpenSpec change, and verify `openspec validate --all` passes after archival.
