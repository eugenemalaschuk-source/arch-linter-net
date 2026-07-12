## Why

`classification.namespace` and `classification.inheritance` are already fully defined by the reviewed schema (`schema/dependencies.arch.schema.json`) and the `semantic-classification-model` design spec, but declaring them today has no runtime effect — the spec explicitly reserves their execution for "their own execution capabilities land" (Requirement "Runtime behavior is introduced only for implemented classification sources and layer selectors"). Issue #113 asks for that runtime to land now for namespace and inheritance conventions, so teams can adopt opt-in, deterministic role/metadata inference from existing namespaces and framework base types without adding explicit attributes everywhere. `classification.path` is excluded from this wave because it depends on issue #171 (source file/declared-type fact index), which is still open; per the issue's own non-goals, path conventions require deterministic source/project discovery that doesn't exist yet.

## What Changes

- Add runtime resolution for `classification.namespace` entries (namespace/namespace_suffix glob matching), reusing the existing `ArchitectureLayerResolver`/`NamespaceGlobPattern` glob machinery via delegate injection from `Execution` into `Scanning`, since `Scanning` must not depend on `Resolution` directly.
- Add runtime resolution for `classification.inheritance` entries (base_type full-name resolution plus transitive class/interface matching via `Type.IsAssignableFrom`).
- Generalize `ArchitectureAttributeRoleExtractor`'s source-combination logic from a hardcoded 2-tier (type_attribute > assembly_attribute) precedence into an N-tier walk covering all four now-implemented sources: type_attribute > assembly_attribute > inheritance > namespace, driven by `classification.precedence`.
- Add `Inheritance` and `Namespace` members to `ArchitectureClassificationSource`.
- Bind `classification.inheritance` and `classification.namespace` YAML sections to real configuration model types (previously ignored by the deserializer as unbound/inert sections).
- Restrict metadata extraction for inheritance/namespace entries to literal scalar and `const:Full.Type.NAME` forms only (no `constructor[]`/`property:`, since neither source has an attribute instance to extract from).
- Apply the existing same-tier (within one source list), declaration-order conflict resolution rule to the two new sources, consistent with how `classification.attributes`/`classification.assembly_attributes` already behave.
- Add a new informational, non-fatal diagnostic when a policy declares a non-empty `classification.path` section, explaining that path-convention classification is deferred pending issue #171 and produces no role assignment. This does not fail `validate`.
- Update the `semantic-classification-model` spec and `docs/policy-format/semantic-classification.md` / `docs/policy-format/supported-capabilities.md` to reflect the new implemented/reserved boundaries and add convention examples (modular monolith, clean architecture, ASP.NET-like, EF-like, Unity/client-like, legacy gradual adoption).

No breaking changes: existing policies with no `classification.namespace`/`classification.inheritance` sections, or with only `classification.attributes`/`classification.assembly_attributes`, behave identically to today.

## Capabilities

### New Capabilities
(none — this change extends an existing capability)

### Modified Capabilities
- `semantic-classification-model`: `classification.namespace` and `classification.inheritance` move from schema-accepted-but-inert to executed classification sources, participating in the fixed source precedence and same-tier conflict resolution alongside `type_attribute`/`assembly_attribute`. `classification.path` remains unimplemented but gains a new informational deferred-diagnostic behavior distinguishing it from the fully-silent `overrides`/`exclusions` reserved sections.

## Impact

- `src/ArchLinterNet.Core/Model/ArchitectureClassificationFacts.cs` — new enum members.
- `src/ArchLinterNet.Core/Contracts/ArchitectureClassificationModels.cs` — new bound mapping types and configuration properties.
- `src/ArchLinterNet.Core/Scanning/ArchitectureAttributeRoleExtractor.cs` — generalized precedence-tier resolution; new inheritance/namespace candidate resolution methods.
- `src/ArchLinterNet.Core/Scanning/ArchitectureAttributeMetadataExtraction.cs` — new no-attribute-instance extraction entry point.
- `src/ArchLinterNet.Core/Execution/ArchitectureRoleIndex.cs` — constructs and injects the namespace-matching delegate; extends the empty-config guard.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs` — new raw-YAML `classification.path` presence detection producing the deferred diagnostic.
- `openspec/specs/semantic-classification-model/spec.md` — requirement/scenario updates (via archive of this change's delta spec).
- `docs/policy-format/semantic-classification.md`, `docs/policy-format/supported-capabilities.md` — documentation updates.
- `tests/ArchLinterNet.Core.Tests/ArchitectureRoleIndexTests.cs`, `ArchitectureAnalysisSessionClassificationTests.cs`, and the `AttributeRoleExtractionTestFixtures` fixture project — new test coverage.
- No new external dependencies. No changes to `schema/dependencies.arch.schema.json` (already fully defines these shapes).
