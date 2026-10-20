# semantic-annotation-contract Specification

## Purpose

Defines the optional v0.10 source-only annotation package, the stable identities and compatibility contract that consumers and ArchLinterNet tooling rely on, and the boundaries that keep annotations separate from policy authority and reviewed API membership.

## Requirements

### Requirement: The optional annotations package is source-only and internal by default

ArchLinterNet SHALL provide the v0.10 canonical annotations as an optional NuGet package for supported SDK-style projects. The package SHALL compile-inject source through NuGet content files and SHALL NOT ship or require a separate annotation runtime assembly, analyzer, source generator, or runtime service. Every project that authors these attributes SHALL opt in directly; central package management MAY centralize the version. Injected annotation types SHALL be internal so installing the package does not add supported public API declarations to the consumer.

#### Scenario: An opted-in SDK-style project compiles canonical annotations

- **WHEN** an SDK-style project directly references `ArchLinterNet.Annotations`
- **THEN** restore includes the package's source files in compilation without requiring a binary annotation reference or executable build component

#### Scenario: Package installation does not add a runtime annotation asset

- **WHEN** a consumer restores, builds, and publishes a project that directly references the package
- **THEN** the package contributes no annotation DLL to the consumer's runtime dependency closure and its internal source definitions do not expand the consumer's supported public API surface

#### Scenario: Multi-project consumers opt in independently

- **WHEN** multiple projects in one solution author canonical annotations
- **THEN** each such project directly references the same package version, compiles its own internal source definitions, and remains recognizable by exact metadata name without requiring shared CLR type identity

### Requirement: Canonical annotation identities are exact and have one reviewed source of truth

The reserved namespace SHALL be `ArchLinterNet.Annotations`. Each first-class role entry in the manifest's `roles[]` SHALL use the exact FQN recorded in that entry, following `ArchLinterNet.Annotations.<FirstClassRole>Attribute`. Roles listed only under `annotationDisposition.customMappingOnly` or `annotationDisposition.deferred` SHALL NOT imply a canonical role attribute FQN. Role attributes SHALL be internal sealed types with a parameterless constructor and optional named string properties `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, and `Runtime`. Context-only attributes SHALL use `ArchLinterNet.Annotations.DomainAttribute`, `BoundedContextAttribute`, `ModuleAttribute`, `FeatureAttribute`, `PlatformAttribute`, or `RuntimeAttribute` and SHALL accept one required string constructor argument. In particular, `FeatureAttribute` SHALL be metadata-only for the `feature` key; catalog role `Feature` is custom-mapping-only and has no canonical role attribute. Each package-using assembly SHALL carry one internal assembly marker whose values are that package's catalog generation and exact SemVer; the initial v0.10.0 package SHALL emit `ArchLinterNet.Annotations.AnnotationCatalogIdentityAttribute(1, "0.10.0")`. The marker SHALL serve only as compatibility metadata and SHALL NOT contribute role or context evidence. Recognition SHALL match exact metadata fully qualified names and SHALL NOT guess by simple name, namespace prefix, inheritance, or defining assembly identity. The versioned canonical catalog manifest SHALL be the reviewed source of truth for injected identities, roles, targets, metadata keys, catalog generation, and package version. Package source, Core mappings, capability metadata, and documentation SHALL be generated from or mechanically checked against that manifest, with drift failing validation.

#### Scenario: Only first-class manifest roles receive canonical role FQNs

- **WHEN** a role appears in `annotationDisposition.customMappingOnly` or `annotationDisposition.deferred` but not in manifest `roles[]`
- **THEN** the role has no built-in role attribute FQN and remains available through its existing custom-mapping path

#### Scenario: Feature context metadata does not collide with a Feature role

- **WHEN** a type carries `ArchLinterNet.Annotations.FeatureAttribute` and catalog role `Feature` remains custom-mapping-only
- **THEN** the attribute contributes only the `feature` context key and does not imply a `Feature` role

