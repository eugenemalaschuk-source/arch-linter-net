## MODIFIED Requirements

### Requirement: Narrow asmdef application service composes loader, resolver, and scanner
`ArchLinterNet.Core.Asmdef.Abstractions.IAsmdefValidationService` SHALL expose `AsmdefValidationOutcome Validate(AsmdefValidationRequest request)`. Its default implementation, `ArchLinterNet.Core.Asmdef.AsmdefValidationService`, SHALL load the policy document at `AsmdefValidationRequest.PolicyPath`, resolve the repository root from that path, and run `strict_asmdef` contract scanning against the resolved root through its constructor-injected `ArchLinterNet.Core.Scanning.Abstractions.IArchitectureAsmdefScanner` collaborator, returning the aggregated violations.

#### Scenario: Validate wraps load, resolve, and scan
- **WHEN** `AsmdefValidationService.Validate` is called with a `AsmdefValidationRequest.PolicyPath` pointing at a valid policy containing `strict_asmdef` contracts
- **THEN** the service SHALL load the document, resolve the repository root from the policy path, and return an `AsmdefValidationOutcome` whose `Violations` contains every violation found across all `strict_asmdef` contracts

#### Scenario: Passed reflects an empty violation set
- **WHEN** `AsmdefValidationService.Validate` runs against a policy whose `strict_asmdef` contracts produce no violations
- **THEN** `AsmdefValidationOutcome.Passed` SHALL be `true`
