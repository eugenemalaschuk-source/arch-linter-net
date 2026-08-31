# self-architecture-policy Specification

## Purpose
Define and enforce ArchLinterNet's own internal validation-pipeline boundaries (application seam, contract execution, diagnostics, resolution, scanning) through the repository's architecture policy and acceptance gate, so the post-#69 split does not regress into central orchestration coupling.

## Requirements

### Requirement: Repository governs its own internal validation-pipeline boundaries
The repository's architecture contract (`architecture/dependencies.arch.yml`) SHALL declare namespace layers for `ArchLinterNet.Core.Model`, `ArchLinterNet.Core.Reporting`, `ArchLinterNet.Core.Resolution`, `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.PolicyContext`, `ArchLinterNet.Core.Validation`, and `ArchLinterNet.Core.Asmdef`, in addition to the package-level layers (`core`, `core_scanning`, `cli`, `testing`).

#### Scenario: Internal layers are declared
- **WHEN** the policy file is loaded
- **THEN** it defines a layer for each of `Core.Model`, `Core.Reporting`, `Core.Resolution`, `Core.Contracts`, `Core.Execution`, `Core.PolicyContext`, `Core.Validation`, and `Core.Asmdef`

### Requirement: CLI must use the application seam
`ArchLinterNet.Cli` SHALL NOT depend directly on `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Resolution`, or `ArchLinterNet.Core.Scanning`. It SHALL route validation and baseline-generation behavior through `ArchLinterNet.Core.Validation` and effective-policy context exports through `ArchLinterNet.Core.PolicyContext`.

#### Scenario: CLI depends only on the seam and shared leaves
- **WHEN** `ArchLinterNet.Cli` source is scanned for namespace references
- **THEN** it references only `ArchLinterNet.Core.Model`, `ArchLinterNet.Core.Reporting`, `ArchLinterNet.Core.Validation`, and `ArchLinterNet.Core.PolicyContext` from Core

### Requirement: Policy context is an application seam above execution
`ArchLinterNet.Core.PolicyContext` SHALL project effective policy facts through the Contracts and Execution seams. It SHALL NOT depend on host adapters or directly on scanning, discovery, resolution, or IO implementation seams.

#### Scenario: Policy context stays within its declared seam
- **WHEN** `ArchLinterNet.Core.PolicyContext` source is scanned for namespace references
- **THEN** it may depend on `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Execution`, and `ArchLinterNet.Core.Model`
- **AND** it does not reference `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Discovery`, `ArchLinterNet.Core.Resolution`, or `ArchLinterNet.Core.IO`

### Requirement: Contract execution does not depend on hosts
`ArchLinterNet.Core.Execution` (including contract handlers) SHALL NOT depend on `ArchLinterNet.Cli` or `ArchLinterNet.Testing`.

#### Scenario: Execution stays host-agnostic
- **WHEN** `ArchLinterNet.Core.Execution` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Cli` or `ArchLinterNet.Testing`

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
- **THEN** neither references `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing`

### Requirement: The self-policy actually runs against the real repository
`make lint-architecture` SHALL be the single authoritative, read-only definition of "the repository
satisfies its own architecture policy". It SHALL execute the real `architecture/dependencies.arch.yml`
in strict mode against the repository's own project graph through the CLI validation path with
`--ensure-built`, which prepares and verifies that graph and SHALL NOT write to
`architecture/`. It SHALL fail the build if the repository violates its own declared boundaries, and
it SHALL remain part of `make lint` and `make acceptance`.

`SelfArchitecturePolicyTests` SHALL continue to validate the same policy through the
`ArchLinterNet.Testing` adapter as parity evidence inside `make test`, and SHALL NOT be a second,
independently maintained definition of success.

#### Scenario: Self-validation runs in the lint gate
- **WHEN** `make lint-architecture` runs
- **THEN** it runs `architecture/dependencies.arch.yml` in strict mode with `--ensure-built` through the CLI
- **AND** the command fails if the repository violates its own declared boundaries
- **AND** no file under `architecture/` is modified by the run

#### Scenario: The Testing adapter agrees with the canonical gate
- **WHEN** `make test` runs `SelfArchitecturePolicyTests`
- **THEN** the same policy validated through `ArchitectureAssertions` in strict mode passes

### Requirement: Application seam does not bypass into scanning, discovery, or resolution internals
`ArchLinterNet.Core.Validation` SHALL NOT depend directly on `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Discovery`, or `ArchLinterNet.Core.Resolution`. It SHALL reach their behavior through `ArchLinterNet.Core.Execution`; the dedicated asmdef-only path SHALL remain in `ArchLinterNet.Core.Asmdef`.

#### Scenario: Validation stays behind the execution seam
- **WHEN** `ArchLinterNet.Core.Validation` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Discovery`, or `ArchLinterNet.Core.Resolution`

