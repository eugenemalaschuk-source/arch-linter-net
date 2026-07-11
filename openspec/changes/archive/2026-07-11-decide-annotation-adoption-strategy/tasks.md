## 1. Documentation

- [x] 1.1 Extend `docs/policy-format/semantic-classification.md` with an explicit "Annotation strategy" section: the decision (no binary/source-only package this wave), the recommended path (user-owned attribute mapped by full type name), and the trade-off comparison (binary package / source-only package / user-owned attributes).
- [x] 1.2 Update the "No annotation package" bullet under "Current limits" in the same doc to reference the new section instead of only stating the negative.

## 2. Spec synchronization

- [x] 2.1 Verify the `semantic-role-catalog` delta spec resolves the issue #108 placeholder without altering unrelated requirements.
- [x] 2.2 Verify the `semantic-classification-model` delta spec resolves the no-package decision without altering unrelated requirements.
- [x] 2.3 Run `openspec validate --all` after archive to confirm both rebuilt specs pass. (`openspec validate --all --strict`: 80/80 passed.)

## 3. Compile fixtures

- [x] 3.1 Add a compile-only fixture file under `tests/ArchLinterNet.Core.Tests/` demonstrating the documented recommended pattern: an internal, `[AttributeUsage]`-annotated custom attribute matching the doc's `DomainLayerAttribute` example shape, following the existing `*TestFixtures.cs` convention (see `AttributeUsageContractTestFixtures.cs`).
- [x] 3.2 Confirm the fixture adds no scanner/runtime assertions (extraction remains out of scope) and only proves the pattern compiles.

## 4. Validation and lifecycle

- [x] 4.1 Run `make fmt`.
- [x] 4.2 Run `make acceptance`; fix failures or document any environment blocker.
- [x] 4.3 Synchronize and archive the OpenSpec change (`openspec archive decide-annotation-adoption-strategy`).
- [x] 4.4 Open the PR for issue #108.
