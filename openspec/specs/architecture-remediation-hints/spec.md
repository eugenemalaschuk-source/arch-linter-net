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

#### Scenario: External dependency has no declared seam
- **WHEN** an external, package, or framework boundary diagnostic has no existing adapter or port evidence
- **THEN** the hint category is `remove_or_replace_dependency` with a caveat that no alternative seam was evidenced
