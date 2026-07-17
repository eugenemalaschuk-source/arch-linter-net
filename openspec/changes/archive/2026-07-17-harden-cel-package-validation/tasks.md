## 1. Strengthen CI package validation

- [x] 1.1 Extend the CEL package-content check in `.github/workflows/package-validation.yml`: assert `README.md`, `lib/net10.0/ArchLinterNet.CEL.dll`, and `lib/net10.0/ArchLinterNet.CEL.xml` are present; reject Core/CLI/Testing assemblies and YAML/JSON-schema/Buildalyzer/Roslyn assets
- [x] 1.2 Extend the Core→CEL edge check: parse the actual declared dependency version and assert it equals the packed CEL package's own version; assert Core's package listing does not contain `lib/net10.0/ArchLinterNet.CEL.dll`
- [x] 1.3 Verify all new/changed assertions locally against a real `make pack` output (positive case) and against synthetic negative-case listings (forbidden entry present, version mismatch, embedded CEL dll) to confirm each assertion actually fails when it should

## 2. Validation

- [x] 2.1 Run `rtk make fmt`
- [x] 2.2 Run `rtk make acceptance`

## 3. Spec synchronization and archive

- [x] 3.1 Run `openspec validate --all`
- [x] 3.2 Run `openspec archive harden-cel-package-validation`
- [x] 3.3 Run `openspec validate --all` again after archive
