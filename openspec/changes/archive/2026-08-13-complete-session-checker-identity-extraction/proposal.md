## Why

The post-v0.4.0 cleanup (#211, #212, #213, #229, #408) extracted checker infrastructure and session contributors, but #229 deliberately left residual family behavior inside `ArchitectureAnalysisSession`'s partials. Only three families ever moved into `ArchLinterNet.Core.Execution.Checkers`.

v0.6.0 showed that residual ownership is still an extension hotspot: #395, #414 and #420 each had to change session family partials, with `ArchitectureAnalysisSession.Checking.cs` at roughly 755 lines. Adding a contract family still meant writing family checking into the shared session-state object, and canonical finding-identity attribution could only be exercised by driving a full mutable session.

This closes issue #452 (child of story #450). It is behavior-preserving; performance/scale evolution stays with #19.

## What Changes

- Every remaining contract-family checking algorithm moves out of `ArchitectureAnalysisSession` into a focused component under `ArchLinterNet.Core.Execution.Checkers`, using the existing descriptor/registry dispatch model — no second dispatch mechanism is introduced.
- A new internal `ArchitectureCheckerContext` is the single, narrow port through which a checker reaches run facts (document, analysis context, type/role/source-fact indexes, expression facts, reference graph, layer and project resolution) and records session-owned participation state. Checkers never receive `ArchitectureAnalysisSession`.
- Each `ArchitectureAnalysisSession.Check*Contract` method becomes a lifecycle wrapper only: contract selection, rule-input-coverage deferral, execution-context creation, delegation, unmatched-ignore collection, and — for `cycle` — baseline-candidate publication.
- Canonical finding-identity attribution moves from `ArchitectureAnalysisSession.FindingIdentities.cs` into `ArchitectureFindingIdentityAttributor`, a pure function of (candidate log, cursor, violations). The session keeps the candidate log and the cursor; the algorithm becomes testable without a session lifecycle.
- Self-policy tests enforce the boundary: every family entry point must stay a thin wrapper delegating to a checker, checkers must not name `ArchitectureAnalysisSession`, and identity attribution must stay in its dedicated component.
- Coverage, policy-consistency, contextual-consumer registration, classification facts, the framework MSBuild evaluation cache and all session lifecycle/cache-facing state stay where they are — they are not contract-family checking.

Behavior is unchanged: strict/audit findings, canonical identities, candidate ordering and tie-breaking, ignored-violation and baseline-visible semantics, cache hit/miss behavior, cancellation boundaries, checker ordering and contract selection are all preserved, and no public API surface is added or removed.

## Capabilities

### New Capabilities

- `finding-identity-attribution`: canonical finding-identity attribution as an owned, independently testable component with an explicit input/output contract and an explicit cancellation guarantee.

### Modified Capabilities

- `family-checker-extraction`: generalized from three named families to every contract family, with `ArchitectureCheckerContext` as the sanctioned fact-access port and a self-policy guarantee that a new family cannot reintroduce session-owned checking.

`contract-handler-execution` is unchanged: the descriptor `Checker` delegate still receives the `ArchitectureAnalysisSession` and the contract, and dispatch order still comes from `ArchitectureContractFamilyRegistry.All`.

## Impact

- **Code**: new `src/ArchLinterNet.Core/Execution/ArchitectureCheckerContext.cs`, `ArchitectureAnalysisSession.FactAccess.cs`, `ArchitectureFindingIdentityAttributor.cs`, `src/ArchLinterNet.Core/Discovery/ProjectPathNormalizer.cs`, and 20 checker files under `src/ArchLinterNet.Core/Execution/Checkers/`; every family partial of `ArchitectureAnalysisSession` shrinks to a lifecycle wrapper; `ArchitectureAnalysisSession.LayoutMatching.cs` is removed (its content became `LayoutConventionChecker.Matching.cs`).
- **Tests**: new `ArchitectureFindingIdentityAttributorTests` (identity attribution without a session) and `ArchitectureAnalysisSessionCheckerOwnershipTests` (the self-policy boundary). Existing suites are unchanged and act as the behavior-equivalence evidence.
- **Schema / docs / policy**: none. No contract semantics, no configuration surface and no public API change.
- **No breaking changes.**
