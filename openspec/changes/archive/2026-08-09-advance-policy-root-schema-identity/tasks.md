## 1. Schema files

- [x] 1.1 Freeze pre-change fragment bytes at `schema/0.5.1/dependencies.arch.fragment.schema.json` (matches existing manifest digest).
- [x] 1.2 Bump `$id` in `schema/dependencies.arch.schema.json` to `.../0.6.1/dependencies.arch.schema.json`.
- [x] 1.3 Bump `$id` and all `$ref`s in `schema/dependencies.arch.fragment.schema.json` to `.../0.6.1/...`.
- [x] 1.4 Create frozen `schema/0.6.1/dependencies.arch.schema.json` and `schema/0.6.1/dependencies.arch.fragment.schema.json` snapshots.

## 2. Registry wiring

- [x] 2.1 Repoint `schema/0.5.1/compatibility-manifest.json`'s `policy-root`/`policy-fragment` entries at the `0.6.1` resources with new digests.
- [x] 2.2 Update `ArchLinterNet.Core.csproj` to embed/pack the `0.6.1` frozen snapshots instead of the `0.5.1` root/fragment files.

## 3. Guidance and CI

- [x] 3.1 Update `docs/reference/yaml-schema.md` and `docs/guides/migration-to-0-5-1.md` editor `$schema:` examples to `0.6.1`.
- [x] 3.2 Update `.github/workflows/package-validation.yml` smoke-test assertions to expect `0.6.1` for policy-root/policy-fragment.

## 4. Tests

- [x] 4.1 `PackagedSchemaRegistryTests.cs`: split the "every schema is 0.5.1" assertion so policy-root/policy-fragment expect `0.6.1`.
- [x] 4.2 `ArchitecturePolicyImportSchemaTests.cs`: update the fragment `$ref` rewrite prefix to `0.6.1`.
- [x] 4.3 `CheckpointBReleaseGateTests.CandidatePackageFeed.cs` / `.PackagedCoverageSchema.cs`: update packed-content path expectations to `0.6.1` for policy-root/policy-fragment.

## 5. Validation

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `make acceptance`.
- [x] 5.3 Verify with a local `dotnet pack` + offline tool install that `schema print policy-root` matches the packed `0.6.1` content and validates a real policy using `overlaps_with`.
