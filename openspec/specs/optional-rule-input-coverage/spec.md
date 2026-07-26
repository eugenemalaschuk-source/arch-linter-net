# optional-rule-input-coverage Specification

## Purpose
TBD - created by archiving change optional-rule-inputs. Update Purpose after archive.
## Requirements
### Requirement: Exact optional rule-input declarations
The policy schema SHALL allow a `scope: rule_input` coverage contract to declare `optional_inputs`. Each declaration SHALL contain a selected `contract_id`, the exact layer-bearing `input` field, its `layer` value, and a non-empty `reason`. The loader SHALL retain authored policy provenance for the declaration and SHALL reject duplicate, unknown, or non-matching declarations.

#### Scenario: An optional future target is declared exactly
- **WHEN** a rule-input coverage contract declares an optional input for one selected contract's `forbidden` field and empty future layer
- **THEN** the declaration is accepted only when its contract ID, input field, layer, and non-empty reason match that exact referenced input

#### Scenario: A stale optional declaration fails closed
- **WHEN** an optional-input declaration names a contract, input field, or layer value that is not an actual selected rule input
- **THEN** policy loading rejects it with actionable stale or unknown identity evidence

### Requirement: Optional-empty is a first-class rule-input state
The system SHALL classify an exact optional input whose declared layer resolves but matches no code as `optional-empty`, SHALL not emit an `empty-input` finding for that input, and SHALL keep every unrelated input subject to ordinary coverage. When matching code later appears, the system SHALL classify that input as covered without requiring policy changes. Unresolved layers SHALL remain unknown and fail closed even when an optional declaration exists.

#### Scenario: One future input does not weaken its contract
- **WHEN** a rule references one populated layer and one exact optional empty layer
- **THEN** coverage reports the populated input as covered and the empty input as optional-empty without an error

#### Scenario: An undeclared empty input still fails
- **WHEN** another input in the same contract is empty and has no matching optional declaration
- **THEN** coverage emits the existing `empty-input` finding for that input

### Requirement: Optional-empty provenance is preserved through imports
The system SHALL preserve an optional input declaration's authored source location and reason when it is composed from an imported policy fragment.

#### Scenario: Imported optional declaration is reported at its source
- **WHEN** an imported fragment declares an optional empty input
- **THEN** structured coverage output identifies the fragment location and its authored reason

