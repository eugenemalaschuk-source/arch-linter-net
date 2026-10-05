# v0.10 semantic annotation contract

This document records the reviewed design contract for issues #567–#574 in the
unreleased v0.10.0 capability wave. The package and built-in recognition are
approved for implementation but are not available in released tools. This
document and the
[machine-readable catalog](../../architecture/semantic-annotations.v1.json)
are the design authority for that work; the manifest owns exact identities,
targets, metadata properties, and catalog disposition.

## Package and adoption

The optional package ID is `ArchLinterNet.Annotations`, with initial version
`0.10.0`. It is source-only and targets supported SDK-style projects through
NuGet `contentFiles/cs/any/**/*.cs`, with `buildAction=Compile` and
`copyToOutput=false`. It ships no `lib/`, `ref/`, runtime, analyzer, source
generator, `build`, or `buildTransitive` asset. A project that authors the
annotations references the package directly; Central Package Management may
centralize the version but does not substitute for that direct reference. The
source is compiled separately into each opting-in project, including each
authoring project in a multi-project solution. The linter recognizes exact
metadata names and does not load a shared annotation assembly. The v0.10 package
guarantee covers SDK-style projects. Non-SDK and Unity source injection is
outside this package contract; those consumers retain the user-owned attribute
and YAML-mapping path.

The package is not required. User-owned attributes mapped by exact full type
name in `classification.attributes` or `classification.assembly_attributes`
remain a first-class path. The package does not grant policy permissions or
change enforcement by itself.

## Stable identity and source shape

The reserved namespace is `ArchLinterNet.Annotations`. A role named `<Role>`
uses `ArchLinterNet.Annotations.<Role>Attribute`. The six metadata-only
identities are `DomainAttribute`, `BoundedContextAttribute`, `ModuleAttribute`,
`FeatureAttribute`, `PlatformAttribute`, and `RuntimeAttribute`. Recognition
uses exact fully qualified metadata names. Simple-name guessing, namespace
prefix matching, inheritance matching, and defining-assembly identity are not
recognition rules.

All injected attribute types are `internal sealed`, apply once, and are not
inherited. Role attributes have a public parameterless constructor and six
optional public settable string properties: `Domain`, `BoundedContext`,
`Module`, `Feature`, `Platform`, and `Runtime`. Constructors and named
properties are public on the internal attribute types so C# attribute syntax
can use them without adding consumer public API. These property names map
exactly to the keys `domain`, `boundedContext`, `module`, `feature`, `platform`,
and `runtime`. Values are compile-time, non-empty strings, compared exactly
without normalization or inference. An empty supplied value omits that key,
retains an otherwise valid role, and is reported as a metadata extraction
failure. Metadata-only attributes have one required public string constructor
argument named `value`, carry only their corresponding metadata key, and accept
the same non-empty-value rule. They do not create a role. The metadata-only FQN
`FeatureAttribute` represents the `feature` context key; the separate catalog
role `Feature` remains custom-mapping-only and has no first-party role
attribute.

Every package-using consumer assembly carries
`ArchLinterNet.Annotations.AnnotationCatalogGenerationAttribute(1)`. The marker
is internal sealed, assembly-targeted, constructed by a public constructor, and
present once. An annotation-aware tool
recognizes generation 1 as defined by the manifest. Unknown generation or
unknown attribute FQN under the reserved namespace produces a deterministic
unsupported-annotation diagnostic rather than a guessed role.

### Attribute target matrix

Role attributes target classes, structs, and interfaces unless the table says
assembly. Assembly-targeted roles are valid only on assemblies. The table is
the readable projection of the exact identities and targets in the manifest.

| Scope | Roles |
| --- | --- |
| Assembly only | `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `UnityRuntime`, `UnityEditor` |
| Type and assembly | `CompositionRoot` |
| Type only | All remaining roles in the manifest |
| Type and assembly, metadata only | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` |

The six metadata-only attributes target type declarations and assemblies. No
annotation targets members or parameters.

## Approved v0.10 role set

The 49 first-class roles are:

| Family | Roles |
| --- | --- |
| Layered/clean | `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `CompositionRoot` |
| DDD | `Entity`, `AggregateRoot`, `ValueObject`, `DomainService`, `DomainEvent`, `Repository`, `Specification`, `Factory` |
| CQRS/messages | `Command`, `CommandHandler`, `Query`, `QueryHandler`, `Event`, `EventHandler`, `IntegrationEvent`, `ReadModel` |
| Web/API | `Controller`, `Endpoint`, `RequestDto`, `ResponseDto`, `ApiContract`, `Validator` |
| Ports/infrastructure | `Port`, `Adapter`, `AntiCorruptionLayer`, `DbContext`, `RepositoryImplementation`, `ExternalClient`, `MessageBusAdapter`, `FileSystemAdapter`, `ClockAdapter` |
| UI | `View`, `ViewModel`, `Presenter` |
| Unity/client | `UnityRuntime`, `UnityEditor`, `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, `SceneAdapter` |

