# family-checker-extraction Specification

## Purpose
Defines the ownership boundary between `ArchitectureAnalysisSession` and contract-family checking:
every family's checking algorithm lives in a focused component under
`ArchLinterNet.Core.Execution.Checkers`, reaching run facts only through the narrow
`ArchitectureCheckerContext` port (or that family's specific primitives), while the session keeps
lifecycle, immutable/request-scoped state, fact/index access, cache-facing state and deterministic
coordination. Registry dispatch, finding order and contract selection are unaffected by where the
checking is written, and self-policy tests make reintroducing session-owned family checking a test
failure rather than a review question.
## Requirements
### Requirement: Extracted families check through a standalone checker class

Every contract family's checking algorithm SHALL live in a focused component under `ArchLinterNet.Core.Execution.Checkers`, exposing a `Check` method (or, where one component serves a family and its allow-only counterpart, a `Check` and a `CheckAllowOnly` method) whose parameters are the contract, the read-only inputs that family's algorithm needs, and an `ArchitectureContractExecutionContext`. Those inputs SHALL be either the specific primitives the family needs (e.g. target assemblies, a resolved assembly lookup, or the type index) or the narrow `ArchitectureCheckerContext` fact-access port. No checker component SHALL take an `ArchitectureAnalysisSession` parameter or otherwise reference `ArchitectureAnalysisSession`.

The `coverage` family is the single exception: it reports a policy inventory over the whole document rather than checking declared types against a contract, shares the session's cached coverage inventory with `CheckConfiguration`, and remains session-owned.

#### Scenario: Checker class is constructible without a session

- **WHEN** a caller invokes the checker for `assembly_independence`, `public_api_surface` or `inheritance` with a contract, the relevant assembly/type inputs, and a directly-constructed `ArchitectureContractExecutionContext`
- **THEN** it SHALL return the violations for that contract without requiring an `ArchitectureAnalysisSession`, `ArchitectureAnalysisContext`, or `ArchitectureContractDocument` to exist

#### Scenario: Port-based checker is invocable without a session type

- **WHEN** a caller invokes any other family's checker with a contract, an `ArchitectureCheckerContext`, and a directly-constructed `ArchitectureContractExecutionContext`
- **THEN** it SHALL return that contract's findings, taking no `ArchitectureAnalysisSession` parameter

#### Scenario: Checker sources do not name the session

- **WHEN** the self-policy suite inspects every source file under `src/ArchLinterNet.Core/Execution/Checkers`
- **THEN** none of them SHALL reference `ArchitectureAnalysisSession`, so family code reaches run facts only through the sanctioned port

### Requirement: Session wrapper for extracted families retains only shared run-state concerns

For every contract family other than `coverage`, `ArchitectureAnalysisSession.Check*Contract` SHALL perform only: the `IsContractSelected` gate, the `IsDanglingButCoveredByRuleInputCoverage` deferral check where the family already had one, any pre-existing precondition guard the family already enforced (such as the assembly families' direct-dependency-depth guard), `ArchitectureContractExecutionContext` creation via `CreateExecutionContext`, delegation to the family's checker component, collection of unmatched ignores into the session's `_unmatchedIgnoredViolations` list, and publication of session-owned run state the checker deliberately does not publish itself (baseline candidates for `cycle`). It SHALL NOT contain the family's violation-detection algorithm inline, at any nesting depth, and SHALL NOT reach that algorithm indirectly: the only session-declared methods it may call are the lifecycle and fact-access members named above.

#### Scenario: Session method output is unchanged for a selected contract

- **WHEN** any `ArchitectureAnalysisSession.Check*Contract` method is called with a selected contract
- **THEN** it SHALL return the same findings, in the same order, that the pre-extraction inline implementation returned for the same contract and session state

#### Scenario: Unselected contract still short-circuits before checker construction

- **WHEN** a `Check*Contract` method is called with a contract whose id is not in `SelectedContractIds`
- **THEN** it SHALL return an empty result and SHALL NOT invoke the family's checker or create an `ArchitectureContractExecutionContext`

#### Scenario: Self-policy rejects reintroduced session-owned checking

- **WHEN** the self-policy suite parses each public `Check*Contract` method declared in an `ArchitectureAnalysisSession` partial
- **THEN** every method other than `CheckCoverageContract` SHALL contain at most ten statements counting every nested statement, and SHALL delegate to a `*Checker` component, so family logic written inline — including inside a nested block, loop or lambda — fails the suite

#### Scenario: Self-policy rejects family logic hidden behind a session helper

- **WHEN** a family's algorithm is moved into a new private `ArchitectureAnalysisSession` method that the family's entry point calls, leaving the entry point small and still delegating to a `*Checker`
- **THEN** the self-policy suite SHALL fail, because an entry point may call no session-declared method outside the named lifecycle and fact-access allowlist

#### Scenario: Self-policy rejects dispatch that bypasses the governed entry points

- **WHEN** a contract family's `ArchitectureContractFamilyDescriptor` dispatches into a session method that is not named `Check*Contract`
- **THEN** the self-policy suite SHALL fail, so no family can be routed to a session method the ownership rules never inspect

### Requirement: Registry dispatch for extracted families is unchanged

`ArchitectureContractFamilyRegistry.All`'s descriptor entries SHALL continue to resolve each family's `Checker` delegate to a call into the corresponding `ArchitectureAnalysisSession.Check*Contract` method, receiving the session as before. Extraction SHALL NOT introduce a second dispatch model, change family order, or change contract selection semantics.

#### Scenario: Registry lambda signature is unchanged

- **WHEN** `ArchitectureContractHandlerRegistry.Execute` dispatches a contract of any family
- **THEN** the resolved `ArchitectureContractChecker` delegate SHALL still receive the `ArchitectureAnalysisSession` and the contract, per `contract-handler-execution`, and SHALL NOT receive a checker component directly

### Requirement: Checkers reach run facts through a narrow context port

`ArchLinterNet.Core.Execution.ArchitectureCheckerContext` SHALL be the only object through which a contract-family checker reads session-owned run facts or records session-owned participation state. It SHALL expose the contract document, the analysis context, the type/role/source-file-fact indexes, expression facts, the reference graph, preprocessor symbols, the resolved build configuration, layer and containing-layer resolution, assembly and project-assembly lookups, contextual-selector matching, framework-reference resolution, and the subtractive-matcher participation recording port. It SHALL NOT expose contract selection, execution-context creation, unmatched-ignore collection, coverage, policy-consistency or cache-facing state, and it SHALL hold no state of its own beyond the session it forwards to.

#### Scenario: Context forwards rather than duplicates

- **WHEN** a checker reads a fact or records participation through `ArchitectureCheckerContext`
- **THEN** the result SHALL be identical to reading or recording against the session directly, including caching and mutation ordering, because the context stores no state and every member forwards to exactly one session member

#### Scenario: Lifecycle concerns are unreachable from a checker

- **WHEN** a contract-family checker is written against `ArchitectureCheckerContext`
- **THEN** it SHALL have no access to contract selection, execution-context creation, unmatched-ignore collection, the coverage inventory, or policy-consistency state, so a new family cannot take on lifecycle responsibilities by accident

### Requirement: Extraction preserves finding, ordering and lifecycle equivalence

Moving family checking out of `ArchitectureAnalysisSession` SHALL NOT change strict or audit findings, canonical identities, finding order, cycle order, lifecycle status, cache hit/miss behavior, publication rules, cancellation boundaries, or the public API surface of any assembly, for unchanged inputs.

#### Scenario: Public API surface is unchanged

- **WHEN** the Core public-API approval test compares the assembly's public surface against the approved baseline
- **THEN** it SHALL match byte-for-byte, so extraction adds no public API to facilitate itself

#### Scenario: Ordering quirks are preserved rather than normalized

- **WHEN** a `layer` contract declares `exhaustive` with a container namespace, or a `layout_conventions` contract runs against a document with no source-enriched declared-type facts
- **THEN** exhaustive-sibling findings SHALL still be appended after unmatched-ignore collection, and the data-unavailable path SHALL still report no unmatched ignores for that contract, exactly as before extraction

