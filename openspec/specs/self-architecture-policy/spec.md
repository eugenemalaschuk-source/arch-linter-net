# self-architecture-policy Specification

## Purpose
Define and enforce ArchLinterNet's own internal validation-pipeline boundaries (application seam, contract execution, diagnostics, resolution, scanning) through the repository's architecture policy and acceptance gate, so the post-#69 split does not regress into central orchestration coupling.
## Requirements
### Requirement: Repository governs its own internal validation-pipeline boundaries
The repository's architecture contract (`architecture/dependencies.arch.yml`) SHALL declare namespace layers for `ArchLinterNet.Core.Model`, `ArchLinterNet.Core.Reporting`, `ArchLinterNet.Core.Resolution`, `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Execution`, and `ArchLinterNet.Core.Validation`, in addition to the existing package-level layers (`core`, `core_scanning`, `cli`, `testing`, `unity`).

#### Scenario: Internal layers are declared
- **WHEN** the policy file is loaded
- **THEN** it defines a layer for each of `Core.Model`, `Core.Reporting`, `Core.Resolution`, `Core.Contracts`, `Core.Execution`, and `Core.Validation`

### Requirement: CLI must use the application seam
`ArchLinterNet.Cli` SHALL NOT depend directly on `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Resolution`, or `ArchLinterNet.Core.Scanning`. It SHALL route validation and baseline-generation behavior through `ArchLinterNet.Core.Validation`.

#### Scenario: CLI depends only on the seam and shared leaves
- **WHEN** `ArchLinterNet.Cli` source is scanned for namespace references
- **THEN** it references only `ArchLinterNet.Core.Model`, `ArchLinterNet.Core.Reporting`, and `ArchLinterNet.Core.Validation` from Core

### Requirement: Contract execution does not depend on hosts
`ArchLinterNet.Core.Execution` (including contract handlers) SHALL NOT depend on `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity`.

#### Scenario: Execution stays host-agnostic
- **WHEN** `ArchLinterNet.Core.Execution` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity`

### Requirement: Diagnostics and model layers stay leaves
`ArchLinterNet.Core.Reporting` SHALL NOT depend on `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, `ArchLinterNet.Core.Resolution`, `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Contracts`, or any host package. `ArchLinterNet.Core.Model` SHALL NOT depend on any other internal layer or host package.

#### Scenario: Reporting stays a diagnostics leaf
- **WHEN** `ArchLinterNet.Core.Reporting` source is scanned for namespace references
- **THEN** it references only `ArchLinterNet.Core.Model`

#### Scenario: Model stays independent
- **WHEN** `ArchLinterNet.Core.Model` source is scanned for namespace references
- **THEN** it references no other `ArchLinterNet.*` namespace

### Requirement: Resolution and scanning do not depend upward
`ArchLinterNet.Core.Resolution` and `ArchLinterNet.Core.Scanning` SHALL NOT depend on `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, or any host package.

#### Scenario: Resolution and scanning stay below execution and validation
- **WHEN** `ArchLinterNet.Core.Resolution` or `ArchLinterNet.Core.Scanning` source is scanned for namespace references
- **THEN** neither references `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity`

### Requirement: The self-policy actually runs against the real repository
The normal lint gate (`make lint-architecture`, part of `make lint` and `make acceptance`) SHALL execute the real `architecture/dependencies.arch.yml` policy in strict mode against the repository's own built assemblies (`Core`, `Cli`, `Testing`, `Unity`) and fail the build if it does not pass.

#### Scenario: Self-validation runs in the lint gate
- **WHEN** `make lint-architecture` runs
- **THEN** it builds `ArchLinterNet.Cli` and `ArchLinterNet.Unity` if not already built, then runs a test that validates `architecture/dependencies.arch.yml` against the repository root in strict mode
- **AND** the test fails if the repository violates its own declared boundaries

### Requirement: Application seam does not bypass into scanning, discovery, or resolution internals
`ArchLinterNet.Core.Validation` SHALL NOT depend directly on `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Discovery`, or `ArchLinterNet.Core.Resolution`. It SHALL reach their behavior only through `ArchLinterNet.Core.Execution` (or, for the Unity host, `ArchLinterNet.Core.Asmdef`).

#### Scenario: Validation stays behind the execution seam
- **WHEN** `ArchLinterNet.Core.Validation` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Discovery`, or `ArchLinterNet.Core.Resolution`

