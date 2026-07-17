## 1. Strengthen CI package validation

- [x] 1.1 Extend the CEL forbidden-content pattern in `.github/workflows/package-validation.yml` to reject `.yml`/`.yaml`/`.schema.json` entries by extension
- [x] 1.2 Verify locally: legitimate package listing still passes; synthetic listings containing `.schema.json`, `.yml`, and `.yaml` entries are all rejected

## 2. Fix versioning policy contradiction

- [x] 2.1 Rewrite "Package release versioning" in `docs/internal/cel-engine-architecture.md` so it no longer implies a Profile v1 semantics change is an allowed release event

## 3. Validation

- [x] 3.1 Run `rtk make fmt`
- [x] 3.2 Run `rtk make acceptance`
- [x] 3.3 Run `rtk make lint-docs`

## 4. Spec synchronization and archive

- [x] 4.1 Run `openspec validate --all`
- [x] 4.2 Run `openspec archive harden-cel-package-validation-round2`
- [x] 4.3 Run `openspec validate --all` again after archive
