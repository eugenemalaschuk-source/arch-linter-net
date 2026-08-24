## ADDED Requirements

### Requirement: Focused comparison boundaries preserve normalized guardrail semantics

`ArchitecturePolicyWeakeningComparer.Compare(...)` SHALL remain the stable
public comparison façade and the sole deterministic aggregation point. It
SHALL orchestrate focused internal boundaries for enforcement, authored
analysis scope, static/source scope, contract facts and optionality,
exceptions, and selector/membership evidence. Comparison validation,
membership-evidence resolution, and canonical context-digest calculation SHALL
be owned by comparison/shared support rather than formatter-facing internals.
Human, JSON, and SARIF formatting SHALL remain projections of the normalized
comparison result; evaluation SHALL not depend on a formatter. Evaluators SHALL
not load YAML, inspect live repository state, or reanalyse a candidate policy.

#### Scenario: Cross-family comparison remains normalized and deterministic
- **WHEN** independently changed policy contexts produce findings from more
  than one comparison family
- **THEN** the public façade returns the same de-duplicated, ordinally ordered
  normalized findings and all output projections preserve their established
  identities and evidence
