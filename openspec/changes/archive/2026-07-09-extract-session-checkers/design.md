## Context

`ArchitectureAnalysisSession` (`src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.cs` + 13 partials, ~4,000 lines) owns two things today: (1) per-run shared state — `Context`, `Document`, `Catalog`, `TypeIndex`, `ReferenceGraph`, `SelectedContractIds`, baseline candidates, unmatched-ignore tracking, coverage-inventory caching — and (2) the actual checking algorithm for all 24 registered contract families, each as a `Check*Contract` method living in a family-named partial file.

#211 (`contract-handler-execution`) already locked in the dispatch seam: `ArchitectureContractFamilyRegistry.All` holds one `ArchitectureContractFamilyDescriptor` per family, each with a `Checker` delegate of type `ArchitectureContractChecker(ArchitectureAnalysisSession session, IArchitectureContract contract) -> ArchitectureHandlerResult`, resolved through `ArchitectureContractHandlerRegistry`. Every descriptor's `Checker` today is a one-line lambda that casts the contract and calls straight back into `session.CheckXxxContract(...)`. That spec (`openspec/specs/contract-handler-execution/spec.md`) requires the delegate to keep receiving `ArchitectureAnalysisSession` as its context parameter — this change does not touch that outer seam, only what the lambda's target method does internally.

#212 (`configuration-contributor-registry`) established a parallel, narrower delegate (`ArchitectureConfigurationContributor`) for configuration-reference reporting, but it is still session-wide (`(session, collector, contract) => ...`) — it does not demonstrate a narrow-collaborator pattern we can copy wholesale; it demonstrates delegate-based decomposition, not dependency narrowing.

Three families are self-contained single-method algorithms whose only collaborators are already-public, immutable session properties (`Context.TargetAssemblies`, `Document`, `TypeIndex`) plus the session's shared cross-cutting gates (`IsContractSelected`, `IsDanglingButCoveredByRuleInputCoverage`, `CreateExecutionContext`): `assembly_independence`, `public_api_surface`, `inheritance`. None of the three shares a family-specific static/instance helper with any other family. These are the safe extraction candidates for this change.

A fourth candidate, `composition`, was investigated and dropped during implementation: its algorithm calls `IsAllowedLocation` and `ResolveProjectAssemblyNames`, two private helpers that physically live in `ArchitectureAnalysisSession.TypePlacement.cs` and are also called by `AttributeUsage.cs` and `InterfaceImplementation.cs`. Extracting `composition` cleanly would require either reaching back into the session for those two helpers (defeating the "no session dependency" goal) or relocating shared cross-family helpers out of an otherwise out-of-scope file — both are decisions that belong to the follow-up covering `AttributeUsage`/`InterfaceImplementation`/`TypePlacement`/`Composition` together, not this change.

## Goals / Non-Goals

**Goals:**
- Move the pure checking algorithm for the three families above out of `ArchitectureAnalysisSession` partials into standalone, directly-constructible classes.
- Give each extracted checker an explicit, narrow parameter list (contract + target assemblies/type index + execution context) instead of the whole session.
- Keep `ArchitectureAnalysisSession.Check*Contract` as the entry point the registry lambda calls, unchanged in signature, so `contract-handler-execution`'s spec (delegate receives the session) still holds.
- Prove the new checkers are unit-testable without spinning up a full session (at least one direct test).
- Document the resulting split of responsibility in `docs/internal/core-architecture-blueprint.md`.