### Requirement: Discovery does not depend upward on execution or validation
`ArchLinterNet.Core.Discovery` SHALL NOT depend on `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, or any host package (`ArchLinterNet.Cli`, `ArchLinterNet.Testing`), matching the existing constraint on `ArchLinterNet.Core.Resolution` and `ArchLinterNet.Core.Scanning`.

#### Scenario: Discovery stays below execution and validation
- **WHEN** `ArchLinterNet.Core.Discovery` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Validation`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing`

### Requirement: Discovery and resolution internals are protected from adapters
`ArchLinterNet.Core.Discovery` and `ArchLinterNet.Core.Resolution` SHALL only be referenced from within `ArchLinterNet.Core`, matching the existing protected-surface constraint on `ArchLinterNet.Core.Scanning`.

#### Scenario: No adapter imports discovery or resolution directly
- **WHEN** `ArchLinterNet.Cli` or `ArchLinterNet.Testing` source is scanned for namespace references
- **THEN** neither references `ArchLinterNet.Core.Discovery` or `ArchLinterNet.Core.Resolution`

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
`ArchLinterNet.Core.Contracts` (including `Contracts.Families` and `Contracts.Validators`) SHALL NOT depend on `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Core.Execution`. The contract-family metadata `Contracts` owns (`ArchitectureContractFamilyBinding`/`ArchitectureContractFamilyBindings`) SHALL remain a self-contained registry rather than depending on `Execution`'s runtime checker/registry (`ArchitectureContractFamilyDescriptor`/`ArchitectureContractFamilyRegistry`).

#### Scenario: Contracts stays free of host references
- **WHEN** `ArchLinterNet.Core.Contracts` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Cli` or `ArchLinterNet.Testing`

#### Scenario: Contracts stays independent of Execution
- **WHEN** `ArchLinterNet.Core.Contracts` source is scanned for namespace references
- **THEN** it does not reference `ArchLinterNet.Core.Execution`

### Requirement: Unity asmdef validation is a Core capability
The repository SHALL keep `.asmdef` validation in `ArchLinterNet.Core.Asmdef` and SHALL NOT maintain a separate `ArchLinterNet.Unity` production or test assembly solely for the asmdef convenience facade.

#### Scenario: Repository package and assembly inventory is evaluated
- **WHEN** the solution, self-policy, release workflow, and package documentation are inspected
- **THEN** they contain `ArchLinterNet.Core`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, and `ArchLinterNet.CEL` production packages
- **AND** asmdef facade tests run from `ArchLinterNet.Core.Tests`

### Requirement: Central catalog and dispatch points grow through their extension mechanism, not inline branches
Because ArchLinterNet has no contract family that inspects a file's internal structure, branch count, or dispatch shape (confirmed unsupported in `docs/policy-format/supported-capabilities.md`), the repository SHALL enforce the following extension-hotspot invariants introduced by the #208-#216 refactor chain through documented guardrail candidates in `docs/internal/core-architecture-blueprint.md`, reviewed at code-review time, rather than through a new architecture-policy YAML contract:

