## Why

Story #106's semantic classification model was designed (#107) and its schema/vocabulary reviewed, but no extraction logic exists yet — `classification.attributes` and `classification.assembly_attributes` are schema-accepted no-ops. #108 confirmed ArchLinterNet ships no binary annotation package, so attribute-based classification must work against arbitrary user-defined attributes mapped by full type name. Issue #109 implements the first classification source pair (type-level and assembly-level attributes) so later work (#110+ namespace/inheritance/path sources, #111/#112 contextual contracts, selector consumption) has real role/metadata facts to consume.

## What Changes

- Add C# model binding for `classification.attributes` and `classification.assembly_attributes` on `ArchitectureContractDocument`, following the schema already reviewed in `openspec/specs/semantic-classification-model/spec.md` (`attribute`, `role`, `metadata` map of extraction expressions).
- Implement type-level attribute extraction: scan `CustomAttributeData` on each scanned type for attributes mapped by full type name.
- Implement assembly-level attribute extraction: scan `CustomAttributeData` on each scanned assembly for attributes mapped by full type name.
- Implement the fixed four-form metadata extraction syntax: `constructor[<index>]`, `property:<Name>`, `const:<Full.Type.NAME>`, literal YAML scalar fallback.
- Implement canonicalization of extracted/literal metadata values into the three fixed comparable domains: string, boolean, decimal.
- Implement source precedence limited to the two sources this issue adds: `type_attribute` overrides `assembly_attribute` for the same type.
- Implement same-tier conflict resolution: within `classification.attributes` (and separately within `classification.assembly_attributes`), first-declared YAML entry wins on conflict; record the discarded alternative as a `conflict` fact.
- Implement repeated-attribute-instance resolution: when a type/assembly carries multiple instances of one mapped attribute, resolve by `CustomAttributeData` metadata order (first instance wins); identical instances are not a conflict.
- Implement uniform evidence-extraction-failure handling: a failed metadata key is omitted (not fabricated/defaulted), the role assignment still proceeds, and the failure is recorded as an explainable diagnostic fact.
- **MODIFIED**: narrow the existing "No runtime behavior is introduced by this design" requirement — it no longer holds for the `type_attribute`/`assembly_attribute` sources, which this change makes fully functional; it continues to hold for `inheritance`, `namespace`, `path`, `classification.overrides`, `classification.exclusions`, and `layers.<name>.selector` consumption, which remain unimplemented no-ops until later issues.

## Capabilities

### New Capabilities
- `attribute-role-extraction`: type-level and assembly-level attribute-based semantic role/metadata extraction — YAML mapping model, `CustomAttributeData` scanning, the four-form metadata extraction syntax, value canonicalization, `type_attribute`/`assembly_attribute` precedence, same-tier and repeated-instance conflict resolution, and uniform evidence-extraction-failure handling.

### Modified Capabilities
- `semantic-classification-model`: narrows the "No runtime behavior is introduced by this design" requirement to exclude the now-implemented `type_attribute`/`assembly_attribute` sources.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` (or a new sibling file): new model classes for `classification`, `classification.attributes`, `classification.assembly_attributes` entries, wired onto `ArchitectureContractDocument`.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: no change expected (YamlDotNet binds new properties automatically) unless custom converters are needed for the metadata-expression syntax.
- `src/ArchLinterNet.Core/Scanning/`: new extraction/scanning code, following the pattern of `ArchitectureAttributeUsageScanner.cs` and `ArchitectureTypeScanner.cs`.
- New unit tests and fixture attributes/types/assemblies under the existing test project(s) mirroring current scanner/contract test conventions.
- No breaking changes to existing policies: a policy with no `classification` section is unaffected.
