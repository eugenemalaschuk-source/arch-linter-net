## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Seam and isolation rules stay matched against real code
The repository's architecture contract SHALL declare a `scope: rule_input` coverage contract covering the seam, leaf-isolation, protected-surface, and Contracts host/execution-isolation rule IDs for the recovered Core architecture, so a rule referencing a renamed or deleted layer is caught as `unresolved`/`empty-input` rather than silently passing.

#### Scenario: A rule-input coverage contract runs alongside the other self-policy contracts
- **WHEN** `make lint-architecture` runs the repository's own policy in strict mode
- **THEN** the rule-input coverage contract's summary reports every referenced contract ID as `covered`, with `stale`/`unresolved` count at `0`, including the `core-contracts-must-not-depend-on-hosts` and `core-contracts-must-not-depend-on-execution` rule IDs

### Requirement: New contract-family implementations require self-policy coverage or a documented exception
Any new ArchLinterNet contract family added to the engine (a new entry alongside dependency, layer, allow-only, cycle, independence, protected-surface, external-dependency, method-body, asmdef, or coverage contracts) SHALL ship with either a corresponding rule in `architecture/dependencies.arch.yml` exercising it against this repository, or a documented reason in the change's proposal for why no self-policy rule applies. New family code SHALL live in the extension namespaces the #208-#216 refactor chain established: family checkers in `ArchLinterNet.Core.Execution.Checkers`, configuration contributors in `ArchLinterNet.Core.Execution.Abstractions`, diagnostic payloads in `ArchLinterNet.Core.Model`, and the YAML contract-group model in `ArchLinterNet.Core.Contracts.Families`, rather than as new branches in the central catalog, session, mapper, or DTO files those namespaces replaced.

#### Scenario: A new contract family is implemented
- **WHEN** a change adds a new contract family to `ArchLinterNet.Core.Contracts`/`ArchLinterNet.Core.Execution`
- **THEN** the change's proposal or design document states which `architecture/dependencies.arch.yml` rule exercises the new family, or explicitly documents why the repository's own policy does not need one
- **AND** the family's checker, configuration contributor (if any), diagnostic payload, and YAML model live in `Execution.Checkers`, `Execution.Abstractions`, `Model`, and `Contracts.Families` respectively
