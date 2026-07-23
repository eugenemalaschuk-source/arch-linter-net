## MODIFIED Requirements

### Requirement: Emit deterministic composition diagnostics

The system SHALL emit, for each composition violation, a diagnostic identifying the violating type, the violating source member, the matched forbidden API's fully-qualified name, the contract, and the expected composition boundary, with violations ordered deterministically by the violating type's fully-qualified name, then by matched API (ordinal), then by source member (ordinal), and at most one violation per (type, source member, matched API) tuple.

Each composition violation's baseline/ignore identity (`ArchitectureViolationIdentity`) SHALL be qualified with the violating type's declaring assembly (`SourceAssembly`), the violating source member (`SourceMember`), and the matched forbidden API (`TargetMember`), and SHALL classify as `Kind: "call"`. Baselining or `ignored_violations` matching SHALL therefore distinguish two same-named types with the same simple name and namespace in different assemblies, and SHALL distinguish two distinct forbidden-call occurrences from different source members of the same type.

#### Scenario: Diagnostic identifies the forbidden composition API usage
- **WHEN** a type outside the allowed composition boundary calls a forbidden API
- **THEN** the emitted diagnostic SHALL include the violating type's fully-qualified name, the violating source member, the matched forbidden API, and the expected composition boundary description

#### Scenario: Repeated runs produce identical ordering
- **WHEN** the same policy is validated twice against the same assemblies with multiple composition violations
- **THEN** the reported violations SHALL appear in the same order both times

#### Scenario: Same-named types in different assemblies do not collide
- **WHEN** two different assemblies each contain a type with the same simple name and namespace outside the composition boundary (e.g. two `Program` types), both calling the same forbidden API, and one occurrence is baselined
- **THEN** the baseline entry SHALL suppress only the violation from its own source assembly; the same-named violation in the other assembly SHALL still be reported as new debt by `validate --baseline`

#### Scenario: Distinct occurrences in one type each get a distinct identity
- **WHEN** a single type outside the composition boundary calls two distinct forbidden APIs from two different source members, and only one occurrence's baseline entry is applied
- **THEN** the other occurrence SHALL still be reported as a violation; baselining one SHALL NOT suppress the other
