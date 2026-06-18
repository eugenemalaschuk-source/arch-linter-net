## 1. Model

- [x] 1.1 Add `External` boolean property to `ArchitectureLayer` in `ArchitectureContractModels.cs`, defaulting to `false`

## 2. Schema

- [x] 2.1 Add `external` optional boolean property to `layer` definition in `schema/dependencies.arch.schema.json`, with `default: false` and a description

## 3. Configuration Check

- [x] 3.1 In `ArchitectureContractRunner.CheckConfiguration()`, skip the empty-layer diagnostic when `layer.External == true`

## 4. Tests

- [x] 4.1 Add test: external layer with no types produces no configuration violation
- [x] 4.2 Add test: non-external empty layer still produces violation (regression)
- [x] 4.3 Add test: external layer with types found is used normally
- [x] 4.4 Add test: external layer as forbidden target in dependency contract works
- [x] 4.5 Add test: external layer in layer contract works
- [x] 4.6 Add test: external layer in independence contract works

## 5. Verification

- [x] 5.1 Run `rtk make restore`
- [x] 5.2 Run `rtk make test` — all tests pass
- [x] 5.3 Run `rtk make lint` — no lint or architecture violations
