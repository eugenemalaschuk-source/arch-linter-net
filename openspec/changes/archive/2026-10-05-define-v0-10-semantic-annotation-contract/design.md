## Context

See `proposal.md` for the v0.10 motivation. The current classifier maps user-owned attributes by exact full name, resolves one winning role through the existing source precedence, and feeds a shared role index. The current catalog says no package exists; #565 now depends on a source-only package and child issues #567–#574 must implement one consistent contract. #525 keeps reviewed public API membership orthogonal to semantic role.

The release lifecycle is capability planning for an unreleased v0.10.0 wave. This change fixes product semantics and implementation boundaries; package/runtime code and publication remain outside its scope.

## Goals / Non-Goals

**Goals:**

- Freeze the v0.10 source package form, canonical role identities, role subset, bounded metadata, deterministic conflict/precedence behavior, version skew, and drift prevention.
- Keep canonical annotations on the existing attribute evidence and role-index path.
- Give #567–#574 a machine-readable catalog and versioned internal authority.

**Non-Goals:**

- Implementing the NuGet package, Core recognition, a source generator, or package validation.
- Adding a second classifier, multi-role tags, arbitrary metadata keys, runtime inference, automatic policy generation, or annotation-based API membership.
- Rewriting existing custom mapping conflict behavior.

## Decisions

### Source-only distribution and opt-in

Use `ArchLinterNet.Annotations` as an optional NuGet package containing source under `contentFiles/cs/any/`, with `buildAction=Compile` and `copyToOutput=false`. A project that authors annotations references the package directly; Central Package Management may centralize the version. Do not ship `lib/`, `ref/`, runtime, analyzer, source-generator, or executable build assets. Do not rely on `buildTransitive` to inject source into unrelated projects. Each opting-in project compiles its own internal definitions. The linter recognizes the emitted metadata names and does not load an `ArchLinterNet.Annotations` assembly.

All injected attribute types are `internal sealed`, `AllowMultiple=false`, and `Inherited=false`. Role attributes have a public parameterless constructor and six optional public settable string properties: `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, and `Runtime`. Metadata-only context attributes have one required public string constructor argument and represent only their corresponding key. Role attributes target classes, structs, and interfaces or assemblies listed in the canonical manifest; no member-level annotations are defined. Only the first-class entries in manifest `roles[]` have canonical role attribute FQNs. Catalog entries classified as `customMappingOnly` or `deferred` do not imply an attribute FQN. `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `UnityRuntime`, and `UnityEditor` are assembly-only. `CompositionRoot` supports type or assembly use. Other canonical roles are type-only. Context-only metadata attributes support type and assembly targets. Each package-using assembly carries an internal `AnnotationCatalogIdentityAttribute` with the package's catalog generation and exact SemVer; the initial v0.10.0 package emits `(1, "0.10.0")`. This assembly marker exposes compatibility metadata only and supplies no role or context evidence.

This preserves package opt-in for SDK-style consumers, avoids a runtime dependency, and keeps the package-provided declarations out of exported public API. A project using annotations in multiple projects adds the direct package reference to each such project; a shared package version avoids identity drift. The v0.10 package guarantee covers SDK-style projects. Non-SDK and Unity project injection is outside this package contract; those consumers retain the user-owned attribute and YAML-mapping path.

Alternatives rejected: a binary annotation library creates a runtime/reference identity; analyzer/source-generator distribution adds an executable build component and another compatibility surface; `buildTransitive` can inject definitions into projects that did not opt in; public annotation classes pollute consumer API snapshots.

### Canonical identities and catalog authority