**Non-Goals:**
- Extracting `ArchitectureAnalysisSession.Checking.cs` (11 distinct family algorithms), `.Coverage.cs`, or `.PolicyConsistency.cs` — each is large enough (600–800 lines) and either cross-cutting (`PolicyConsistency` runs once per validation, not per contract) or internally multi-algorithm (`Checking.cs`) to warrant its own scoped follow-up issue, per #213's own stated non-goals.
- Extracting families whose partials own a `ConfigurationContributor` closure or a cross-family static/instance helper (`AssemblyDependency`, `PackageDependency`, `AttributeUsage`, `InterfaceImplementation`, `TypePlacement`, `ProjectMetadata`, and `Composition` — see Context above) — these need the shared-helper-ownership question answered first (does the helper move with one checker, become its own shared service, or stay session-owned?), which is a separate design decision belonging to its own follow-up.
- Changing the `ArchitectureContractChecker` delegate signature, the registry, or any descriptor wiring.
- Introducing a new checker *interface* (e.g. `IArchitectureFamilyChecker`) — with only three instances and no shared method signature (the three checkers' `Check` methods differ in parameters), an interface would be speculative. Plain classes with a `Check` method are sufficient; a shared interface can be introduced once a follow-up extracts enough families to reveal a genuinely common shape.

## Decisions

**Decision: One class per extracted family, under `ArchLinterNet.Core.Execution.Checkers`, internal, non-static.**
Each becomes `internal sealed class AssemblyIndependenceChecker` / `PublicApiSurfaceChecker` / `InheritanceChecker`, with a single public instance method `Check(...)` taking the contract, whatever read-only inputs the algorithm needs (target assemblies, a pre-resolved assembly lookup, or the type index), and the already-constructed `ArchitectureContractExecutionContext`. Non-static so a future follow-up that gives two families a shared helper can introduce a constructor-injected collaborator without an API break.
- *Alternative considered*: static methods (`AssemblyIndependenceChecker.Check(...)`). Rejected because the issue explicitly asks for checkers to receive "explicit context and collaborators" — a class taking collaborators through its constructor is a strictly better shape for the *next* follow-up than a static method whose signature would have to grow.

**Decision: `ArchitectureAnalysisSession.Check*Contract` stays, shrinks to gates + context-creation + delegation + ignore-collection.**
```csharp
public List<ArchitectureViolation> CheckAssemblyIndependenceContract(ArchitectureAssemblyIndependenceContract contract)
{
    if (!IsContractSelected(contract.Id)) return new List<ArchitectureViolation>();
    ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
    List<ArchitectureViolation> violations = new AssemblyIndependenceChecker().Check(contract, Context.TargetAssemblies, executionContext);
    executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
    return violations;
}
```
`public_api_surface` and `inheritance` additionally keep their existing `IsDanglingButCoveredByRuleInputCoverage(contract)` gate on the session (it reads `_ruleInputCoveredContractIdsForMode` and `Document.Layers`, both session-owned run state). This keeps `IsContractSelected`, the coverage-deferral gate, `CreateExecutionContext`, and `_unmatchedIgnoredViolations` — all genuinely shared, session-owned concerns — on the session, exactly matching the issue's target model ("session owns run state; checkers own behavior"). The registry lambda in `ArchitectureContractFamilyRegistry.cs` is untouched.
- *Alternative considered*: move the gates and `CreateExecutionContext` call into the checker too, so the session wrapper is a one-liner. Rejected: this infrastructure is used identically by most of the 24 families (not just these three); moving it into 3 of 24 checkers now would create two competing patterns for the other 21 families to eventually follow, which is exactly the kind of premature-abstraction risk the issue's non-goals warn against.

**Decision: Add a `family-checker-extraction` capability spec, matching repo precedent.**
The archived `cli-command-dispatch` capability (#225, `extract-cli-commands`) shows this repo's convention: even a purely internal, behavior-preserving reorganization gets a capability spec documenting the new structural rule, not just an empty Capabilities section. This change follows that precedent with `family-checker-extraction`, scoped explicitly to the three families it covers.

**Decision: Test the new checkers directly, keep existing session-level tests as regression coverage.**
`AssemblyIndependenceContractTests.cs`, `PublicApiSurfaceContractTests.cs`, and `InheritanceContractTests.cs` already exercise `ArchitectureAnalysisSession.Check*Contract` end-to-end via `ArchitectureContractRunner` — these stay unmodified and must keep passing, proving behavior preservation. A new test file constructs a checker class directly with a hand-built `ArchitectureContractExecutionContext` (its constructor takes only primitives/lists — no session needed) to satisfy the issue's "checker exercised with a minimal/fake session context or collaborator set" acceptance criterion.

## Risks / Trade-offs

- **[Risk]** Moving code between files could silently change behavior if a helper method or private field access is missed. → **Mitigation**: move method bodies verbatim (no logic changes), keep the three families' existing end-to-end tests unmodified as a regression check, run full suite (`make acceptance`) before commit.
- **[Risk]** Reviewers might expect the whole 4,000-line session to shrink meaningfully in this PR. → **Mitigation**: proposal and PR description state the scope explicitly (3 of 24 families) and name the remaining files as tracked follow-up work, consistent with issue #213's own "Non-goals: Rewriting every algorithm if some are better handled by deliberately scoped follow-up issues."
- **[Trade-off]** Three small classes with no shared interface is slightly more boilerplate than one static helper class, but keeps each family's future evolution independent and avoids guessing at a shared shape from only three data points.

## Open Questions

None — scope, shape, and test strategy are settled for this change; remaining families (including `composition`, dropped during implementation) are explicitly deferred, not undecided.