#### Scenario: Canonical annotation is recognized by its exact FQN

- **WHEN** a scanned type carries `ArchLinterNet.Annotations.AggregateRootAttribute`
- **THEN** an annotation-aware tool recognizes the exact FQN as the canonical `AggregateRoot` role without requiring a YAML mapping

#### Scenario: A same-simple-name user attribute is not canonical

- **WHEN** a scanned type carries `MyCompany.Architecture.AggregateRootAttribute`
- **THEN** ArchLinterNet does not treat it as canonical by simple name and the existing user-owned full-name YAML mapping path remains available

#### Scenario: An unknown FQN in the reserved namespace fails visibly

- **WHEN** an annotation-aware tool encounters an unrecognized attribute FQN in `ArchLinterNet.Annotations`
- **THEN** it reports deterministic unsupported-canonical-annotation evidence and does not silently assign a different role

#### Scenario: A projected catalog drifts from the manifest

- **WHEN** package source, Core's mapping catalog, capability metadata, or the reviewed documentation disagrees with the versioned manifest
- **THEN** the repository validation added by the implementation wave fails with the mismatched projection identified

### Requirement: Package and tool version skew is explicit and diagnosable

The first package release SHALL use catalog generation `1` and package version `0.10.0`. Each consumer assembly compiled with a package SHALL carry one catalog identity marker containing that package's catalog generation and exact SemVer. Tool capability metadata SHALL advertise the generations and package SemVer ranges it recognizes, and package metadata SHALL identify the minimum annotation-aware tool release. Since both values are in assembly metadata, `target_assemblies`-only analysis SHALL NOT require project-file or `PackageReference` metadata to diagnose compatibility. When a known canonical annotation FQN is present, an assembly without exactly one valid marker, with an unsupported generation, or with a malformed or unsupported package SemVer SHALL produce a deterministic diagnostic and the canonical annotation evidence SHALL NOT be resolved. Newer tools SHALL accept older catalog identities only while both generation and package version remain in the tool's advertised support set. The automatic skew-diagnostic guarantee begins with v0.10.0 annotation-aware tools; package guidance SHALL identify v0.10.0 as the minimum supported tool version.

#### Scenario: Supported v0.10 package and tool agree

- **WHEN** a v0.10.0-or-newer annotation-aware tool analyzes a consumer whose marker reports package version `0.10.0` and catalog generation `1`
- **THEN** the tool recognizes the generation and resolves the packaged canonical identities it advertises

#### Scenario: An older annotation-aware tool sees a newer generation

- **WHEN** an annotation-aware tool encounters a consumer assembly whose marker reports a catalog generation greater than the tool supports
- **THEN** it reports an unsupported-generation diagnostic and does not silently classify unknown evidence

#### Scenario: Canonical annotation without an identity marker fails closed

- **WHEN** an assembly contains a known canonical annotation FQN but has no catalog identity marker
- **THEN** the tool reports a deterministic missing-identity diagnostic and does not interpret that canonical annotation evidence

#### Scenario: Package SemVer outside the supported range fails closed

- **WHEN** an assembly contains a known canonical annotation FQN and its identity marker reports a malformed package version or a version outside the tool's advertised range for that generation
- **THEN** the tool reports a deterministic package-version compatibility diagnostic and does not interpret that canonical annotation evidence

#### Scenario: Assembly-only analysis can diagnose package compatibility

- **WHEN** a tool analyzes a target assembly without project or `PackageReference` metadata
- **THEN** it reads catalog generation and package SemVer from the assembly marker and applies the advertised compatibility rules

#### Scenario: A newer tool analyzes an older supported package

- **WHEN** a newer tool encounters an older catalog-generation and package-version pair still listed in its supported capabilities
- **THEN** it resolves that identity's stable annotations using the same canonical role and metadata semantics

#### Scenario: A tool predating annotation support is selected

- **WHEN** an adopter selects an ArchLinterNet tool older than v0.10.0 for a project using the package
- **THEN** the package compatibility guidance identifies that combination as unsupported and directs the adopter to an annotation-aware tool

