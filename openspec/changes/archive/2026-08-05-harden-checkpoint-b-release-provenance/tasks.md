## 1. Immutable candidate lifecycle

- [x] 1.1 Add a versioned candidate manifest generator and verifier for the four release packages.
- [x] 1.2 Make the workflow calculate version and pack exactly once before platform validation.
- [x] 1.3 Download, verify, and publish the same manifested artifact without a second pack.

## 2. Isolated acceptance matrix

- [x] 2.1 Make the Checkpoint B harness consume a supplied candidate feed through isolated NuGet configuration and caches.
- [x] 2.2 Add fixture outcome oracles and assert cache, clean-checkout, shell, non-TTY, and external-consumer provenance.
- [x] 2.3 Add deterministic in-flight cancellation and publication-interruption checks.

## 3. Evidence and release gates

- [x] 3.1 Replace self-declared evidence with strict manifest/scenario/platform validation.
- [x] 3.2 Install pinned OpenSpec and run strict validation in the aggregation job.
- [x] 3.3 Update release documentation and the Checkpoint B spec purpose/artifact contract.

## 4. Verification

- [x] 4.1 Run focused tests, formatting, lint, acceptance, and strict OpenSpec validation.
- [ ] 4.2 Archive the corrective OpenSpec change and run the non-publishing release workflow.
