## 1. Identity qualification

- [x] 1.1 Add `"composition" => "call"` to `ArchitectureViolationIdentity.ResolveKind`.
- [x] 1.2 In `ArchitectureAnalysisSession.Composition.cs`, pass `sourceAssembly: actualAssemblyName, sourceMember: match.SourceMember, targetMember: matchedForbiddenApi` to `executionContext.IsIgnored`.

## 2. Tests

- [x] 2.1 Extend `ArchitectureBaselineCandidate`/`ArchitectureContractExecutionContext`-level or `CheckCompositionContract`-level test asserting a composition violation's collected baseline candidate carries `ContractFamily: "composition"`, `Kind: "call"`, non-null `SourceAssembly`, and the expected `SourceMember`/`TargetMember`.
- [x] 2.2 Add a same-named-type-in-different-assembly non-collision test at the `ArchitectureContractExecutionContext.IsIgnored` level (mirroring `IsIgnored_Version2Entry_SameNamedTypeDifferentAssembly_OnlyBaselinedAssemblySuppressed`), parameterized for `contractGroup: "composition"`/`"strict_composition"`, confirming a v2 baseline entry for one assembly's `Program` type does not suppress another assembly's same-named `Program` violation.
- [x] 2.3 Verify existing `CompositionContractTests` assertions (ordering, ignore suppression, audit mode, boundary matching) remain green unchanged.

## 3. Docs

- [x] 3.1 Add a short note to `docs/contracts/composition.md` under Semantics/Violations stating that violation/baseline identity is assembly- and member-qualified (same-named types in different assemblies, or distinct calls within one type, are never conflated), mirroring the equivalent note already present for dependency-style contracts.

## 4. Spec synchronization

- [x] 4.1 Archive this change into `openspec/specs/composition-contracts/spec.md`, adding the assembly-aware identity requirement/scenario.
