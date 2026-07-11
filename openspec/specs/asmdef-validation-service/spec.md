# asmdef-validation-service Specification

## Purpose
Defines the narrow Core application service and convenience API that compose policy loading, repository-root resolution, and `strict_asmdef` contract scanning for asmdef-only callers without requiring a separate Unity package or the full CLI/Testing validation seam.
## Requirements
### Requirement: Narrow asmdef application service composes loader, resolver, and scanner
`ArchLinterNet.Core.Asmdef.Abstractions.IAsmdefValidationService` SHALL expose `AsmdefValidationOutcome Validate(AsmdefValidationRequest request)`. Its default implementation, `ArchLinterNet.Core.Asmdef.AsmdefValidationService`, SHALL load the policy document at `AsmdefValidationRequest.PolicyPath`, resolve the repository root from that path, and run `strict_asmdef` contract scanning against the resolved root through its constructor-injected `ArchLinterNet.Core.Scanning.Abstractions.IArchitectureAsmdefScanner` collaborator, returning the aggregated violations.

#### Scenario: Validate wraps load, resolve, and scan
- **WHEN** `AsmdefValidationService.Validate` is called with a `AsmdefValidationRequest.PolicyPath` pointing at a valid policy containing `strict_asmdef` contracts
- **THEN** the service SHALL load the document, resolve the repository root from the policy path, and return an `AsmdefValidationOutcome` whose `Violations` contains every violation found across all `strict_asmdef` contracts

#### Scenario: Passed reflects an empty violation set
- **WHEN** `AsmdefValidationService.Validate` runs against a policy whose `strict_asmdef` contracts produce no violations
- **THEN** `AsmdefValidationOutcome.Passed` SHALL be `true`

### Requirement: AsmdefValidationService is composed through the Core composition root
`AddArchLinterNetCore()` SHALL register `IAsmdefValidationService` → `AsmdefValidationService` as a singleton, and `ArchitectureEngine` SHALL expose `AsmdefValidationOutcome ValidateAsmdef(AsmdefValidationRequest request)` that resolves and invokes it, so narrow asmdef-only callers never need to resolve services from a container directly.

#### Scenario: Engine resolves the asmdef service
- **WHEN** `new ArchitectureEngineBuilder().AddArchLinterNetCore().Build()` is called and `ValidateAsmdef` is invoked on the result
- **THEN** the returned `AsmdefValidationOutcome` SHALL equal what a directly-constructed `AsmdefValidationService` (given the same collaborators) returns for the same request

### Requirement: Core exposes the asmdef convenience facade
`ArchLinterNet.Core.Asmdef.AsmdefValidator` SHALL expose the existing `Validate(string policyPath)` and `Validate(string policyPath, out IReadOnlyCollection<ArchitectureViolation> violations)` convenience signatures and SHALL delegate to a lazily constructed default `ArchitectureEngine`. The facade SHALL live in `ArchLinterNet.Core`; callers SHALL NOT need an `ArchLinterNet.Unity` package or assembly.

#### Scenario: Existing asmdef-only validation behavior is available from Core
- **WHEN** a caller references `ArchLinterNet.Core` and invokes `AsmdefValidator.Validate` for a policy containing `strict_asmdef` contracts
- **THEN** the return value and violations SHALL match `ArchitectureEngine.ValidateAsmdef` for the same policy
- **AND** `audit_asmdef` contracts SHALL remain excluded from this asmdef-only facade
