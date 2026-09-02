## ADDED Requirements

### Requirement: Capture emits deterministic review candidates for supported subjects
The system SHALL provide a read-only topology capture operation that accepts one supported
first-party subject kind (`type`, `namespace`, `project`, or `assembly`) and emits a versioned,
machine-readable capture document. The document SHALL retain deterministic canonical candidate
subjects and directed dependency witnesses from one analysis session, and repeated captures of
unchanged inputs SHALL be byte-stable. Capture SHALL work when the policy has no declared topology
and SHALL never modify the reviewed policy, an imported policy source, or a baseline.

#### Scenario: Unchanged assembly capture is byte-stable
- **WHEN** a policy is captured twice for the same unchanged first-party assembly topology
- **THEN** both capture documents have the same versioned shape and identical bytes with subjects
  and relationships in canonical order

#### Scenario: Type capture remains a review candidate
- **WHEN** a user captures a type-level topology
- **THEN** the output identifies the observed type candidates and relationships without inventing
  exact-type mapping selectors or writing a topology declaration

### Requirement: Diff distinguishes declared-versus-observed topology categories
The system SHALL provide a topology diff operation that consumes the declared topology evaluation
produced by ordinary validation and renders deterministic structural mapping, relational forbidden
edge, unmapped-subject, and stale-declaration categories separately. Relational entries SHALL
retain the evaluator's deterministic dependency witness; reviewed out-of-scope evidence SHALL be
visible without being reported as unmapped or drift. Diff SHALL fail with an actionable typed
diagnostic when the policy has no declared topology and SHALL not modify policy data.

#### Scenario: Diff exposes mapping and relationship drift distinctly
- **WHEN** a declared exhaustive topology has an unmapped observed subject and a prohibited
  relationship between two correctly mapped components
- **THEN** the diff reports the unmapped subject and the forbidden directed relationship with its
  witness in separate deterministic categories

#### Scenario: Complete evidence exposes stale declarations
- **WHEN** stale declarations are enabled and ordinary evaluation has complete mapping evidence
- **THEN** the diff lists stale nodes and stale directed edges separately from structural and
  relational entries

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
exercise capture, diff, and verification without automatically accepting the generated candidate.

#### Scenario: .NET server/library lifecycle is reviewable
- **WHEN** the .NET server/library fixture is captured, diffed, and verified
- **THEN** its candidate observations and declared topology categories remain deterministic and
  no command rewrites the fixture policy

#### Scenario: Unity lifecycle is reviewable
- **WHEN** the Unity-style fixture is captured, diffed, and verified
- **THEN** its assembly/asmdef-oriented candidate observations and declared topology categories
  remain deterministic and no command rewrites the fixture policy
