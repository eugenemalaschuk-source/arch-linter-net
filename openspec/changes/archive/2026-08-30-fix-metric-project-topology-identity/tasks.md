## 1. Canonical metric project projection

- [x] 1.1 Separate canonical resolved-artifact project identity from legacy policy selector display identity in the metric topology projection, and verify project relations and external facts use the canonical identity.
- [x] 1.2 Mark a selected project metric unassessable when its legacy project selector covers multiple canonical artifact-derived project identities, and verify the applicability reason is `missing_required_input`.

## 2. Regression and reviewed surface

- [x] 2.1 Add a regression with two distinct resolved artifacts sharing an output assembly name and verify no trusted project metric value is emitted.
- [x] 2.2 Regenerate the complete Core approval fixture from its canonical reflection surface and verify its approval test and `make public-api-check` pass.

## 3. Validation

- [x] 3.1 Run focused Core metric tests, OpenSpec validation, formatting, and the public API check; verify all pass.
