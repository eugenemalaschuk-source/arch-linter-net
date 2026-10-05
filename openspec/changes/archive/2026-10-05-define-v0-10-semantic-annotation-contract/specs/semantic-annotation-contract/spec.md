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

The reserved namespace SHALL be `ArchLinterNet.Annotations`. A role named `<Role>` SHALL use the exact fully qualified type name `ArchLinterNet.Annotations.<Role>Attribute`; role attributes SHALL be internal sealed types with a parameterless constructor and optional named string properties `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, and `Runtime`. Context-only attributes SHALL use `ArchLinterNet.Annotations.DomainAttribute`, `BoundedContextAttribute`, `ModuleAttribute`, `FeatureAttribute`, `PlatformAttribute`, or `RuntimeAttribute` and SHALL accept one required string constructor argument. Every opting-in assembly SHALL carry the internal assembly marker `ArchLinterNet.Annotations.AnnotationCatalogGenerationAttribute(1)`. Recognition SHALL match exact metadata fully qualified names and SHALL NOT guess by simple name, namespace prefix, inheritance, or defining assembly identity. The versioned canonical catalog manifest SHALL be the reviewed source of truth for injected identities, roles, targets, metadata keys, and generation. Package source, Core mappings, capability metadata, and documentation SHALL be generated from or mechanically checked against that manifest, with drift failing validation.

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

The first package release SHALL use catalog generation `1` and the v0.10.0 product version. Each consumer assembly compiled with the package SHALL carry the catalog generation marker. Tool capability metadata SHALL advertise the generations and package versions it recognizes, and package metadata SHALL identify the minimum annotation-aware tool release. A supported annotation-aware tool SHALL fail closed with a deterministic diagnostic when a consumer advertises an unsupported newer generation or uses an unknown canonical FQN. Newer tools SHALL accept older generations only while those generations remain in the tool's advertised support set. The automatic skew-diagnostic guarantee begins with v0.10.0 annotation-aware tools; package guidance SHALL identify v0.10.0 as the minimum supported tool version.

#### Scenario: Supported v0.10 package and tool agree

- **WHEN** a v0.10.0-or-newer annotation-aware tool analyzes a consumer compiled with package `0.10.x` and catalog generation `1`
- **THEN** the tool recognizes the generation and resolves the packaged canonical identities it advertises

#### Scenario: An older annotation-aware tool sees a newer generation

- **WHEN** an annotation-aware tool encounters a consumer assembly whose catalog generation is greater than the tool supports
- **THEN** it reports an unsupported-generation diagnostic and does not silently classify unknown evidence

#### Scenario: A newer tool analyzes an older supported package

- **WHEN** a newer tool encounters an older package generation still listed in its supported capabilities
- **THEN** it resolves that generation's stable identities using the same canonical role and metadata semantics

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
