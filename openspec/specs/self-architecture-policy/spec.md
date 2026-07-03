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
The repository's architecture contract SHALL declare a `scope: rule_input` coverage contract covering the seam, leaf-isolation, and protected-surface rule IDs for the recovered Core architecture, so a rule referencing a renamed or deleted layer is caught as `unresolved`/`empty-input` rather than silently passing.

#### Scenario: A rule-input coverage contract runs alongside the other self-policy contracts
- **WHEN** `make lint-architecture` runs the repository's own policy in strict mode
- **THEN** the rule-input coverage contract's summary reports every referenced contract ID as `covered`, with `stale`/`unresolved` count at `0`

### Requirement: Static production service and god-object guardrails are documentation-governed
Because ArchLinterNet's supported contract families (documented in `docs/policy-format/supported-capabilities.md`) do not include static-class-declaration detection or type/member-count size checks, the repository SHALL enforce its static-production-service allowlist and god-object-growth prevention through `docs/internal/static-class-inventory.md` (reviewed classification of every production `static class` under `src/`) rather than through a new architecture-policy YAML contract. The namespace/type-placement mechanism available today — `strict_protected` contracts restricting internals to `ArchLinterNet.Core` — SHALL be used wherever a namespace boundary already exists (see the discovery/resolution protected-surface requirement above) as the structural half of god-object prevention.

#### Scenario: A new static production service is proposed
- **WHEN** a contributor or reviewer adds a new `static class` under `src/` that owns behavior, state, or collaborators (not a pure helper, extension-method container, constants holder, or documented compatibility facade)
- **THEN** `docs/internal/static-class-inventory.md` is updated to classify it, and it is either converted to a DI-registered instance service or documented as a reviewed exception with a rationale

### Requirement: New contract-family implementations require self-policy coverage or a documented exception
Any new ArchLinterNet contract family added to the engine (a new entry alongside dependency, layer, allow-only, cycle, independence, protected-surface, external-dependency, method-body, asmdef, or coverage contracts) SHALL ship with either a corresponding rule in `architecture/dependencies.arch.yml` exercising it against this repository, or a documented reason in the change's proposal for why no self-policy rule applies.

#### Scenario: A new contract family is implemented
- **WHEN** a change adds a new contract family to `ArchLinterNet.Core.Contracts`/`ArchLinterNet.Core.Execution`
- **THEN** the change's proposal or design document states which `architecture/dependencies.arch.yml` rule exercises the new family, or explicitly documents why the repository's own policy does not need one

