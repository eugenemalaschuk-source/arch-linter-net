## Context

External dependency contracts (`ArchitectureExternalDependencyContract`) detect forbidden vendor/framework references by scanning type-level metadata via `ArchitectureReferenceScanner.GetReferencedTypes()`. This covers interfaces, base types, fields, properties, parameters, return types, and generic arguments — but NOT references that appear only inside method bodies.

The IL scanner (`ArchitectureIlMethodBodyScanner`) already knows how to read IL bytearrays, decode opcodes, resolve metadata tokens to `MemberInfo`, and enumerate methods/constructors on source types. However, it currently uses `ArchitectureForbiddenCallMatcher` (string-based pattern matching) rather than `ArchitectureExternalDependencyResolver.MatchesGroup` (namespace/type prefix matching).

The goal is to connect these two capabilities: reuse IL reading to extract `MemberInfo` references from method bodies, then apply external dependency group matching rules.

## Goals / Non-Goals

**Goals:**
- Detect forbidden external dependency references inside method bodies (IL-level)
- Reuse existing `ArchitectureExternalDependencyResolver.MatchesGroup` for namespace/type prefix matching
- Emit diagnostics that include source type, source member, forbidden group, and referenced external member
- Keep strict and audit behavior consistent with existing external dependency contracts
- No YAML schema changes — existing `strict_external`/`audit_external` contracts gain method-body scanning automatically

**Non-Goals:**
- Semantic data-flow or taint analysis
- Runtime dependency injection resolution validation
- Security/authorization correctness validation
- Static analysis of third-party package internals
- Roslyn-based method body scanning for external deps (IL is sufficient and simpler)
- New YAML contract types (extending existing contracts is cleaner)

## Decisions

### Decision 1: Extend IL scanner path, not Roslyn

**Choice**: Use IL-based method body scanning for external dependency detection.

**Rationale**: The IL scanner already resolves metadata tokens to `MemberInfo` objects. `MemberInfo` carries full type information that can be fed directly into `ArchitectureExternalDependencyResolver.MatchesGroup`. Roslyn-based scanning would require building a compilation, which is heavier and not needed here — IL is sufficient for detecting type references.

**Alternative considered**: Build a Roslyn-based scanner for external deps in method bodies. Rejected because it would duplicate compilation work already done by method-body contracts and add unnecessary complexity.

### Decision 2: Add new method `FindMethodBodyExternalViolations` to the violation finder

**Choice**: Create a new static method in `ArchitectureExternalDependencyViolationFinder` (or a sibling class) that scans IL method bodies of source types and checks each resolved `MemberInfo`'s declaring type against external dependency groups.

**Rationale**: Keeps the external dependency detection logic co-located. The method takes the same `ArchitectureExternalDependencyGroup` and source types already resolved by `CheckExternalContract`.

**Alternative considered**: Create a entirely new scanner class. Rejected to avoid proliferation of scanner classes when the logic is a natural extension of the existing violation finder.

### Decision 3: Extend `CheckExternalContract` to call method body scanning

**Choice**: In `ArchitectureContractRunner.Checking.cs`, after the existing type-level scan in `CheckExternalContract`, also invoke the new method body external dependency scanner for the same source types.

**Rationale**: Minimal change to the runner — just an additional scan pass per contract. No new contract types, no schema changes.

### Decision 4: Diagnostic format includes member context

**Choice**: Method-body external dep violations include the declaring type AND the method/constructor name in the source member field, formatted as `TypeName.MethodName`.

**Rationale**: Users need to know which specific method contains the violation to fix it. The IL scanner already has access to `MethodBase.DeclaringType` and `MethodBase.Name`.

### Decision 5: No new YAML schema fields

**Choice**: Existing `strict_external`/`audit_external` contracts automatically gain method-body scanning. No opt-in flag needed.

**Rationale**: The feature is a strict improvement — if a reference was previously undetected, detecting it is always desired. Adding an opt-out flag adds complexity without clear value. If users need to suppress specific method-body violations, the existing `ignored_violations` mechanism handles that.

## Risks / Trade-offs

- **[Risk] False positives on dynamically resolved types** → Mitigated by only resolving metadata tokens that the IL scanner can successfully resolve (existing `ResolveReferencedMember` pattern with try/catch).
- **[Risk] Performance impact of scanning IL for every external contract** → Mitigated by the fact that IL scanning is already fast (method body bytearrays are small) and source types are already resolved per-contract. The scan is bounded by `sourceTypes × methods × tokens`.
- **[Trade-off] No Roslyn source-location in diagnostics** → IL-level violations won't include file paths or line numbers (only method names). This is acceptable because the primary use case is architecture governance, not IDE-level code fixes. The method name is sufficient to locate the issue.
- **[Risk] Backward compatibility** → Policies that previously passed strict validation may now fail if they have undetected method-body violations. This is the intended behavior improvement, not a regression. Document in release notes.
