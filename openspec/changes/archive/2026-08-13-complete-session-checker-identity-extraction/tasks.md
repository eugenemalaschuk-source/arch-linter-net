## 1. Checker access port

- [x] 1.1 Add internal `ArchitectureCheckerContext` (`src/ArchLinterNet.Core/Execution/ArchitectureCheckerContext.cs`) forwarding only the fact/index access and recording ports family checkers need.
- [x] 1.2 Move the session's layer/type/assembly/project/contextual fact-access helpers into `ArchitectureAnalysisSession.FactAccess.cs` and make them `internal` so only the context can forward them.
- [x] 1.3 Expose one lazily-created `CheckerContext` per session.

## 2. Extract remaining family checking

- [x] 2.1 `ArchitectureAnalysisSession.Checking.cs`: extract `dependency`, `layer`/`layer_template`, `allow_only`, `cycle`, `acyclic_sibling`, `method_body`, `asmdef`, `independence`, `external`, `external_allow_only` into `DependencyChecker`, `LayerChecker`, `AllowOnlyChecker`, `CycleChecker`, `AcyclicSiblingChecker`, `MethodBodyChecker`, `AsmdefChecker`, `LayerIndependenceChecker`, `ExternalDependencyChecker`.
- [x] 2.2 Extract the contextual families into `ContextDependencyChecker` / `ContextAllowOnlyChecker`, with `ContextualCheckerSupport` for the selector description and `when`-participation helpers shared with port-boundary.
- [x] 2.3 Extract `port_boundary` into `PortBoundaryChecker`.
- [x] 2.4 Extract `type_placement`, `attribute_usage`, `interface_implementation` and `composition`, with the shared declared-location test in `CheckerLocationAllowance`.
- [x] 2.5 Extract `protected`, `project_metadata`, the package families and the assembly families (with `AssemblyDependencyDepthGuard` for the shared transitive-depth guard).
- [x] 2.6 Extract the framework families into `FrameworkReferenceChecker`, leaving the session-cached MSBuild evaluation in place because `CheckConfiguration` shares it.
- [x] 2.7 Extract `layout_conventions` into `LayoutConventionChecker` (+ `.Matching.cs`), moving `LayoutExclusionTracker` with it and deleting `ArchitectureAnalysisSession.LayoutMatching.cs`.
- [x] 2.8 Reduce every `ArchitectureAnalysisSession.Check*Contract` method to a lifecycle wrapper.
- [x] 2.9 Preserve the three ordering/lifecycle quirks explicitly via `LayerChecker.Result`, `CycleChecker.Result` and `LayoutConventionChecker.Result` (see design Decision 3).
- [x] 2.10 Replace `ArchitectureAnalysisSession.NormalizeProjectPath` with `ProjectPathNormalizer.Normalize` so no checker names the session.

## 3. Finding-identity attribution

- [x] 3.1 Move the attribution algorithm into `ArchitectureFindingIdentityAttributor.Attach(candidateLog, cursor, violations)`.
- [x] 3.2 Reduce `ArchitectureAnalysisSession.AttachFindingIdentities` to a delegation, keeping the candidate log and cursor session-owned.
- [x] 3.3 Confirm the executor's per-contract call site is unchanged, so cancellation still cannot expose partially attributed findings.

## 4. Governance

- [x] 4.1 Add `ArchitectureAnalysisSessionCheckerOwnershipTests`: thin-wrapper + delegation rule, checkers-never-name-the-session rule, identity-attribution-ownership rule.
- [x] 4.2 Record `CheckCoverageContract` as the single documented session-owned entry point, with its reason inline.

## 5. Tests and validation

- [x] 5.1 Add `ArchitectureFindingIdentityAttributorTests` exercising attribution with no session: per-reference selection order, single consumption, cursor bracketing, composition payload behavior, suffixed-reference matching, no-match passthrough.
- [x] 5.2 Confirm the Core public-API approval test still matches the approved baseline byte-for-byte.
- [x] 5.3 Run the Core, CEL and CLI suites plus `make lint` and `openspec validate --all --strict`.