Reserve `ArchLinterNet.Annotations`. Each first-class role listed in manifest `roles[]` maps to the FQN recorded in that entry; catalog disposition names in `customMappingOnly` or `deferred` do not imply role FQNs. The six context-only names are `DomainAttribute`, `BoundedContextAttribute`, `ModuleAttribute`, `FeatureAttribute`, `PlatformAttribute`, and `RuntimeAttribute`. `FeatureAttribute` is reserved for the metadata-only `feature` key; catalog role `Feature` remains custom-mapping-only and has no role attribute FQN. The assembly identity marker is `ArchLinterNet.Annotations.AnnotationCatalogIdentityAttribute`, with catalog generation and package SemVer constructor arguments. Matching is exact FQN metadata matching. Simple-name, namespace-prefix, inheritance, and assembly-identity discovery are prohibited.

The reviewed machine source of truth is [the v1 annotation catalog](../../../architecture/semantic-annotations.v1.json). It records the canonical roles and each family's FQN, target, supported metadata properties, `catalogGeneration`, package identity, non-first-class role dispositions, and compatibility marker. #567/#568/#573 must generate or mechanically validate package source, Core mapping, capability metadata, and the human-readable reference against this file; CI fails when a projection drifts. The catalog generation is separate from package SemVer and is never inferred from it.

An annotation-aware tool also treats the canonical namespace as reserved: an unknown type under it is an unsupported annotation diagnostic, never an opportunity for simple-name fallback. YAML cannot remap a canonical FQN, even redundantly, because that would make a stable identity policy-dependent.

### Curated v0.10 role set

Promote the following 49 catalog roles to first-class annotations:

| Family | Roles |
| --- | --- |
| Layered/clean | `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `CompositionRoot` |
| DDD | `Entity`, `AggregateRoot`, `ValueObject`, `DomainService`, `DomainEvent`, `Repository`, `Specification`, `Factory` |
| CQRS/messages | `Command`, `CommandHandler`, `Query`, `QueryHandler`, `Event`, `EventHandler`, `IntegrationEvent`, `ReadModel` |
| Web/API | `Controller`, `Endpoint`, `RequestDto`, `ResponseDto`, `ApiContract`, `Validator` |
| Ports/infrastructure | `Port`, `Adapter`, `AntiCorruptionLayer`, `DbContext`, `RepositoryImplementation`, `ExternalClient`, `MessageBusAdapter`, `FileSystemAdapter`, `ClockAdapter` |
| UI | `View`, `ViewModel`, `Presenter` |
| Unity/client | `UnityRuntime`, `UnityEditor`, `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, `SceneAdapter` |

Every other role in the existing catalog receives exactly one annotation disposition: custom-mapping-only when its intended meaning is valid but project/organization-specific, and deferred when it depends on framework/runtime behavior or remains materially ambiguous. The public catalog keeps its existing five vocabulary-support tiers and adds the v0.10 disposition as a separate axis.

`DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `UnityRuntime`, and `UnityEditor` are assembly-only; `CompositionRoot` supports type and assembly; other roles are type-only. Type targets are class, struct, or interface declarations as recorded per role by the manifest. The annotations do not inspect runtime behavior. In particular, `ApiContract` is a genuine primary semantic role only; it does not select reviewed API members.

Alternatives rejected: shipping every catalog row would promote ambiguous and framework-specific names; a binary `PublicApi` marker or treating `ApiContract` as membership would violate #525; allowing a type to stack a layer role with a DDD role would turn the single-role model into an implicit tag system.

### Primary-role conflicts

Canonical role annotations enter the existing type-attribute/assembly-attribute evidence tiers and the existing role index. Exact equivalent evidence at one specificity coalesces and retains provenance. Two different roles or incompatible metadata at the same specificity produce deterministic conflict evidence and no effective role for that type; resolution does not depend on attribute enumeration order. A canonical role and a distinct custom-mapped attribute at the same tier use this same rule. An explicit YAML mapping that names a reserved canonical FQN is invalid.

The existing source precedence remains `yaml_override > type_attribute > assembly_attribute > inheritance > namespace > path`. Thus a type role outranks an assembly role; role metadata from the lower-precedence role source is not merged. Existing custom-only YAML mapping entries keep their reviewed first-declared conflict behavior for compatibility.

### Bounded context metadata

Support exactly `domain`, `boundedContext`, `module`, `feature`, `platform`, and `runtime` in canonical role properties and metadata-only attributes. Role properties use PascalCase C# names corresponding to the canonical lower/camel-case keys and are named arguments, not positional constructor values. Metadata-only attributes take one required compile-time string value. Values are exact non-empty strings; the classifier does not normalize values or infer them from names. An empty supplied value is a metadata extraction failure: omit that key, retain an otherwise valid role, and report the failure.

Composition is: metadata from the winning role evidence, then type-scope metadata-only attributes, then assembly-scope metadata-only attributes for keys still absent. Equal values coalesce with provenance. A type-scope value overrides an assembly default. Conflicting values at the same scope emit a deterministic metadata conflict and omit only that key while retaining an otherwise valid role. Lower-precedence role metadata does not supply defaults. Custom YAML mappings remain the path for other metadata keys.

Metadata-only assembly attributes are accepted because they let one assembly provide context defaults without consuming the type's role slot. A property bag, open-ended key attribute, multi-role accumulation, or lower-role metadata merge is rejected.

### Package/tool compatibility

The first package version is `0.10.0`, catalog generation `1`, and it embeds the assembly-targeted `AnnotationCatalogIdentityAttribute(1, "0.10.0")` marker in each opted-in consumer assembly. Later package versions embed their own exact SemVer while retaining the applicable catalog generation. The generation-1 compatible package range is `[0.10.0,0.11.0)`. Package SemVer follows the product release train; `0.10.x` preserves role identities, meanings, targets, constructors, and named metadata properties. Package SemVer does not encode catalog generation. The marker makes both independent values observable in the target assembly, including target_assemblies-only scans without project metadata. A canonical annotation in an assembly without exactly one valid marker, with an unsupported generation, or with a malformed/out-of-range package version produces a deterministic compatibility diagnostic and is not resolved. A future catalog identity or semantic addition increments the catalog generation and ships with tool capability metadata in the matching capability release; removals, renames, target changes, and role reassignment require explicit compatibility notes and migration guidance.

`archlinternet.capabilities.json` reports exact FQNs, supported catalog generations, supported package SemVer ranges, and compatibility diagnostics. Annotation-aware tools fail closed on a generation or package version they do not support. Newer tools accept older catalog identities only while both generation and package version remain in the supported sets. Package documentation sets v0.10.0 as the minimum tool version. Pre-v0.10 tools cannot be made to understand a source-only catalog identity marker retroactively; that boundary is explicit rather than promising a diagnostic from tools that contain no annotation support.

### Follow-up ownership

- #567 owns one source-only NuGet packaging mechanism and packed consumer proof.
- #568 owns exact FQN recognition in the existing classifier, reserved-identity validation, generation diagnostics, and manifest parity.
- #569 owns the layered/clean and DDD source definitions.
- #570 owns the CQRS/message and web/API definitions, preserving the #525 boundary.
- #571 owns the ports/infrastructure/UI/Unity definitions without framework runtime claims.
- #572 owns the six-key metadata source and composition rules.
- #573 owns adoption/compatibility documentation and generated or parity-tested role references.
- #574 owns the packed artifact compatibility and consumer exit gate.

## Risks / Trade-offs

- [Risk] Older tools cannot recognize source-only annotation metadata. → Mitigation: package metadata and documentation state v0.10.0 as the minimum annotation-aware tool; supported newer-generation skew is diagnosed by annotation-aware releases.
- [Risk] The curated list is sizeable and can become a taxonomy burden. → Mitigation: keep every other role custom-only or deferred, prohibit runtime inference, and add roles only through a new reviewed catalog generation.
- [Risk] Separate metadata-only attributes expand the package surface and conflict model. → Mitigation: limit them to six named keys and one type/assembly precedence rule with same-scope conflicts failing closed.
- [Risk] Repeated source injection in multi-project solutions can be mistaken for a shared attribute assembly. → Mitigation: require direct opt-in per authoring project and define recognition by exact metadata FQN rather than CLR identity.
