## 1. Schema alignment

- [x] 1.1 Update the embedded coverage schema so project and assembly scopes reject `roots` and namespace roots remain required.
- [x] 1.2 Add schema-instance regressions for valid rootless project/assembly contracts and invalid roots.

## 2. Public policy validation coverage

- [x] 2.1 Add a direct `policy check` regression fixture exercising namespace, project, assembly, dependency-edge, and rule-input coverage scopes.
- [x] 2.2 Add imported/composed-policy regressions for the same coverage-scope semantics and actionable invalid-root diagnostics.
- [x] 2.3 Verify the packed schema artifact preserves the corrected project and assembly semantics.

## 3. Specification and validation

- [x] 3.1 Run focused schema, policy-check, composition, and package-artifact tests.
- [x] 3.2 Run formatting and full acceptance; synchronize and archive the OpenSpec change.
