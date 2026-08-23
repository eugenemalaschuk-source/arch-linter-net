## 1. Versioned declaration model

- [x] 1.1 Move the v0.6.4 authority into a tracked scopes collection and add the reviewed v0.7.0/#613 declaration.
- [x] 1.2 Select one stable target declaration from candidate-manifest metadata without adding caller-controlled paths.

## 2. Candidate-bound release evidence

- [x] 2.1 Include declaration identity/hash, candidate version, and complete reviewed inventories in release-scope evidence.
- [x] 2.2 Reject incomplete or cross-candidate scope evidence during Checkpoint B aggregation.
- [x] 2.3 Update release process documentation and active terminology for target-selected authorities.

## 3. Regression coverage and verification

- [x] 3.1 Cover coexisting declarations, unmapped/duplicate/malformed targets, live required states, exclusions, and candidate-binding failures.
- [x] 3.2 Run focused release-tool tests, format/lint checks, and strict OpenSpec validation.