### Requirement: Semantic annotations are evidence, not governance authority

Canonical annotations SHALL contribute only a primary semantic role or bounded context metadata to the existing classification model. They SHALL NOT grant dependency permission, create policy rules, select reviewed API membership, alter strict/audit mode, create ignores or baselines, generate YAML, or infer runtime behavior. `ApiContract` SHALL remain a primary semantic role; reviewed API membership SHALL remain an orthogonal policy selector decision under the public API surface contract.

#### Scenario: A canonical role does not change dependency policy

- **WHEN** a type gains a canonical role annotation
- **THEN** dependency permissions, layers, ignores, baselines, topology, and enforcement behavior change only if authored policy independently selects that semantic fact

#### Scenario: A ValueObject remains a ValueObject in the reviewed API

- **WHEN** a `ValueObject` is intentionally selected into a reviewed public API surface
- **THEN** its semantic role remains `ValueObject` and API membership is determined only by the authored #525 selector

#### Scenario: ApiContract does not imply reviewed membership

- **WHEN** a type has the primary role `ApiContract`
- **THEN** the annotation alone does not add the type to any reviewed public API snapshot

### Requirement: Layered and DDD role attributes follow the reviewed catalog

The source-only annotation set SHALL provide internal sealed role attributes for every `layered-clean` and `ddd` role in `architecture/semantic-annotations.v1.json`. Each attribute SHALL use the manifest FQN, `AllowMultiple=false`, and `Inherited=false`; it SHALL expose the six optional public string metadata properties defined by the role attribute contract. Its valid attribute targets SHALL match the manifest exactly.

#### Scenario: Layered and clean roles use approved targets

