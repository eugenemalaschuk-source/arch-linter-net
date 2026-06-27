## ADDED Requirements

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
