# architecture-remediation-hints Specification

## Purpose
Define optional, deterministic, evidence-backed architectural remediation
guidance attached to normalized findings without authorizing automatic edits or
policy weakening.
## Requirements
### Requirement: Normalized findings can carry deterministic remediation hints
The system SHALL attach an optional typed remediation hint to a normalized architecture finding only when the existing diagnostic and canonical identity provide sufficient deterministic evidence. A hint SHALL retain the stable contract identity, the complete canonical finding identity, an enum-backed finite category, deterministic summary, ordered evidence, optional expected seam or direction, optional caveat, and explicit review requirement.

#### Scenario: Same-named cross-assembly findings retain distinct hint identity
- **WHEN** two findings have the same source type name but distinct canonical source assemblies or targets
- **THEN** each attached remediation hint retains that finding's own canonical identity and the hints are distinguishable without parsing display text

#### Scenario: Missing evidence does not invent architecture intent
- **WHEN** a diagnostic has no declared approved seam, ownership fact, classification fact, or other deterministic remediation evidence
- **THEN** the system attaches no specialized hint or attaches the finite `review_contract` category, and does not invent a layer, port, adapter, shared module, or ownership boundary

### Requirement: Hint categories are finite and safety-oriented
The system SHALL expose only the documented machine-readable categories `move_code`, `depend_on_abstraction`, `invert_dependency`, `introduce_adapter`, `use_declared_port`, `fix_classification`, `fix_policy_input`, `narrow_exception`, `remove_or_replace_dependency`, and `review_contract`. Hints SHALL never recommend broad ignored violations, broad source or target exclusions, allow-list growth solely to accept an observed edge, scope reduction, baselining new debt, deleting a contract without evidence, or changing strict mode to audit as a default repair.

#### Scenario: A reviewed exception remains narrow
- **WHEN** the system emits a `narrow_exception` hint
- **THEN** its caveat identifies the affected canonical finding/edge, marks the exception as requiring explicit review, and does not suggest a wildcard or broad ignore

#### Scenario: A forbidden edge has no known safe seam
- **WHEN** a dependency violation has no policy-evidenced abstraction, port, adapter, or ownership direction
- **THEN** its hint is absent or `review_contract`, rather than `depend_on_abstraction`, `invert_dependency`, or `introduce_adapter`

### Requirement: Specialized guidance uses existing typed evidence
The system SHALL produce specialized categories only from architecture facts already represented by policy and the normalized diagnostic. Port-boundary diagnostics with declared port or adapter evidence SHALL guide use of that declared seam; placement and classification facts SHALL guide code movement or classification repair; coverage/preflight/policy-input facts SHALL guide policy-input repair; and external/package/framework boundaries with no declared seam SHALL guide bounded removal or replacement.

#### Scenario: A direct cross-context edge has an approved port
- **WHEN** a port-boundary diagnostic records a direct edge and its expected port seam
- **THEN** the hint category is `use_declared_port` and its evidence identifies the expected seam

#### Scenario: An existing adapter is in the wrong context
- **WHEN** a port-boundary diagnostic records `adapter_context` and its expected port seam
- **THEN** the hint category is `move_code` and directs the existing adapter to the declared adapter context

#### Scenario: An existing adapter implements the wrong port
- **WHEN** a port-boundary diagnostic records `adapter_port_mismatch` and its expected port seam
- **THEN** the hint category is `use_declared_port` and directs the existing adapter to implement that seam without creating a second adapter

#### Scenario: A configuration-shaped diagnostic lacks unambiguous repair evidence
- **WHEN** a configuration diagnostic can represent either a policy input problem or a forbidden dependency preserved with template metadata
- **THEN** it uses `review_contract` rather than `fix_policy_input`

#### Scenario: An unmatched ignore is a pattern rather than a current edge
- **WHEN** an unmatched ignore contains wildcard source or reference patterns
- **THEN** it uses `fix_policy_input` to remove the stale rule and does not suggest an exact-edge exception

### Requirement: Structured remediation guidance evolves the JSON envelope additively
The system SHALL publish structured remediation guidance at `remediation_guidance`. It SHALL NOT replace or change the type of an existing family-owned `remediation_hint` value while the normalized finding schema version remains unchanged.

#### Scenario: Port-boundary legacy hint remains a string
- **WHEN** a port-boundary diagnostic has its existing remediation-hint text and normalized guidance
- **THEN** `remediation_hint` remains the legacy string in both the envelope and details, while `remediation_guidance` contains the structured object

#### Scenario: External dependency has no declared seam
- **WHEN** an external, package, or framework boundary diagnostic has no existing adapter or port evidence
- **THEN** the hint category is `remove_or_replace_dependency` with a caveat that no alternative seam was evidenced