- `ArchLinterNet.Core.Execution.ArchitectureContractFamilyRegistry` and `ArchLinterNet.Core.Contracts.ArchitectureContractFamilyBindings` SHALL grow by appending a new `ArchitectureContractFamilyDescriptor`/`ArchitectureContractFamilyBinding` entry, not by adding new per-family conditional branches inline.
- `ArchLinterNet.Core.Execution.ArchitectureAnalysisSession` SHALL NOT regain inline per-family checking or configuration-inspection logic; new family checks belong in `ArchLinterNet.Core.Execution.Checkers`, and new configuration inspection belongs in an `ArchitectureConfigurationContributor` under `ArchLinterNet.Core.Execution.Abstractions`.
- `ArchLinterNet.Core.Reporting.ArchitectureDiagnosticMapper.FromViolation` SHALL NOT regrow an if/switch dispatch chain; new diagnostic families SHALL supply an `IArchitectureDiagnosticPayload` implementation under `ArchLinterNet.Core.Model` instead.
- `ArchLinterNet.Core.Contracts.ArchitectureContractModels` (including the `ArchitectureContractGroups` partial) SHALL NOT regrow inline `[YamlMember]` clusters for new contract groups; new families get their own file under `ArchLinterNet.Core.Contracts.Families`.
- New checkers (`ArchLinterNet.Core.Execution.Checkers`), validators (`ArchLinterNet.Core.Contracts.Validators`), and configuration contributors (`ArchLinterNet.Core.Execution.Abstractions`) SHALL depend only on `Contracts`/`Model` abstractions and the per-run session/context they are handed, not on a CLI/reporting *adapter* (a formatter, a console/JSON writer, or any `ArchLinterNet.Cli` type), to produce or shape output. This cannot be a blanket `core_execution`/`core_contracts`-must-not-depend-on-`core_reporting` dependency rule: `core_execution` already legitimately depends on `core_reporting` for seam-signature data (`IArchitectureRunnerSetupService.BuildRunner` takes a `ValidationTiming` parameter), so such a rule would break that existing, valid dependency; the guardrail distinguishes data-shape references from adapter-behavior references and is therefore code-review-governed like the other four.

#### Scenario: A new contract family is added to the engine
- **WHEN** a contributor or reviewer adds a new contract family
- **THEN** the family's descriptor/binding is appended to `ArchitectureContractFamilyRegistry`/`ArchitectureContractFamilyBindings` rather than an inline branch being added to those files, and its checker, configuration inspection, diagnostic payload, and YAML model each live in `Execution.Checkers`, `Execution.Abstractions`, `Model`, and `Contracts.Families` respectively
- **AND** `docs/internal/core-architecture-blueprint.md`'s guardrail paragraph is consulted during review to confirm none of the five regression patterns were reintroduced, including a checker/validator/contributor reaching into a CLI/reporting adapter instead of depending only on abstractions/context

### Requirement: Project discovery and project coverage are authoritative
The repository's architecture contract SHALL declare `analysis.solution: ArchLinterNet.slnx` as the
project-discovery source of truth, SHALL exclude `tests/**` and `benchmarks/**` through
`analysis.project_exclude`, and SHALL declare a `scope: project` coverage contract. Test, benchmark,
and sample projects SHALL be deliberately excluded rather than accidentally absent, and a newly added
first-party production project SHALL NOT silently escape governance.

#### Scenario: Every discovered production project is covered
- **WHEN** the strict gate runs
- **THEN** the `scope: project` coverage summary reports every discovered project as covered, with `uncovered` and `unknown` at `0`

#### Scenario: A discovered project that no layer covers fails the gate
- **WHEN** a project enters the discovered inventory whose assembly maps to no declared layer and matches no exclusion
- **THEN** project coverage reports it and strict validation fails

