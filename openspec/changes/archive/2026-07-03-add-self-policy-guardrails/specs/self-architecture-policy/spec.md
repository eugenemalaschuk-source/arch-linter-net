## ADDED Requirements

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