The existing catalog's other roles are explicitly classified as
custom-mapping-only or deferred in both the manifest and public catalog. That
disposition is independent of the existing vocabulary support tier. It prevents
the first-party set from growing by implication.

## Primary roles and conflicts

Canonical role evidence enters the existing classification pipeline and role
index. It uses the established source order:

```text
yaml_override > type_attribute > assembly_attribute > inheritance > namespace > path
```

Within one specificity, equivalent canonical evidence coalesces and retains
provenance. Contradictory role evidence or conflicting role metadata produces a
deterministic conflict and no effective role; attribute enumeration order does
not select a winner. Canonical and distinct custom-mapped evidence follow the
same rule when they have equal specificity. Existing conflicts among
custom-mapping-only YAML entries retain their reviewed first-declared behavior.
YAML cannot remap an FQN reserved by the canonical manifest.

A type-level role outranks an assembly-level role. Metadata from a losing
assembly role is not merged into the winning type role. At most one primary
role is effective for a type.

## Context metadata

Canonical first-party context is limited to six keys: `domain`,
`boundedContext`, `module`, `feature`, `platform`, and `runtime`. Role attribute
properties and matching metadata-only attributes are the only first-party
annotation sources for these keys. Other metadata keys continue to work
through explicit custom YAML mappings.

Metadata composes in this order:

1. Metadata on the winning role evidence.
1. Type-scope metadata-only attributes for keys still absent.
1. Assembly-scope metadata-only attributes as defaults for keys still absent.

Equal values coalesce and retain provenance. A type-scope value overrides an
assembly default without conflict. Different values at the same scope produce
a deterministic metadata conflict and omit only that key while preserving an
otherwise valid role. Lower-precedence role evidence never supplies missing
metadata to a winning role. Context attributes do not consume or create a
primary role.

## Versioning and compatibility

Package SemVer and catalog generation are independent. The initial package is
`0.10.0`, catalog generation is `1`, and the generation marker is emitted in
each authoring assembly. The package range compatible with generation 1 is
`[0.10.0,0.11.0)`. Within that range, role FQNs and meanings, targets,
constructors, and metadata property names/types remain stable. The manifest
records the package range and marker.

Annotation-aware tool capability metadata advertises supported catalog
generations and package range. A tool fails closed with a deterministic
diagnostic for an unsupported newer generation or an unknown canonical FQN.
Newer tools accept older generations only while they remain in their advertised
support set. Package guidance names v0.10.0 as the minimum annotation-aware tool
version. Older tools cannot diagnose source-only annotation metadata
retroactively, so pre-v0.10 use is documented as unsupported rather than
promising a diagnostic they cannot produce.

An identity or semantic addition requires a reviewed catalog-generation change
and matching tool capability update. Renames, removals, target changes, and
role reassignment require explicit compatibility and migration guidance.

## API and policy boundary

`ApiContract` is a genuine primary semantic role only when that describes the
type. It does not select reviewed API membership. Public API membership remains
the independent selector decision defined by #525; an `Entity` or `ValueObject`
can belong to a reviewed public API without being reclassified. No annotation
grants dependency permission, creates policy, changes strict/audit behavior,
creates ignores or baselines, generates YAML, or implies runtime behavior.

## Implementation ownership

Issues #567–#574 divide implementation as follows:

| Issue | Ownership |
| --- | --- |
| #567 | Source-only NuGet package and packed SDK-style consumer proof. |
| #568 | Exact FQN recognition, reserved-identity validation, generation diagnostics, manifest parity in Core. |
| #569 | Layered/clean and DDD source definitions. |
| #570 | CQRS/message and web/API definitions, preserving the #525 boundary. |
| #571 | Ports, infrastructure, UI, and Unity definitions without runtime framework claims. |
| #572 | Six-key metadata extraction, metadata-only sources, and composition/conflicts. |
| #573 | Adoption/compatibility docs and generated or parity-tested role reference. |
| #574 | Packed-artifact compatibility and consumer exit gate. |

Package/runtime implementation and release publication are outside the scope of
this design change. The package is not shipped until its implementation issues
and the v0.10 release lifecycle authorize those steps.
