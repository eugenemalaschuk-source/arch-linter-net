## 1. Configuration model

- [x] 1.1 Add `ArchitectureClassificationConfiguration`, `ArchitectureAttributeClassificationMapping` to `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`, wired as `ArchitectureContractDocument.Classification` (`[YamlMember(Alias = "classification")]`), defaulting to an empty configuration.
- [x] 1.2 Confirm `ArchitecturePolicyDocumentLoader` binds the new section via YamlDotNet without additional converters; add one if the raw `metadata` dictionary doesn't deserialize as `Dictionary<string, string>` cleanly.
- [x] 1.3 Unit test: a policy with no `classification` section binds to an empty configuration and existing policy tests remain green.

## 2. Metadata extraction dispatch

- [x] 2.1 Implement the fixed-order metadata-expression dispatcher (`constructor[<index>]`, `property:<Name>`, `const:<Full.Type.NAME>`, literal fallback) against a `CustomAttributeData` instance.
- [x] 2.2 Implement `constructor[<index>]` reading `CustomAttributeData.ConstructorArguments` by index, including out-of-range as an evidence-extraction failure.
- [x] 2.3 Implement `property:<Name>` reading only `CustomAttributeData.NamedArguments`, never the attribute type's declared members/defaults.
- [x] 2.4 Implement `const:<Full.Type.NAME>` resolving a `FieldInfo` with `IsLiteral && !IsInitOnly`, reading via `GetRawConstantValue()`; anything else (including `static readonly`, unresolved type/member) is an evidence-extraction failure. (Extended during implementation: `const decimal` fields compile as `static initonly` with `[DecimalConstant]`, not IL `literal`, so that shape is also accepted as compile-time const, per the spec's explicit const-decimal scenario.)
- [x] 2.5 Implement value canonicalization into `string | bool | decimal | null` (null = failure): `System.Type` → `FullName`, enum → declared member name only for an unambiguous underlying value, every CLR numeric primitive and YAML/JSON numeric literal → `decimal`, unsupported shapes (array, attribute-typed, null, NaN/Infinity) → failure.
- [x] 2.6 Unit tests for each of the four forms, including every failure-path scenario from `specs/attribute-role-extraction/spec.md`.

## 3. Type-level and assembly-level extraction

- [x] 3.1 Implement type-level extraction: for a scanned `Type`, enumerate `GetCustomAttributesData()`, match against `classification.attributes` entries by full attribute type name, applying the same defensive try/catch envelope (`TypeLoadException`, `FileNotFoundException`, `CustomAttributeFormatException`) as `ArchitectureAttributeUsageScanner`.
- [x] 3.2 Implement assembly-level extraction: for a scanned `Assembly`, enumerate `GetCustomAttributesData()`, match against `classification.assembly_attributes` entries, computed once per assembly and reused across its types.
- [x] 3.3 Implement same-tier conflict resolution within one mapping list: first-declared YAML entry wins, discarded alternative recorded as a conflict fact.
- [x] 3.4 Implement repeated-attribute-instance resolution: multiple `CustomAttributeData` instances of one mapped attribute resolve by their order in `GetCustomAttributesData()`; identical role+metadata across instances is not a conflict.
- [x] 3.5 Implement `type_attribute > assembly_attribute` precedence: type-level result wins when both sources contribute a role for one type.
- [x] 3.6 Define and implement the per-type extraction result shape (role, canonicalized metadata, conflict facts, evidence-extraction-failure facts).
- [x] 3.7 Unit tests covering: type-attribute-only, assembly-attribute-only, both present (precedence), no match, same-tier conflicts, repeated-instance conflicts (differing and identical).

## 4. Fixtures and integration tests

- [x] 4.1 Add fixture attribute types and decorated fixture types/assemblies mirroring existing scanner test conventions (constructor-arg metadata, named-property metadata, const-field metadata, literal metadata, conflicting mappings, repeated attribute instances).
- [x] 4.2 Add end-to-end test(s) loading a YAML policy with `classification.attributes`/`classification.assembly_attributes` and asserting extraction results against the fixtures.
- [x] 4.3 Confirm existing architecture-contract/policy-loading test suites remain unaffected (no regression for policies without `classification`).

## 5. Spec sync and archive

- [x] 5.1 Reconcile `openspec/changes/add-attribute-role-extraction/specs/` delta files with whatever implementation details shifted during coding.
- [x] 5.2 Run `openspec validate add-attribute-role-extraction --strict` and `openspec validate --all`.
- [x] 5.3 Run `openspec archive add-attribute-role-extraction`.

## 6. Validation gate

- [x] 6.1 Run `make fmt`.
- [x] 6.2 Run `make acceptance`; fix failures.