### Requirement: Discovery does not depend upward on execution or validation
`ArchLinterNet.Core.Discovery` SHALL NOT depend on `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, or any host package (`ArchLinterNet.Cli`, `ArchLinterNet.Testing`, `ArchLinterNet.Unity`), matching the existing constraint on `ArchLinterNet.Core.Resolution` and `ArchLinterNet.Core.Scanning`.

#### Scenario: Discovery stays below execution and validation
- **WHEN** `ArchLinterNet.Core.Discovery` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity`

### Requirement: Discovery and resolution internals are protected from adapters
`ArchLinterNet.Core.Discovery` and `ArchLinterNet.Core.Resolution` SHALL only be referenced from within `ArchLinterNet.Core`, matching the existing protected-surface constraint on `ArchLinterNet.Core.Scanning`.

#### Scenario: No adapter imports discovery or resolution directly
- **WHEN** `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity` source is scanned for namespace references
- **THEN** none of them reference `ArchLinterNet.Core.Discovery` or `ArchLinterNet.Core.Resolution`

### Requirement: Seam and isolation rules stay matched against real code
The repository's architecture contract SHALL declare a `scope: rule_input` coverage contract covering the seam, leaf-isolation, protected-surface, and Contracts host/execution-isolation rule IDs for the recovered Core architecture, so a rule referencing a renamed or deleted layer is caught as `unresolved`/`empty-input` rather than silently passing.

#### Scenario: A rule-input coverage contract runs alongside the other self-policy contracts
- **WHEN** `make lint-architecture` runs the repository's own policy in strict mode
- **THEN** the rule-input coverage contract's summary reports every referenced contract ID as `covered`, with `stale`/`unresolved` count at `0`, including the `core-contracts-must-not-depend-on-hosts` and `core-contracts-must-not-depend-on-execution` rule IDs

### Requirement: Static production service and god-object guardrails are documentation-governed
Because ArchLinterNet's supported contract families (documented in `docs/policy-format/supported-capabilities.md`) do not include static-class-declaration detection or type/member-count size checks, the repository SHALL enforce its static-production-service allowlist and god-object-growth prevention through `docs/internal/static-class-inventory.md` (reviewed classification of every production `static class` under `src/`) rather than through a new architecture-policy YAML contract. The namespace/type-placement mechanism available today — `strict_protected` contracts restricting internals to `ArchLinterNet.Core` — SHALL be used wherever a namespace boundary already exists (see the discovery/resolution protected-surface requirement above) as the structural half of god-object prevention.

#### Scenario: A new static production service is proposed
- **WHEN** a contributor or reviewer adds a new `static class` under `src/` that owns behavior, state, or collaborators (not a pure helper, extension-method container, constants holder, or documented compatibility facade)
- **THEN** `docs/internal/static-class-inventory.md` is updated to classify it, and it is either converted to a DI-registered instance service or documented as a reviewed exception with a rationale

### Requirement: New contract-family implementations require self-policy coverage or a documented exception
Any new ArchLinterNet contract family added to the engine (a new entry alongside dependency, layer, allow-only, cycle, independence, protected-surface, external-dependency, method-body, asmdef, or coverage contracts) SHALL ship with either a corresponding rule in `architecture/dependencies.arch.yml` exercising it against this repository, or a documented reason in the change's proposal for why no self-policy rule applies. New family code SHALL live in the extension namespaces the #208-#216 refactor chain established: family checkers in `ArchLinterNet.Core.Execution.Checkers`, configuration contributors in `ArchLinterNet.Core.Execution.Abstractions`, diagnostic payloads in `ArchLinterNet.Core.Model`, and the YAML contract-group model in `ArchLinterNet.Core.Contracts.Families`, rather than as new branches in the central catalog, session, mapper, or DTO files those namespaces replaced.

