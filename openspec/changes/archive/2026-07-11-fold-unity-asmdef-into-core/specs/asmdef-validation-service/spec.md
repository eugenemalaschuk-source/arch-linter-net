## MODIFIED Requirements

### Requirement: Core exposes the asmdef convenience facade
`ArchLinterNet.Core.Asmdef.AsmdefValidator` SHALL expose `Validate(string policyPath)` and `Validate(string policyPath, out IReadOnlyCollection<ArchitectureViolation> violations)` and SHALL delegate to a lazily constructed default `ArchitectureEngine`. Callers SHALL require only `ArchLinterNet.Core`; no separate Unity assembly or package SHALL be required.

#### Scenario: Existing asmdef-only validation behavior is available from Core
- **WHEN** a caller references `ArchLinterNet.Core` and invokes `AsmdefValidator.Validate` for a policy containing `strict_asmdef` contracts
- **THEN** the return value and violations SHALL match `ArchitectureEngine.ValidateAsmdef` for the same policy
- **AND** `audit_asmdef` contracts SHALL remain excluded from this asmdef-only facade
