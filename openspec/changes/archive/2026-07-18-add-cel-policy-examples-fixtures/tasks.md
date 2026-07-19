## 1. Modular-monolith sample: Catalog port seam

- [x] 1.1 Add `samples/policies/imports/modular-monolith/architecture/policy/bounded-contexts/catalog.arch.yml` declaring the `catalog_domain`/`catalog_port` layers (mirroring `inventory.arch.yml`'s shape).
- [x] 1.2 Add a `strict_port_boundaries` entry to `sales.arch.yml` (or a new `policy/bounded-contexts/sales-catalog-seam.arch.yml` fragment if that keeps `sales.arch.yml` focused): Sales Application reaching Catalog only through an approved `Port`/`domain: Catalog` seam, with `forbidden` listing Catalog `DomainLayer` and `Adapter`.
- [x] 1.3 Wire the new fragment(s) into `samples/policies/imports/modular-monolith/architecture/arch.yml`'s `imports` list and `analysis.target_assemblies` (illustrative name, matching existing `MyCompany.Product.Modules.*` convention).
- [x] 1.4 Update `samples/policies/imports/modular-monolith/monolithic.yml` with the equivalent inlined contract so the existing `Load_PublicMonolithicAndImportedSamples_ProduceEquivalentModels` acceptance test continues to prove structural equivalence.

## 2. Modular-monolith sample: LegacyCrm anti-corruption seam

- [x] 2.1 Add `samples/policies/imports/modular-monolith/architecture/policy/bounded-contexts/legacy-crm.arch.yml` declaring a `LegacyCrm` layer and a `strict_port_boundaries` entry using an `AntiCorruptionLayer` allowed seam with a forbidden direct database/infrastructure adapter target, using role/metadata names from `semantic-role-catalog`.
- [x] 2.2 Wire the fragment into `architecture/arch.yml`'s imports and `analysis.target_assemblies`.
- [x] 2.3 Update `monolithic.yml` with the equivalent inlined contract.

## 3. Modular-monolith sample: layout convention

- [x] 3.1 Add a `strict_layout_conventions` entry (new `policy/shared/application-layout.arch.yml` fragment or added to an existing shared fragment) declaring `files_matching.folder_segment: Services`, `require_type_kind: class`, `required_name_suffix: Service`, and `require_matching_interface: { name_prefix: I }`, matching the shape already documented in `docs/contracts/layout-conventions.md`.
- [x] 3.2 Wire the fragment into `architecture/arch.yml`'s imports.
- [x] 3.3 Update `monolithic.yml` with the equivalent inlined contract.

## 4. Unity-client sample: layout convention

- [x] 4.1 Add a `strict_layout_conventions` or `audit_layout_conventions` entry to `samples/policies/imports/unity-client/architecture/policy/runtime.arch.yml` (or a new fragment) expressing the Runtime/Editor/Features classification as a layout expectation (e.g. forbidding an editor-only name/type-kind pattern from a Runtime-folder-selected file).
- [x] 4.2 Wire the fragment into `architecture/arch.yml`'s imports if a new file was added.
- [x] 4.3 Update `samples/policies/imports/unity-client/monolithic.yml` with the equivalent inlined contract.

## 5. Structural acceptance coverage for the extended samples

- [x] 5.1 Run `Load_PublicMonolithicAndImportedSamples_ProduceEquivalentModels` and `Load_RecommendedAndArbitraryFixtureNames_ProduceEquivalentModels` (extending fixture data if needed) against both extended samples; confirm they still pass with the new contracts present.
- [x] 5.2 Add an assertion (new test or extend an existing one in `ArchitecturePolicyImportAcceptanceTests.cs`) that the loaded modular-monolith model contains the new `strict_port_boundaries` (Catalog seam + LegacyCrm ACL) and `strict_layout_conventions` entries, and the Unity-client model contains the new layout-convention entry — proving the fixtures actually loaded, not just that monolithic/imported forms match each other.

## 6. CLI-level runnable fixture: port boundary and anti-corruption

- [x] 6.1 Create `tests/ArchLinterNet.Cli.Tests/PortLayoutCliFixtures.cs` following `ContextualContractCliFixtures.cs`'s pattern: marker attributes for `DomainLayer`, `Port`, `Adapter`, `AntiCorruptionLayer`; a Sales type reaching Catalog through an approved port type (passing); a Sales type directly referencing a Catalog domain type (violating); a LegacyCrm type reaching infrastructure through an ACL type (passing); a LegacyCrm type directly referencing a database adapter type (violating).
- [x] 6.2 Create `tests/ArchLinterNet.Cli.Tests/PortLayoutCliTests.cs` following `ContextualContractCliTests.cs`'s pattern: write a policy YAML with `strict_port_boundaries` entries for both scenarios, run through `ValidateCommandHandler`, and assert strict mode fails with the violating type identified and passes (no violation) for the approved-seam type.
- [x] 6.3 Add a parallel test declaring the same rule shape under `audit_port_boundaries` instead, asserting the audit run reports the finding (exit code per existing audit convention) while a strict run of a policy that declares only the audit entry exits 0 and omits the finding — mirroring `Validate_AuditContextDependencies_CrossDomainReference_IsReportedUnderAuditMode`.
- [x] 6.4 Add a `--format json` test asserting the JSON output for the violating port-boundary case includes the violating type's identity and the forbidden/expected evidence fields.

## 7. CLI-level runnable fixture: layout convention and CEL when

- [x] 7.1 Extend `PortLayoutCliFixtures.cs` with a `Services`-folder-selected fixture type pairing (a concrete service class with a matching `I`-prefixed interface elsewhere, and a second concrete service class with no matching interface) — note: layout convention selectors need real on-disk source file paths, so confirm whether `PortLayoutCliFixtures.cs` needs to be written to a temp source file at test time (matching `LayoutConventionContractTestFixtures.cs`'s `WriteFixtureFile` pattern) rather than compiled into the test assembly directly; follow whichever approach `LayoutConventionContractTests.cs` establishes for source-path-backed facts.
- [x] 7.2 Add a `strict_layout_conventions` test asserting the missing-interface-counterpart type is reported as a violation and the paired type is not.
- [x] 7.3 Add a `strict_layout_conventions` test using `files_matching.when` to narrow matched declared types by a CEL predicate (e.g. `subject.simpleName == "..."`), asserting only the predicate-matched type is checked.
- [x] 7.4 Add a `--format json` (or `explain`, whichever the layout-convention diagnostic path already supports at CLI level) test asserting the JSON/explain output for the `when`-narrowed violation includes the expected file, contract, and counterpart-name evidence fields.

## 8. Documentation cross-references

- [x] 8.1 Update `docs/contracts/port-boundary.md` to reference the exact tested fixture shape from `PortLayoutCliTests.cs` (file path or a short "this shape is exercised in tests/..." note), keeping the existing prose example if it remains accurate.
- [x] 8.2 Update `docs/contracts/layout-conventions.md` similarly for the layout/`when` fixture.
- [x] 8.3 Update `docs/ai/policy-authoring-guide.md` with a short section distinguishing the narrative `samples/policies/imports/*` examples from the CLI-tested fixture shapes, per design.md's "Risks" note, so AI guidance points at the proven pattern first.

## 9. Validation

- [x] 9.1 Run `rtk make lint` and confirm architecture/format/code-size lint still pass.
- [x] 9.2 Run `rtk make test` (or targeted `rtk dotnet test tests/ArchLinterNet.Core.Tests --no-restore` and `rtk dotnet test tests/ArchLinterNet.Cli.Tests --no-restore`) and confirm all new and existing tests pass.
- [x] 9.3 Run `rtk make acceptance` as the final gate.
- [x] 9.4 Run `openspec validate --all` after archiving.