### Requirement: The shipped assembly graph is governed directly
The repository's architecture contract SHALL govern the real compiled assembly-reference graph in
addition to namespace direction: `ArchLinterNet.CEL` SHALL directly reference no other shipped
assembly, `ArchLinterNet.Core` SHALL directly reference only `ArchLinterNet.CEL` among shipped
assemblies, `ArchLinterNet.Cli` and `ArchLinterNet.Testing` SHALL directly reference only
`ArchLinterNet.Core`, and `ArchLinterNet.Cli` and `ArchLinterNet.Testing` SHALL be mutually
independent. These contracts evaluate direct references only; they SHALL NOT claim transitive
reference-path proof.

#### Scenario: An unlisted direct first-party assembly reference fails the gate
- **WHEN** an adapter assembly directly references a shipped assembly outside its allow-only list
- **THEN** strict validation reports the source assembly and the disallowed reference

### Requirement: Shipped library compatibility surfaces are reviewed snapshots
The repository SHALL govern the exported surface of `ArchLinterNet.Core`, `ArchLinterNet.Testing`,
and `ArchLinterNet.CEL` through `strict_public_api_surface` contracts using repository-local
`api_snapshot` files under `architecture/api/` with `api_comparison: exact`, so additions, removals,
and signature changes are all reviewed deltas. Snapshots SHALL be produced and updated only through
the `public-api` capture/diff/update lifecycle, never hand-edited and never through inline
`declared_api` lists.

`ArchLinterNet.Cli` SHALL NOT be governed by a public API surface contract: it is a packed executable
tool whose compatibility boundary is its command line, not its assembly surface.

Whole-assembly membership SHALL be used rather than a bounded `surface_selector`, because for these
three packages the entire exported surface is the shipped compatibility surface and a selected
surface would leave the remainder ungoverned.

#### Scenario: An unreviewed exported member fails the read-only gate
- **WHEN** a governed assembly exports a member absent from its reviewed snapshot
- **THEN** strict validation fails and the snapshot file is not rewritten

#### Scenario: A reviewed entry that no longer exists fails the read-only gate
- **WHEN** a reviewed snapshot declares a signature the live surface no longer exports
- **THEN** exact comparison reports the removal and strict validation fails

### Requirement: Project metadata, friend assemblies, and package boundaries are governed
The repository's architecture contract SHALL declare `strict_project_metadata` contracts restricting
each shipped project's `InternalsVisibleTo` set to a reviewed allowlist, forbidding shipped projects
from referencing `tests/**` or `benchmarks/**` projects, and forbidding a shipped project from being
a test project. It SHALL declare package and FrameworkReference contracts stating that no shipped
project declares a test or benchmark framework package, that MSBuild/Buildalyzer and dependency
injection container packages are declared only by `ArchLinterNet.Core`, that `ArchLinterNet.CEL`
declares no `PackageReference` at all, and that every shipped project carries only the implicit base
runtime framework.

The complete current package graph SHALL NOT be frozen as an exhaustive allow-list, and incidental
SDK or build-style properties SHALL NOT be frozen.

#### Scenario: An unreviewed friend assembly fails the gate
- **WHEN** a shipped project declares an `InternalsVisibleTo` assembly absent from its reviewed allowlist
- **THEN** strict validation reports the project and the friend assembly name

#### Scenario: An undeclared framework reference fails the gate
- **WHEN** a shipped project declares a `FrameworkReference` outside the reviewed base runtime group
- **THEN** strict validation reports the project and the framework name

### Requirement: Repeated external rules are authored once and expanded through source sets
The repository's architecture contract SHALL express the dependency-injection-container and
MSBuild-project-evaluation boundaries as one authored rule each, expanded over a named `source_sets`
layer inventory and subtracted with `exclude_sources` for the layers where the dependency is the
architecture, rather than as one copy-pasted rule per layer. Expansion SHALL NOT weaken enforcement
or widen the declared exceptions, and each authored rule SHALL carry a stable `id` included in
`scope: rule_input` coverage so a renamed or deleted layer cannot silently turn the guard into a
no-op.

