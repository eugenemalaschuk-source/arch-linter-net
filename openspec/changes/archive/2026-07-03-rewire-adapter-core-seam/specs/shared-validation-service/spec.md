## MODIFIED Requirements

### Requirement: One service drives CLI validation, the public API, and the Testing adapter
`ArchLinterNet.Core.Validation.Abstractions.IArchitectureValidationApplicationService.Validate`, reached through a composed `ArchLinterNet.Core.Composition.ArchitectureEngine`, SHALL be the single implementation of the policy-load → condition-set-resolution → repository-root-resolution → assembly-resolution → runner-creation → contract-execution → result-aggregation pipeline. The CLI `validate` command, the public `ArchitectureValidator` API, and the `ArchitectureAssertions` Testing adapter SHALL each build (and reuse) their own `ArchitectureEngine` via `new ArchitectureEngineBuilder().AddArchLinterNetCore().Build()` and call `engine.Validate(...)` rather than re-implementing the pipeline or calling the static `ArchitectureValidationService.Validate` facade directly. The static facade SHALL remain available as a compatibility entry point for other callers and SHALL continue to produce identical outcomes.

#### Scenario: CLI validate delegates to the composed engine
- **WHEN** the CLI `validate` command runs
- **THEN** it SHALL build a `ValidationRequest` and call `ArchitectureEngine.Validate`, performing no policy loading, condition-set resolution, assembly resolution, or contract execution itself

#### Scenario: Public API delegates to the composed engine
- **WHEN** `ArchitectureValidator.Validate(...)` is called
- **THEN** it SHALL build a `ValidationRequest` with `Mode = "strict"` and call `ArchitectureEngine.Validate`, translating the returned `ValidationOutcome` into its `out` parameters and boolean return value

#### Scenario: Testing adapter delegates to the composed engine
- **WHEN** `ArchitectureValidationBuilder.ValidateStrict()` or `ValidateAudit()` is called
- **THEN** it SHALL build a `ValidationRequest` with the corresponding `Mode` and call `ArchitectureEngine.Validate`, wrapping the returned `ValidationOutcome` in an `ArchitectureValidationResult`
