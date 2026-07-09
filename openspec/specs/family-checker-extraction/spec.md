# family-checker-extraction Specification

## Purpose
TBD - created by archiving change extract-session-checkers. Update Purpose after archive.
## Requirements
### Requirement: Extracted families check through a standalone checker class
For the `assembly_independence`, `public_api_surface`, and `inheritance` contract families, the checking algorithm SHALL live in a standalone, non-static class under `ArchLinterNet.Core.Execution.Checkers` (`AssemblyIndependenceChecker`, `PublicApiSurfaceChecker`, `InheritanceChecker`), exposing a `Check` method whose parameters are the contract plus the specific read-only inputs that family's algorithm needs (e.g. target assemblies, a resolved assembly lookup, or the type index) and an `ArchitectureContractExecutionContext`. These classes SHALL NOT take an `ArchitectureAnalysisSession` parameter.

#### Scenario: Checker class is constructible without a session
- **WHEN** a caller constructs `new AssemblyIndependenceChecker()` (or the equivalent for `public_api_surface`, `inheritance`) and calls `Check` with a contract, the relevant assembly/type inputs, and a directly-constructed `ArchitectureContractExecutionContext`
- **THEN** it SHALL return the violations for that contract without requiring an `ArchitectureAnalysisSession`, `ArchitectureAnalysisContext`, or `ArchitectureContractDocument` to exist

### Requirement: Session wrapper for extracted families retains only shared run-state concerns
For each of the three extracted families, `ArchitectureAnalysisSession.Check*Contract` SHALL perform only: the `IsContractSelected` gate, the `IsDanglingButCoveredByRuleInputCoverage` deferral check where the family already had one, `ArchitectureContractExecutionContext` creation via `CreateExecutionContext`, delegation to the family's checker class, and collection of unmatched ignores into the session's `_unmatchedIgnoredViolations` list. It SHALL NOT contain the family's violation-detection algorithm inline.

#### Scenario: Session method output is unchanged for a selected contract
- **WHEN** `ArchitectureAnalysisSession.CheckAssemblyIndependenceContract` (or the equivalent for `public_api_surface`, `inheritance`) is called with a selected contract
- **THEN** it SHALL return the same violations, in the same order, that the pre-extraction inline implementation returned for the same contract and session state

#### Scenario: Unselected contract still short-circuits before checker construction
- **WHEN** `ArchitectureAnalysisSession.CheckAssemblyIndependenceContract` (or the equivalent for the other two families) is called with a contract whose id is not in `SelectedContractIds`
- **THEN** it SHALL return an empty violation list and SHALL NOT construct the family's checker class or an `ArchitectureContractExecutionContext`

### Requirement: Registry dispatch for extracted families is unchanged
`ArchitectureContractFamilyRegistry.All`'s descriptor entries for `assembly_independence`, `public_api_surface`, and `inheritance` SHALL continue to resolve their `Checker` delegate to a call into the corresponding `ArchitectureAnalysisSession.Check*Contract` method, receiving the session as before.

#### Scenario: Registry lambda signature is unchanged
- **WHEN** `ArchitectureContractHandlerRegistry.Execute` dispatches an `assembly_independence` contract
- **THEN** the resolved `ArchitectureContractChecker` delegate SHALL still receive the `ArchitectureAnalysisSession` and the contract, per `contract-handler-execution`, and SHALL NOT receive an `AssemblyIndependenceChecker` instance directly