#### Scenario: An expanded external rule is covered through its authored id
- **WHEN** the strict gate runs
- **THEN** the `scope: rule_input` coverage summary reports every expanded instance as covered, with `stale` and `unresolved` at `0`

#### Scenario: A source set member that no longer resolves is rejected
- **WHEN** a source set lists a member absent from the policy input it resolves against
- **THEN** policy preparation fails with a diagnostic naming the set and the member

### Requirement: The post-refactor extension seams are executable, not documentation-only
The repository's architecture contract SHALL make the seams produced by the #451/#452/#453 refactors
executable where current engine evidence expresses them precisely: family-checker types SHALL reside
in `ArchLinterNet.Core.Execution.Checkers`, every `ArchitectureDiagnostic` subtype and every
`IArchitectureDiagnosticPayload` implementation SHALL reside in `ArchLinterNet.Core.Model`, and
`IArchitecturePolicyRawDocumentValidator` / `IArchitecturePolicyDocumentValidator` implementations
SHALL reside in `ArchLinterNet.Core.Contracts.RawValidators` and
`ArchLinterNet.Core.Contracts.Validators` respectively.

Interface and base-type evidence SHALL be preferred over naming evidence wherever it exists. Naming
evidence SHALL be used only where no structural evidence selects the type — currently only family
checkers, which are static classes bound through the `ArchitectureContractChecker` delegate.
Attributes SHALL NOT be added to production code merely to make a selector possible.

#### Scenario: A checker declared outside the extracted seam fails the gate
- **WHEN** a family-checker type resides outside `ArchLinterNet.Core.Execution.Checkers`
- **THEN** strict validation reports the type and its actual location

#### Scenario: A diagnostic payload declared outside the model layer fails the gate
- **WHEN** a type implementing `IArchitectureDiagnosticPayload` resides outside `ArchLinterNet.Core.Model`
- **THEN** strict validation reports the type and its actual location

### Requirement: The self-policy developer workflow exposes read-only and writing commands separately
The repository SHALL expose thin Make entrypoints over already-shipped CLI capabilities rather than a
second orchestration framework: a fast policy-only check that performs no project evaluation or
assembly loading, a read-only reviewed public API check, a non-writing update preview, an explicit
snapshot update, and an explain/provenance entrypoint. Normal lint, acceptance, and CI SHALL NOT
mutate reviewed API snapshots or the policy. The existing acceptance ordering that serializes
build-output-mutating checks SHALL be preserved.

#### Scenario: The fast policy-only gate rejects an invalid policy without analysis
- **WHEN** `make policy-check` runs against a malformed or import-invalid policy
- **THEN** it fails without evaluating projects or loading target assemblies

#### Scenario: The read-only API check never rewrites a snapshot
- **WHEN** the reviewed public API check runs against a drifted surface
- **THEN** it reports the drift, fails, and leaves every snapshot file byte-identical

### Requirement: Every supported contract family has a recorded self-policy decision
The repository SHALL maintain `docs/internal/self-policy-capability-matrix.md`, recording for every
contract family the engine currently supports: the repository invariant it could protect, its
evidence source, an explicit `adopt` / `already covered` / `not applicable` / `defer` decision, the
reason, and the negative regression proving any adopted guard. It SHALL also record engine semantics
found to be unsupported or surprising during review, so future work does not author policy that looks
plausible but is not executable. A family SHALL NOT be enabled solely to claim dogfooding.

#### Scenario: A new contract family is added to the engine
- **WHEN** a change adds a contract family to the engine
- **THEN** the capability matrix gains a row with that family's decision and rationale

