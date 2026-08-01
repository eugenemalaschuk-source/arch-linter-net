## 1. Publication and process reliability

- [ ] 1.1 Complete report-routing evidence and non-retroactive stream cancellation tests.
- [x] 1.2 Drain asynchronous child output after polling and check cancellation before constructing `ValidationOutcome`.
- [ ] 1.3 Bound child-process cleanup after kill without leaking a process tree.

## 2. Full shared-pipeline propagation

- [ ] 2.1 Thread cancellation through policy import/composition and all scanning outer loops.
- [x] 2.2 Add cancellation boundaries to hashing and receipt publication.
- [ ] 2.3 Extend baseline, public-API, and profile shared-pipeline requests and CLI callers.

## 3. Validation

- [ ] 3.1 Add deterministic regression tests for each fixed boundary and run acceptance.