#### Scenario: A new contract family is implemented
- **WHEN** a change adds a new contract family to `ArchLinterNet.Core.Contracts`/`ArchLinterNet.Core.Execution`
- **THEN** the change's proposal or design document states which `architecture/dependencies.arch.yml` rule exercises the new family, or explicitly documents why the repository's own policy does not need one
- **AND** the family's checker, configuration contributor (if any), diagnostic payload, and YAML model live in `Execution.Checkers`, `Execution.Abstractions`, `Model`, and `Contracts.Families` respectively

### Requirement: Contracts stays host-agnostic and independent of execution internals
`ArchLinterNet.Core.Contracts` (including `Contracts.Families` and `Contracts.Validators`) SHALL NOT depend on `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, `ArchLinterNet.Unity`, or `ArchLinterNet.Core.Execution`. The contract-family metadata `Contracts` owns (`ArchitectureContractFamilyBinding`/`ArchitectureContractFamilyBindings`) SHALL remain a self-contained registry rather than depending on `Execution`'s runtime checker/registry (`ArchitectureContractFamilyDescriptor`/`ArchitectureContractFamilyRegistry`).

#### Scenario: Contracts stays free of host references
- **WHEN** `ArchLinterNet.Core.Contracts` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity`

#### Scenario: Contracts stays independent of Execution
- **WHEN** `ArchLinterNet.Core.Contracts` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Core.Execution`

### Requirement: Central catalog and dispatch points grow through their extension mechanism, not inline branches
Because ArchLinterNet has no contract family that inspects a file's internal structure, branch count, or dispatch shape (confirmed unsupported in `docs/policy-format/supported-capabilities.md`), the repository SHALL enforce the following extension-hotspot invariants introduced by the #208-#216 refactor chain through documented guardrail candidates in `docs/internal/core-architecture-blueprint.md`, reviewed at code-review time, rather than through a new architecture-policy YAML contract:

- `ArchLinterNet.Core.Execution.ArchitectureContractFamilyRegistry` and `ArchLinterNet.Core.Contracts.ArchitectureContractFamilyBindings` SHALL grow by appending a new `ArchitectureContractFamilyDescriptor`/`ArchitectureContractFamilyBinding` entry, not by adding new per-family conditional branches inline.
- `ArchLinterNet.Core.Execution.ArchitectureAnalysisSession` SHALL NOT regain inline per-family checking or configuration-inspection logic; new family checks belong in `ArchLinterNet.Core.Execution.Checkers`, and new configuration inspection belongs in an `ArchitectureConfigurationContributor` under `ArchLinterNet.Core.Execution.Abstractions`.
- `ArchLinterNet.Core.Reporting.ArchitectureDiagnosticMapper.FromViolation` SHALL NOT regrow an if/switch dispatch chain; new diagnostic families SHALL supply an `IArchitectureDiagnosticPayload` implementation under `ArchLinterNet.Core.Model` instead.
- `ArchLinterNet.Core.Contracts.ArchitectureContractModels` (including the `ArchitectureContractGroups` partial) SHALL NOT regrow inline `[YamlMember]` clusters for new contract groups; new families get their own file under `ArchLinterNet.Core.Contracts.Families`.

#### Scenario: A new contract family is added to the engine
- **WHEN** a contributor or reviewer adds a new contract family
- **THEN** the family's descriptor/binding is appended to `ArchitectureContractFamilyRegistry`/`ArchitectureContractFamilyBindings` rather than an inline branch being added to those files, and its checker, configuration inspection, diagnostic payload, and YAML model each live in `Execution.Checkers`, `Execution.Abstractions`, `Model`, and `Contracts.Families` respectively
- **AND** `docs/internal/core-architecture-blueprint.md`'s "Guardrail candidate for #215" paragraph is consulted during review to confirm none of the four regression patterns were reintroduced

