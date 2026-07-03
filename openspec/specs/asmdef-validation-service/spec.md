# asmdef-validation-service Specification

## Purpose
Defines the narrow Core application service used by the Unity-facing adapter for asmdef-only validation. The service preserves Unity's existing `strict_asmdef` behavior while moving policy loading, repository-root resolution, and asmdef scanner orchestration behind the composed Core seam.

## Requirements
### Requirement: Core exposes a narrow asmdef validation application service
Core SHALL expose a composed `IAsmdefValidationService` application service for Unity-facing asmdef-only validation. The service SHALL load the architecture contract document, resolve the repository root from the policy path, and execute `strict_asmdef` contracts through the Core asmdef scanner.

#### Scenario: Strict asmdef contracts are evaluated through Core
- **WHEN** an `AsmdefValidationRequest` points to a policy containing `strict_asmdef` contracts
- **THEN** `IAsmdefValidationService` SHALL return an `AsmdefValidationOutcome` whose violations come from evaluating those strict asmdef contracts against the repository's asmdef files

### Requirement: Unity adapter remains a thin compatibility facade
`ArchLinterNet.Unity.AsmdefValidator` SHALL keep its existing `Validate(string)` and `Validate(string, out IReadOnlyCollection<ArchitectureViolation>)` signatures, but SHALL delegate validation to the composed Core asmdef validation seam. It SHALL NOT directly call policy document loaders, repository-root resolvers, asmdef scanners, execution internals, or container APIs.

#### Scenario: Unity facade delegates to Core asmdef service
- **WHEN** Unity code calls `new AsmdefValidator().Validate(policyPath, out violations)`
- **THEN** the adapter SHALL return the pass/fail result and violation collection produced by the Core asmdef validation service without duplicating policy loading, root resolution, or scanner orchestration

### Requirement: Unity asmdef path does not invent full validation semantics
The asmdef service SHALL preserve Unity's existing asmdef-only behavior and SHALL NOT add validation mode, baseline, condition-set, selected-contract, or full validation-output semantics.

#### Scenario: Audit asmdef contracts are not evaluated by Unity seam
- **WHEN** a policy contains `audit_asmdef` contracts
- **THEN** the Unity-facing asmdef service SHALL ignore them, matching the pre-existing Unity adapter behavior that evaluates only `strict_asmdef`
