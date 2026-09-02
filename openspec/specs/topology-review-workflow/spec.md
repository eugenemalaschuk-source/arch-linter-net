# topology-review-workflow Specification

## Purpose

Provide deterministic, review-only topology capture, diff, and focused verification workflows
without bypassing or mutating the existing declared-topology validation model.
## Requirements
### Requirement: Capture emits deterministic review candidates for supported subjects
The system SHALL provide a read-only topology capture operation that accepts one supported
first-party subject kind (`type`, `namespace`, `project`, or `assembly`) and emits a versioned,
machine-readable capture document. The document SHALL retain deterministic canonical candidate
subjects and directed dependency witnesses from one analysis session, and repeated captures of
unchanged inputs SHALL be byte-stable. Capture SHALL work when the policy has no declared topology
and SHALL never modify a reviewed policy, imported policy source, baseline, assembly, receipt,
project, asmdef, source, or any physical-file alias of an analysis input. Capture SHALL use only
the session's consumed-input provenance to detect existing physical aliases and SHALL NOT recurse
through unrelated repository directories to reconstruct inputs. A non-existent output path SHALL
not trigger physical-alias discovery. Capture publication SHALL write a temporary sibling file and
atomically replace the requested non-input output only after successful document generation.
Cancellation during publication SHALL be reported as cancellation after temporary cleanup rather
than as an output-write failure.

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

#### Scenario: Capture skips unrelated output discovery
- **WHEN** a capture has a new output path and the repository contains large, generated, or
  inaccessible unrelated directories
- **THEN** capture does not traverse those directories while checking output collision

#### Scenario: Capture publication is cancelled
- **WHEN** cancellation occurs after capture creates its temporary output and before atomic rename
- **THEN** capture removes the temporary artifact and reports a typed cancellation result

#### Scenario: Capture publication fails
- **WHEN** generation or publication fails after a temporary output has been created
- **THEN** the requested output's prior content remains unchanged and the temporary artifact is
  removed

### Requirement: Diff distinguishes declared-versus-observed topology categories
The system SHALL provide a topology diff operation that consumes the declared topology evaluation
produced by ordinary validation and renders deterministic structural mapping, relational forbidden
edge, unmapped-subject, and stale-declaration categories separately. The diff document SHALL retain
the single native declared-topology applicability record, including state, reasons, membership, and
provenance. Relational entries SHALL retain the evaluator's deterministic dependency witness;
reviewed out-of-scope evidence SHALL be visible without being reported as unmapped or drift. When
ordinary validation reports the non-projectable `unexpected_empty_input` applicability reason, diff
SHALL emit a typed unassessable artifact and return the ordinary runtime-error exit code instead of
presenting empty review categories as a clean result. Unmapped, ambiguous, and stale evidence that
is projectable review evidence SHALL remain review categories rather than being converted into a
runtime error. Diff SHALL fail with an actionable typed diagnostic when the policy has no declared
topology and SHALL not modify a reviewed policy, imported policy source, baseline, assembly,
receipt, project, asmdef, source, or any physical-file alias of an analysis input. Diff SHALL use
only consumed-input provenance for existing physical alias checks, SHALL skip physical-alias checks
for new outputs, and SHALL publish atomically.

#### Scenario: Diff exposes mapping and relationship drift distinctly
- **WHEN** a declared exhaustive topology has an unmapped observed subject and a prohibited
  relationship between two correctly mapped components
- **THEN** the diff reports the unmapped subject and the forbidden directed relationship with its
  witness in separate deterministic categories

#### Scenario: Diff exposes non-projectable applicability
- **WHEN** an exhaustive topology with `allow_empty: false` observes no subjects and ordinary
  validation reports `unexpected_empty_input`
- **THEN** the diff artifact retains that unassessable applicability record and the command returns
  the ordinary runtime-error exit code

#### Scenario: Complete evidence exposes stale declarations
- **WHEN** stale declarations are enabled and ordinary evaluation has complete mapping evidence
- **THEN** the diff lists stale nodes and stale directed edges separately from structural and
  relational entries

#### Scenario: Diff output aliases a trusted input
- **WHEN** a diff output names a symbolic link, hard link, or other physical-file alias of a
  consumed analysis input
- **THEN** diff fails before publication and leaves that input unchanged

### Requirement: Focused verification uses normal topology validation semantics
The system SHALL provide a topology verify operation that invokes normal validation once for the
selected strict or audit mode and projects the resulting declared-topology evidence. Its pass/fail
and applicability semantics SHALL be those of the ordinary validation outcome; it SHALL NOT
introduce a second evaluator, a topology-specific applicability envelope, or policy mutation.

#### Scenario: Strict verification matches ordinary validation
- **WHEN** a policy has a declared topology with a forbidden observed component relationship
- **THEN** topology verify and ordinary strict validation expose the same topology finding and
  fail state

#### Scenario: Audit verification preserves audit behavior
- **WHEN** a policy runs topology verification in audit mode
- **THEN** the operation uses the same evaluator and audit result semantics as ordinary audit
  validation

### Requirement: Lifecycle fixtures prove .NET and Unity behavior
The system SHALL provide realistic .NET server/library and Unity-style topology fixtures that
exercise the real capture, diff, and verification command lifecycle without automatically accepting
the generated candidate. Automated acceptance tests SHALL build the .NET fixture, materialize the
Unity-style assemblies in the fixture's `Library/ScriptAssemblies` layout, and invoke real capture,
diff, and verify commands with separate output artifacts. They SHALL prove repeat-capture byte
stability; structural, relational, unmapped, and stale categories; strict and audit exit semantics;
publication artifact existence; and unchanged hashes for policy, imports, asmdef, source, project,
and all other consumed fixture inputs.

#### Scenario: .NET server/library lifecycle is reviewable
- **WHEN** automated acceptance captures, diffs, and verifies the .NET server/library fixture
- **THEN** its candidate observations and declared topology categories remain deterministic, strict
  and audit outcomes follow ordinary validation, each requested artifact exists, and no command
  rewrites a consumed fixture input

#### Scenario: Unity lifecycle is reviewable
- **WHEN** automated acceptance materializes the Unity-style fixture assemblies and captures,
  diffs, and verifies that fixture
- **THEN** its assembly and asmdef-oriented candidate observations and declared topology categories
  remain deterministic, strict and audit outcomes follow ordinary validation, each requested
  artifact exists, and no command rewrites a consumed fixture input

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

### Requirement: Topology help matches registered options
The CLI SHALL advertise only options registered by the selected topology subcommand. Capture SHALL
not advertise validation-only waiver or external-evidence options. Diff and verify SHALL advertise
the common registered ordinary-validation options, including waiver evaluation date,
external-evidence binding, and evidence assessment context.

#### Scenario: Help and parser remain aligned
- **WHEN** a user inspects capture, diff, or verify help and invokes an advertised option
- **THEN** that option is registered for that command, and no validation-only option is advertised
  for capture
