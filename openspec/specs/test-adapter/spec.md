# Test Adapter Specification

## Purpose
Provides a fluent NUnit-friendly test adapter that loads a policy by path or repository root and asserts strict/audit validation results.

## Requirements

### Requirement: Test adapter loads policy from path
`ArchitectureAssertions.FromPolicy(path)` SHALL return an `ArchitectureValidationBuilder` configured to load the YAML policy from the given file path.

#### Scenario: FromPolicy returns valid builder
- **WHEN** `FromPolicy("architecture/dependencies.arch.yml")` is called
- **THEN** a non-null `ArchitectureValidationBuilder` SHALL be returned

### Requirement: Test adapter loads policy from repository root
`ArchitectureAssertions.FromRepositoryRoot(root)` SHALL resolve the policy at `{root}/architecture/dependencies.arch.yml` and return a configured builder.

#### Scenario: FromRepositoryRoot with valid root
- **WHEN** `FromRepositoryRoot("/some/repo")` is called
- **THEN** the builder SHALL resolve to `/some/repo/architecture/dependencies.arch.yml`

### Requirement: Test adapter supports strict validation
`ArchitectureValidationBuilder.ValidateStrict()` SHALL run strict-mode contracts and return an `ArchitectureValidationResult`.

#### Scenario: ValidateStrict passes with clean policy
- **WHEN** `ValidateStrict()` is called on a builder with a policy that has no strict violations
- **THEN** `Passed` SHALL be `true` and `Violations` SHALL be empty

#### Scenario: ValidateStrict catches violations
- **WHEN** `ValidateStrict()` is called on a builder with a policy that has known strict violations
- **THEN** `Passed` SHALL be `false` and `Violations` SHALL contain the expected violations

### Requirement: Test adapter supports audit validation
`ArchitectureValidationBuilder.ValidateAudit()` SHALL run audit-mode contracts and return an `ArchitectureValidationResult`.

#### Scenario: ValidateAudit returns result
- **WHEN** `ValidateAudit()` is called
- **THEN** an `ArchitectureValidationResult` SHALL be returned

### Requirement: Test adapter throws on failure
`ArchitectureValidationResult.ShouldPass()` SHALL throw `InvalidOperationException` with formatted diagnostic details when validation fails. It SHALL NOT throw when validation passes.

#### Scenario: ShouldPass with passing result
- **WHEN** `ShouldPass()` is called on a result where `Passed` is `true`
- **THEN** no exception SHALL be thrown

#### Scenario: ShouldPass with failing result
- **WHEN** `ShouldPass()` is called on a result where `Passed` is `false`
- **THEN** `InvalidOperationException` SHALL be thrown with messages containing violation and/or cycle details
