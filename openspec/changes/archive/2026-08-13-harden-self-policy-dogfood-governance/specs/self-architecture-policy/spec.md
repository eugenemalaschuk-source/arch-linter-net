## MODIFIED Requirements

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

## ADDED Requirements

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
