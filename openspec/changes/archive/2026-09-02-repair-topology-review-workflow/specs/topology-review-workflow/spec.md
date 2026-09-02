## MODIFIED Requirements

### Requirement: Capture emits deterministic review candidates for supported subjects
The system SHALL provide a read-only topology capture operation that accepts one supported
first-party subject kind (`type`, `namespace`, `project`, or `assembly`) and emits a versioned,
machine-readable capture document. The document SHALL retain deterministic canonical candidate
subjects and directed dependency witnesses from one analysis session, and repeated captures of
unchanged inputs SHALL be byte-stable. Capture SHALL work when the policy has no declared topology
and SHALL never modify a reviewed policy, imported policy source, baseline, assembly, receipt,
project, asmdef, or any physical-file alias of an analysis input. Capture publication SHALL write a
temporary sibling file and atomically replace the requested non-input output only after successful
document generation.

#### Scenario: Unchanged assembly capture is byte-stable
- **WHEN** a policy is captured twice for the same unchanged first-party assembly topology
- **THEN** both capture documents have the same versioned shape and identical bytes with subjects
  and relationships in canonical order

#### Scenario: Type capture remains a review candidate
- **WHEN** a user captures a type-level topology
- **THEN** the output identifies the observed type candidates and relationships without inventing
  exact-type mapping selectors or writing a topology declaration

#### Scenario: Capture output aliases a trusted input
- **WHEN** a capture output names a symbolic link, hard link, or other physical-file alias of a
  consumed analysis input
- **THEN** capture fails before publication and leaves that input unchanged

#### Scenario: Capture publication fails
- **WHEN** generation or publication fails after a temporary output has been created
- **THEN** the requested output's prior content remains unchanged and the temporary artifact is
  removed

### Requirement: Diff distinguishes declared-versus-observed topology categories
The system SHALL provide a topology diff operation that consumes the declared topology evaluation
produced by ordinary validation and renders deterministic structural mapping, relational forbidden
edge, unmapped-subject, and stale-declaration categories separately. Relational entries SHALL
retain the evaluator's deterministic dependency witness; reviewed out-of-scope evidence SHALL be
visible without being reported as unmapped or drift. Diff SHALL fail with an actionable typed
diagnostic when the policy has no declared topology and SHALL not modify a reviewed policy, imported
policy source, baseline, assembly, receipt, project, asmdef, or any physical-file alias of an
analysis input. Diff publication SHALL be atomic and SHALL reject an output that aliases such an
input.

#### Scenario: Diff exposes mapping and relationship drift distinctly
- **WHEN** a declared exhaustive topology has an unmapped observed subject and a prohibited
  relationship between two correctly mapped components
- **THEN** the diff reports the unmapped subject and the forbidden directed relationship with its
  witness in separate deterministic categories

#### Scenario: Complete evidence exposes stale declarations
- **WHEN** stale declarations are enabled and ordinary evaluation has complete mapping evidence
- **THEN** the diff lists stale nodes and stale directed edges separately from structural and
  relational entries

#### Scenario: Diff output aliases a trusted input
- **WHEN** a diff output names a symbolic link, hard link, or other physical-file alias of a
  consumed analysis input
- **THEN** diff fails before publication and leaves that input unchanged

### Requirement: Lifecycle fixtures prove .NET and Unity behavior
The system SHALL provide realistic .NET server/library and Unity-style topology fixtures that
exercise the real capture, diff, and verification command lifecycle without automatically accepting
the generated candidate. Automated acceptance tests SHALL build the .NET fixture, materialize the
Unity-style assemblies in the fixture's `Library/ScriptAssemblies` layout, and invoke real capture,
diff, and verify commands. They SHALL prove repeat-capture byte stability; structural, relational,
unmapped, and stale categories; strict and audit exit semantics; and unchanged hashes for policy,
import, asmdef, and all other consumed source inputs.

#### Scenario: .NET server/library lifecycle is reviewable
- **WHEN** automated acceptance captures, diffs, and verifies the .NET server/library fixture
- **THEN** its candidate observations and declared topology categories remain deterministic, strict
  and audit outcomes follow ordinary validation, and no command rewrites a consumed fixture input

#### Scenario: Unity lifecycle is reviewable
- **WHEN** automated acceptance materializes the Unity-style fixture assemblies and captures,
  diffs, and verifies that fixture
- **THEN** its assembly and asmdef-oriented candidate observations and declared topology categories
  remain deterministic, strict and audit outcomes follow ordinary validation, and no command
  rewrites a consumed fixture input

## ADDED Requirements

### Requirement: Topology capture respects Core's protected execution boundary
The `Topology` application surface SHALL consume a neutral topology-observation projection and
SHALL NOT import Execution-owned evaluator, subject, dependency, or generic collection types. The
projection SHALL preserve the ordinary evaluator's observed topology semantics without introducing a
second evaluator.

#### Scenario: Protected contract inspection includes Topology
- **WHEN** the Core protected-contract tests inspect the `Topology` namespace and its
  compiler-generated types
- **THEN** no Topology importer references the protected Execution namespace while capture retains
  the ordinary evaluator's observed subjects and relationships

### Requirement: Nested topology command diagnostics identify the selected command
The CLI SHALL derive parse-error usage hints from the full command ancestry so an error in a nested
topology subcommand identifies `topology capture`, `topology diff`, or `topology verify` rather than
a same-named command family.

#### Scenario: Nested diff parse error
- **WHEN** parsing fails for `topology diff`
- **THEN** the usage hint identifies the topology diff command and not the baseline diff command

#### Scenario: Nested capture or verify parse error
- **WHEN** parsing fails for `topology capture` or `topology verify`
- **THEN** the usage hint identifies that topology subcommand and not a public-API capture or
  baseline verify command

### Requirement: Topology validation uses ordinary execution semantics
Topology diff and verify SHALL construct native validation through the same request mapper and
post-native external-evidence binding seam as the ordinary validation command. They SHALL carry
waiver evaluation date, external-evidence bindings, and external-evidence assessment context
without introducing a partial topology-only execution request.

#### Scenario: External evidence changes topology verify outcome
- **WHEN** a topology verify invocation supplies a declared external-evidence binding that makes
  ordinary validation fail or become unassessable
- **THEN** topology verify returns the corresponding ordinary validation exit outcome after one
  native validation call
