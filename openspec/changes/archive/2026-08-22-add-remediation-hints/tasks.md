## 1. Normalized remediation model and generation

- [x] 1.1 Add the public finite remediation-hint model and optional `ArchitectureFinding` property without changing finding identity.
- [x] 1.2 Add the exact-type remediation-provider registry and completeness protection alongside the existing detail projection registry.
- [x] 1.3 Implement conservative providers for port/adapter, placement/classification, coverage/build/policy input, external/package/framework, narrow exception, public-surface, and generic review cases.

## 2. Output integration

- [x] 2.1 Project the optional hint through the existing normalized JSON and concise Human finding output.
- [x] 2.2 Preserve the same normalized hint under SARIF result properties without creating SARIF executable fixes.
- [x] 2.3 Verify Testing consumers expose the normalized hint through `ArchitectureValidationResult.Findings`.

## 3. Evidence and regression tests

- [x] 3.1 Add focused tests for declared-port/adapter, no-known-seam, placement/classification, coverage/preflight, external/package/framework, narrow exception, and public-surface guidance.
- [x] 3.2 Add JSON/SARIF/Human/Testing parity tests, same-named cross-assembly identity tests, and registry completeness regressions.
- [x] 3.3 Update reviewed public API evidence for the additive Core model.

## 4. Documentation and verification

- [x] 4.1 Document remediation categories, safe-fix ordering, examples, limitations, and non-automatic behavior in the public MkDocs documentation.
- [x] 4.2 Format changed files and run focused Core/Testing/CLI tests, architecture lint, public API validation, and strict OpenSpec validation.
- [x] 4.3 Synchronize the implemented specs, archive the OpenSpec change, and validate the archived specification set.