- **WHEN** a project compiles the layered/clean annotation source set
- **THEN** `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, and `SharedKernel` are assembly-only attributes, while `CompositionRoot` supports classes, structs, interfaces, and assemblies

#### Scenario: DDD roles use type targets

- **WHEN** a project compiles the DDD annotation source set
- **THEN** `Entity`, `AggregateRoot`, `ValueObject`, `DomainService`, `DomainEvent`, `Repository`, `Specification`, and `Factory` support classes, structs, and interfaces

#### Scenario: Role attribute definitions match the manifest contract

- **WHEN** the source definitions are inspected
- **THEN** each of the 17 roles has its exact manifest FQN, is internal and sealed, has a public parameterless constructor, and exposes the six optional public string metadata properties

### Requirement: The annotation source set is compatible with C# 9.0

The exact source set distributed by the package SHALL compile with C# 9.0 or newer and SHALL use only syntax available in C# 9.0. The repository SHALL include a `netstandard2.1` consumer configured with `LangVersion=9.0` that compiles the distributed source files. This source compatibility supports Unity source-import workflows and does not claim that every Unity project supports NuGet contentFiles installation.

#### Scenario: Source definitions compile with C# 9.0

- **WHEN** the repository builds the `netstandard2.1` consumer with `LangVersion=9.0`
- **THEN** all distributed annotation sources and representative assembly/type annotations compile without syntax changes

### Requirement: The role attribute base type relation follows the manifest

The manifest's `roleAttributeContract.baseType` SHALL name a CLR ancestor of every role attribute. Its `baseTypeRelationship` value SHALL declare `assignable-ancestor`, meaning the named type can be either the direct base class or an indirect ancestor. Validation SHALL resolve the declared base type from the manifest and mechanically check each role attribute's inheritance chain against it.

#### Scenario: Role attributes satisfy the manifest base type

- **WHEN** the repository validates each role attribute against the semantic annotation manifest
- **THEN** each type is a subclass of the manifest's declared `baseType`, whether that base type is immediate or indirect

### Requirement: Canonical identity and compatibility diagnostics are actionable and consistent

When canonical role annotation evidence is present, the annotation-aware tool SHALL require exactly one valid assembly catalog identity and SHALL resolve the evidence only when its catalog generation and package SemVer are supported. Missing, malformed, duplicated, or unsupported identity markers and unknown FQNs under the reserved namespace SHALL produce deterministic diagnostics that identify the subject, exact FQN, and compatibility or identity reason. Any policy mapping whose attribute FQN is in the reserved namespace SHALL produce a deterministic configuration diagnostic, even when it repeats the canonical role or metadata exactly, and SHALL NOT reinterpret that identity. Analysis diagnostics SHALL be represented consistently in human, JSON, SARIF, and Testing outputs.

#### Scenario: Canonical evidence without a valid catalog marker fails closed

- **WHEN** a target contains a known canonical role FQN but its assembly has no unique valid marker, an unsupported generation, or a malformed or unsupported package version
- **THEN** the tool reports the applicable actionable diagnostic and does not resolve that annotation as role evidence

#### Scenario: Unknown reserved FQN fails closed

- **WHEN** a target contains an attribute FQN under `ArchLinterNet.Annotations` that is not a supported canonical role identity
- **THEN** the tool reports the exact unsupported FQN and does not guess a role by simple name, namespace prefix, or inheritance

#### Scenario: YAML cannot name a reserved canonical identity

- **WHEN** policy YAML maps an attribute FQN in the reserved namespace, including an exact redundant mapping of a known canonical role FQN
- **THEN** policy validation reports the reserved-identity configuration error and does not reinterpret the annotation

#### Scenario: Analysis diagnostic projections agree

- **WHEN** one of these identity or compatibility conditions is encountered during analysis
- **THEN** human, JSON, SARIF, and Testing surfaces preserve the same subject, exact FQN, reason, and deterministic ordering

### Requirement: Tool capability and schema help expose the supported canonical role catalog

The machine-readable capability inventory SHALL advertise the canonical role FQNs recognized by the tool, each corresponding role and allowed scope, the reserved namespace, supported catalog generations, supported package SemVer ranges, and minimum annotation-aware tool version. Packaged policy-schema help SHALL explain that canonical roles are exact-FQN semantic evidence requiring a supported catalog identity and do not grant policy authority. Capability and schema catalog projections SHALL be validated against the versioned canonical manifest so drift is reported by the repository checks.

#### Scenario: Capability inventory describes the exact supported role identities

- **WHEN** tooling reads the capability inventory for an annotation-aware release
- **THEN** it can determine the exact canonical role FQNs and their type or assembly scope together with the supported catalog and package versions

#### Scenario: Schema help explains the canonical annotation boundary

- **WHEN** an adopter inspects the packaged policy schema or semantic-classification help
- **THEN** the metadata explains exact identity matching, version compatibility, and that annotations do not create policy rules or reviewed API membership

#### Scenario: Catalog projection drift is detected

- **WHEN** a supported role FQN, role name, scope, generation, or package range differs between a capability/schema projection and the canonical manifest
- **THEN** the focused repository validation fails and identifies the mismatched projection

### Requirement: CQRS/messaging and web/API role attributes follow the reviewed catalog

The source-only annotation set SHALL provide role attributes for every `cqrs-messages` and `web-api` first-class role in `architecture/semantic-annotations.v1.json`: `Command`, `CommandHandler`, `Query`, `QueryHandler`, `Event`, `EventHandler`, `IntegrationEvent`, `ReadModel`, `Controller`, `Endpoint`, `RequestDto`, `ResponseDto`, `ApiContract`, and `Validator`. Each attribute SHALL use its exact manifest FQN and targets, be internal and sealed, reject multiple uses and inheritance, have a public parameterless constructor, and expose only the six bounded string metadata properties defined by the role-attribute contract.

#### Scenario: The approved role family source set matches the manifest

- **WHEN** a consumer compiles the annotation source set
- **THEN** all 14 approved CQRS/messaging and web/API role identities are available on classes, structs, and interfaces with the exact manifest FQNs and no role identity is added for deferred or custom-mapping-only catalog entries

#### Scenario: Canonical role evidence uses the existing classifier

- **WHEN** a consumer type uses one approved role attribute and its assembly has a supported catalog identity
- **THEN** the existing semantic classifier resolves the exact FQN to the manifest role without a YAML mapping and exposes its bounded metadata through the existing role fact and index

#### Scenario: Custom mappings remain equivalent semantic evidence

- **WHEN** a user-owned attribute is explicitly mapped to the same role and metadata as an approved canonical annotation
- **THEN** both inputs produce the same effective role and metadata through the existing semantic model while retaining their respective evidence provenance

#### Scenario: CQRS and API facts compose through existing selectors

- **WHEN** an application marks a command and its handler in the same bounded context, a query and read model, an integration-event boundary, request and response DTOs, or a controller and endpoint
- **THEN** those distinct types resolve to their approved single roles and the existing selectors, contextual contracts, semantic coverage, and explain projections consume those facts without inferring runtime behavior

#### Scenario: ApiContract remains separate from reviewed API membership

- **WHEN** a type has the primary semantic role `ApiContract`, or a `ValueObject` is selected into a reviewed API surface by an authored #525 selector
- **THEN** the role annotation alone does not select reviewed API membership and the selected `ValueObject` retains its primary semantic role

### Requirement: Canonical context annotations enrich the existing primary role

The six manifest-defined context-only annotations SHALL contribute only their corresponding bounded metadata key and SHALL NOT assign or replace a primary role. A role's own effective metadata SHALL take precedence, followed by type-scope context values for missing keys, followed by assembly-scope context defaults for remaining missing keys. Equivalent values SHALL coalesce. Invalid or conflicting type-scope context evidence SHALL block assembly fallback for the affected key, while preserving winning role metadata. Conflicting values at the same context scope SHALL omit only the affected context key and produce a deterministic diagnostic while preserving an otherwise valid role and unrelated metadata.

#### Scenario: Role metadata and separate context metadata compose without replacing the role

- **WHEN** a type has one canonical role annotation, a context-only annotation, and its assembly has a context default
- **THEN** the existing role remains primary, role metadata wins for keys it supplies, type context fills missing keys, and assembly context fills only the remaining missing keys

#### Scenario: Type context overrides an assembly context default

- **WHEN** a type and its assembly provide different context values for the same key and the winning role evidence does not provide that key
- **THEN** the type-scope value appears in the effective role metadata

#### Scenario: Context metadata alone does not classify a type

- **WHEN** a type or assembly carries canonical context annotations but no classification source assigns a role
- **THEN** no role descriptor is produced and no role is inferred from the context value

#### Scenario: Feature context does not become the Feature role

- **WHEN** a type carries `ArchLinterNet.Annotations.FeatureAttribute`
- **THEN** it contributes only the `feature` metadata key and does not receive the custom-mapping-only `Feature` role

### Requirement: Ports, infrastructure, UI, and Unity role attributes follow the reviewed catalog

The source-only annotation set SHALL provide internal sealed role attributes for every `ports-infrastructure`, `ui`, and `unity-client` role in `architecture/semantic-annotations.v1.json`: `Port`, `Adapter`, `AntiCorruptionLayer`, `DbContext`, `RepositoryImplementation`, `ExternalClient`, `MessageBusAdapter`, `FileSystemAdapter`, `ClockAdapter`, `View`, `ViewModel`, `Presenter`, `UnityRuntime`, `UnityEditor`, `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, and `SceneAdapter`. Each attribute SHALL use its exact manifest FQN and targets, reject multiple uses and inheritance, expose a public parameterless constructor, and inherit only the six bounded string metadata properties defined by the role-attribute contract. `UnityRuntime` and `UnityEditor` SHALL be assembly-targeted only; all other roles in these families SHALL be type-targeted only. The definitions SHALL not reference or infer behavior from infrastructure, UI, or Unity framework types.