### Requirement: Release forensics module boundaries are self-governed
The repository self-policy SHALL declare and exercise explicit namespace layers
for the Release Architecture Forensics canonical utility, Git ingestion,
configuration, task extraction, canonical file-evidence construction, scoring,
reporting, optional .NET enrichment, and CLI History command modules. Existing
strict dependency contracts SHALL ensure raw Git ingestion cannot depend on
evidence, scoring, reporting, or enrichment; evidence construction cannot
depend on scoring, reporting, or enrichment; scoring may consume finalized
evidence but cannot read raw Git ingestion, render reports, or use enrichment;
optional enrichment cannot depend on report rendering; report rendering cannot
depend on raw Git ingestion; and the CLI History command cannot import internal
Git, configuration, task, evidence, scoring, canonical-utility, or enrichment
modules.

The parent History namespace MAY remain the composition seam coordinating the
finalized reusable result. The policy SHALL include every new rule ID in the
existing rule-input coverage contract; it SHALL not add a test-only policy or a
new contract family for this purpose.

#### Scenario: Report rendering remains independent of Git ingestion
- **WHEN** a production report-rendering type is scanned by the repository
  self-policy
- **THEN** importing a raw Git-ingestion namespace is a strict architecture
  violation while consuming finalized evidence through the reusable History
  result remains allowed

#### Scenario: Scoring cannot reach back into evidence construction
- **WHEN** a production canonical-evidence construction type imports a History
  scoring namespace
- **THEN** the strict self-policy reports a violation
- **AND** a scorer may import the finalized History evidence namespace without
  importing raw Git-ingestion types

#### Scenario: History command bypass is introduced
- **WHEN** a CLI History command type imports a History scoring, evidence, Git,
  configuration, task, canonical-utility, or enrichment implementation type
- **THEN** the strict self-policy reports a violation rather than allowing the
  CLI to bypass the reusable History composition/result seam

### Requirement: New or grown production partial-type aggregates are blocked

The repository's architecture contract SHALL declare a strict `layout_conventions` rule reusing
`max_declarations_per_type: 1` over production source (`folder_segment: src`), separate from the
existing audit-only `production-types-have-one-source-declaration` rule, which remains unchanged as
the full-debt inventory `decompose-god-classes` targets. The strict rule SHALL freeze today's
reviewed offending types through exact-match `ignored_violations` entries (exact `source_type` and
exact `forbidden_reference`, matching this repository's already-shipped ignore mechanism) rather
than a new baseline file, metric kind, or per-type numeric-override schema field. Every reviewed
entry SHALL be an exact snapshot of one type's current declaration count and file list.

#### Scenario: An unchanged known aggregate remains accepted debt
- **WHEN** a reviewed type's declarations exactly match its frozen `ignored_violations` entry
- **THEN** `make lint-architecture` does not report a violation for that type

#### Scenario: A reviewed aggregate gains a declaration
- **WHEN** a type with a frozen reviewed entry gains an additional source declaration
- **THEN** its declaration text and canonical identity change, the frozen entry no longer matches,
  and strict validation fails for that type

#### Scenario: A new handwritten partial type is introduced
- **WHEN** a production type outside the reviewed exception list is declared across more than one
  source file
- **THEN** strict validation fails naming the type, its actual declaration count, and its paths

#### Scenario: A reviewed aggregate is fully resolved
- **WHEN** a reviewed type's declarations are reduced to exactly one
- **THEN** the checker stops reporting a candidate for that type and its now-stale
  `ignored_violations` entry must be removed in the same change, because
  `unmatched_ignored_violations` fails closed by default

#### Scenario: Improving a reviewed aggregate without finishing it changes required evidence
- **WHEN** a reviewed type's declaration count decreases but remains above one
- **THEN** its frozen entry's exact text no longer matches the new count, and the change must update
  that entry to the new exact evidence for strict validation to pass

#### Scenario: The audit inventory remains the full-debt authority
- **WHEN** `make audit-architecture` runs `production-types-have-one-source-declaration`
- **THEN** it continues to report every production type above one declaration, including reviewed
  entries accepted by the strict ratchet rule
