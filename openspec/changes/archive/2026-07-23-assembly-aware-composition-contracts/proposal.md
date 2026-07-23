## Why

Issue #360: `strict_composition`/`audit_composition` contracts do not thread source-assembly information into violation identity. `CheckCompositionContract` already computes each candidate type's assembly name to test the composition boundary, but discards it afterward — the reported `sourceType` is an unqualified type name, and `IsIgnored` is called with no `sourceAssembly`/`sourceMember`/`targetMember`. Two same-named types in different assemblies (e.g. two `Program` types) therefore collide in baseline identity: baselining one silently suppresses the other. This is the same collision class #357/#381 fixed for dependency-style and method-body contracts, explicitly left out of scope there for composition.

## What Changes

- Thread the composition check's already-computed `actualAssemblyName` into `ArchitectureContractExecutionContext.IsIgnored` as `sourceAssembly`, and also pass the matched call's `SourceMember` and the matched forbidden API as `sourceMember`/`targetMember`, so composition violations get a fully qualified `ArchitectureViolationIdentity` instead of falling back to `(contract family, contract id, source_type, target_type)`.
- Classify the `composition` contract family's `Kind` as `call` in `ArchitectureViolationIdentity.ResolveKind` (it detects static call-site usage, the same shape as `method_body`), instead of falling through to the generic `reference` default.
- No change to the existing `allowed_only_in_*` boundary-matching logic, forbidden-API pattern matching, or diagnostic payload shape — this only qualifies baseline/ignore identity, matching the mechanism already proven generic for other families.

## Capabilities

### Modified Capabilities

- `composition-contracts`: violation/baseline identity for `strict_composition`/`audit_composition` becomes assembly- and member-qualified, so same-named types across assemblies and distinct call occurrences within one type are never conflated by baselining or `ignored_violations` matching.

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.Composition.cs` (`CheckCompositionContract`).
- `src/ArchLinterNet.Core/Model/ArchitectureViolationIdentity.cs` (`ResolveKind`).
- `tests/ArchLinterNet.Core.Tests/CompositionContractTests.cs` and/or a new focused test file covering assembly-qualified identity and same-named-type-in-different-assembly non-collision.
- `docs/contracts/composition.md` (note assembly-aware identity, mirroring the dependency-contracts baseline note).