#### Scenario: Ports/infrastructure and UI role definitions match the manifest

- **WHEN** the annotation source contract is checked against the manifest
- **THEN** all nine `ports-infrastructure` roles and all three `ui` roles are defined under their exact FQNs with the manifest targets and the reviewed role-attribute contract

#### Scenario: Unity roles retain static scope boundaries

- **WHEN** the annotation source contract is checked against the manifest
- **THEN** `UnityRuntime` and `UnityEditor` target assemblies only, while `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, and `SceneAdapter` target types only, with no Unity runtime dependency

#### Scenario: The approved source set compiles in the C# 9 consumer

- **WHEN** an SDK-style consumer compiles the annotation source set using the repository's C# 9 smoke project
- **THEN** representative ports/infrastructure, UI, and Unity/client annotations compile without framework references or source changes

### Requirement: Packed annotation package is source-only and externally consumable

The `ArchLinterNet.Annotations` package SHALL include the approved annotation C# source set under `contentFiles/cs/any` with NuGet `buildAction=Compile` and `copyToOutput=false`. It SHALL include one catalog identity source whose generation and exact SemVer match the package being packed. The `.nupkg` SHALL NOT contain `lib/`, `ref/`, `runtimes/`, `native/`, `build/`, `buildTransitive/`, analyzer, source-generator, or executable tool assets, and SHALL have no ArchLinterNet or other runtime package dependency. The source files SHALL be compile-injected by a direct `PackageReference` into each opting-in SDK-style project.

#### Scenario: A packed source package has only compile content and package metadata

- **WHEN** the repository packs `ArchLinterNet.Annotations`
- **THEN** the package contains each approved source file exactly once as `contentFiles/cs/any` content, declares those files for compilation without copying them to output, and contains no runtime, binary, analyzer, generator, build-target, or tool asset

#### Scenario: Package and assembly catalog identities agree

- **WHEN** an SDK-style consumer compiles source from a packed annotation package
- **THEN** its assembly carries exactly one internal catalog identity with generation 1 and the exact SemVer of the package that supplied the source

#### Scenario: A public library opts into canonical annotations

- **WHEN** an external public class library directly references the packed package and uses canonical role and context attributes
- **THEN** it builds through ordinary restore/build, Core recognizes the manifest FQNs, its exported public API contains no package-provided annotation types, and its runtime dependency closure contains no annotation assembly

#### Scenario: Central package management and multiple projects opt in

- **WHEN** two SDK-style projects directly reference the package with a version centralized in `Directory.Packages.props`
- **THEN** both projects compile their own internal source definitions, remain discoverable by exact metadata name, and require neither shared CLR type identity nor a package runtime dependency

#### Scenario: Removing the package leaves no generated project artifacts

- **WHEN** a consumer removes its direct package reference, removes annotation usages, restores, cleans, and rebuilds
- **THEN** its project tree contains no package-generated source files and its build/runtime outputs contain no ArchLinterNet annotation assembly or lingering package artifacts

#### Scenario: Release inventory adds annotations only to the compatible stable line

- **WHEN** a release candidate has a stable version in `[0.10.0,0.11.0)`
- **THEN** its immutable manifest uses schema v3 with exactly the reviewed five-package inventory, records the full candidate version on every package record, and includes one annotation `.nupkg` without requiring a symbol package; NuGet-normalized filenames omit SemVer build metadata while the manifest retains it, and previews and versions outside the reviewed range retain their reviewed package set

#### Scenario: Legacy v2 inventory cannot omit annotations from stable 0.10.x

- **WHEN** a v2 candidate manifest declares a stable version in `[0.10.0,0.11.0)` with only the legacy four packages
- **THEN** manifest validation rejects it as incompatible with the required stable-line package inventory

#### Scenario: Unity source import uses the same package source set

- **WHEN** a Unity-compatible project cannot consume NuGet `contentFiles`
- **THEN** the package documentation identifies a deterministic source-import path that uses the same reviewed C# files and does not define a second annotation vocabulary
