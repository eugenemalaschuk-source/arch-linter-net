## 1. Release range and bundle tooling

- [x] 1.1 Implement candidate-bound stable/preview predecessor selection, annotated-tag peeling, ancestry checks, shallow/missing-object rejection, duplicate-version rejection, and the typed no-predecessor result; verify with focused release-forensics selector tests.
- [x] 1.2 Verify the exact candidate package manifest and run its CLI once to emit JSON and Markdown; generate the deterministic provenance manifest/checksums and verify report identity/digests with focused bundle tests.
- [x] 1.3 Record CLI phase/render timings, process wall time/memory, and separate orchestration observations outside canonical JSON; verify stable report bytes and distinct observation fields with focused tests.

## 2. Release workflow integration

- [x] 2.1 Export candidate commit/tree identity and add a full-history, read-only `history-forensics` job that depends only on `prepare-candidate`; verify its dependency graph, least privilege, exact package use, and absence of solution builds/enrichment with workflow tests.
- [x] 2.2 Make the existing package publication wait for the complete forensics bundle and extend the existing GitHub Release job to attach it with no-clobber retry behavior and read-back digest verification; verify publication gating and identity checks with workflow tests.

## 3. Maintainer documentation and validation

- [x] 3.1 Document dry-run artifact review, durable release assets, stable/preview boundaries, no-predecessor behavior, and packed-candidate reproduction in the maintainer release process.
- [x] 3.2 Run focused release tooling/workflow tests, relevant format/lint checks, and strict OpenSpec validation; inspect the final diff for candidate identity, dependency cycles, missing assets, or unrelated changes.
