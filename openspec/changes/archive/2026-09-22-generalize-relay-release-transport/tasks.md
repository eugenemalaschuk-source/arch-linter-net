## 1. Transport identity

- [x] 1.1 Generalize candidate version validation beyond v0.8.x.
- [x] 1.2 Replace v0.8-specific current-authority fields with support status,
  external publication boundary, and historical review origin.
- [x] 1.3 Version inventory and distribution manifest schemas.

## 2. Regression evidence

- [x] 2.1 Create and verify frozen transport for v0.8.x.
- [x] 2.2 Create and verify frozen transport for `0.9.0-preview.1`.
- [x] 2.3 Prove wrong source/version, tampering, incompatible subjects and
  malformed versions remain fail-closed.
- [x] 2.4 Prove v0.9 transport evidence contains no false current #806/v0.8.x
  authority claim.

## 3. Documentation

- [x] 3.1 Document inventory v3 and distribution v2.
- [x] 3.2 Document review-origin versus current release-authority boundary.

## 4. Validation

- [ ] 4.1 Run release-tool tests.
- [ ] 4.2 Run strict OpenSpec validation.
- [ ] 4.3 Pass full PR CI, Package Validation, CodeQL, and workflow quality.
- [ ] 4.4 Re-run the official `0.9.0-preview.1` publication only after the
  fix is merged.
