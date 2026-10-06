## Purpose

Defines the optional v0.10 source-only annotation package, the stable identities and compatibility contract that consumers and ArchLinterNet tooling rely on, and the boundaries that keep annotations separate from policy authority and reviewed API membership.

## ADDED Requirements

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
