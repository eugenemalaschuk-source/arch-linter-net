## Context

Current cycle contracts (`strict_cycles`/`audit_cycles`) require policy authors to explicitly enumerate layers. For broad architecture rules — e.g., "all sibling namespaces under `MyApp.Features` must be acyclic" — this means listing every sibling and updating the list as new siblings are added. Acyclic sibling contracts automate discovery of sibling namespaces from loaded types, reducing policy maintenance and catching cycles in newly added modules automatically.

Existing relevant infrastructure:
- `ArchitectureCycleDetector` — DFS-based cycle detection for `Dictionary<string, HashSet<string>>` graphs
- `ArchitectureCycleContract` — layer-based cycle contract model
- `ArchitectureContractRunner.CheckCycleContract` — builds graph from explicit layer list then delegates to detector
- `ArchitectureIgnoreMatcher` — glob-based violation exclusion
- `ArchitectureTypeScanner` — finds types in a namespace layer
- `ArchitectureNamespaceViolationFinder` — namespace-level violation detection

## Goals / Non-Goals

**Goals:**
- Add `ArchitectureAcyclicSiblingContract` model with `ancestors` (list of namespace prefixes)
- Implement namespace discovery: scan loaded types, group by immediate child segment under each ancestor
- Attribute descendant dependencies up to the direct sibling group
- Reuse `ArchitectureCycleDetector` for cycle detection on sibling graphs
- Report diagnostics with ancestor context
- Wire into runner, validator, CLI, testing adapters, and JSON schema
- Maintain strict/audit symmetry matching existing cycle contracts
- Full backward compatibility with existing cycle contracts

**Non-Goals:**
- Deep/recursive multi-level sibling checking (reserved for future issue)
- Graph visualization UI
- Performance/caching optimizations
- Replacing existing explicit cycle contracts

## Decisions

### Decision: Namespace-based ancestors (not layer references)
**Choice**: `ancestors` takes raw namespace prefixes like `MyApp.Features`, not layer names declared in `layers:`.
**Rationale**: The feature's value is autonomy — discovering siblings without requiring them to be pre-declared as layers. Requiring layer declarations would blur the distinction with layer templates and reduce automation.
**Alternative considered**: Layer-name references would be consistent with existing contract patterns but defeat the discovery purpose.

### Decision: Direct siblings only in v1
**Choice**: Only check direct sibling children of each ancestor. "Recursive" means dependency attribution through sub-packages (e.g., `Auth.Controllers` → `Auth`), not multi-level cycle checking.
**Rationale**: Keeps scope aligned with issue acceptance criteria. Deep mode adds complexity with no demonstrated use case.
**Alternative considered**: A `depth` parameter with `direct`/`deep` modes was deferred.

### Decision: Multiple ancestors in one contract
**Choice**: A single contract accepts `ancestors: [list]`. Each ancestor is evaluated independently with its own diagnostics.
**Rationale**: Keeps policy files compact for projects with many sibling groups. Independent evaluation per ancestor avoids cross-contamination of cycle reports.

### Decision: Reuse ArchitectureCycleDetector as-is
**Choice**: Build the sibling dependency graph as `Dictionary<string, HashSet<string>>` (sibling name → dependant sibling names) and pass to the existing `FindCycles` method.
**Rationale**: The detector is a pure function on the graph shape. No changes needed to the detector itself.
**Alternative considered**: Creating a specialized sibling cycle detector — rejected as unnecessary duplication.

### Decision: Diagnostic format includes ancestor context
**Choice**: Cycle strings use format `"<ancestor>: <siblingA> -> <siblingB> -> <siblingC> -> <siblingA>"`.
**Rationale**: The same sibling name could appear under different ancestors (e.g., `Auth` under both `Features` and `Modules`). The ancestor prefix disambiguates.
**Alternative considered**: Returning structured objects with ancestor + cycle path fields — more complex and inconsistent with existing string-based cycle reporting.

### Decision: Empty/single-child ancestors are silent
**Choice**: When an ancestor has 0 or 1 discovered child namespace, produce no output (no cycle, no warning).
**Rationale**: Not a configuration error — the policy is valid but no siblings exist to check. Verbose/debug output could expose this if needed later.
**Alternative considered**: Emitting a diagnostic warning for single-child namespaces — would surface as noise in most projects.

### Decision: New contract group properties, not type-discriminated union
**Choice**: Add `StrictAcyclicSiblings` and `AuditAcyclicSiblings` lists to `ArchitectureContractGroups`, following the established pattern (e.g., `StrictCycles`, `AuditCycles`).
**Rationale**: Consistent with existing pattern. Every contract type gets its own named list in both modes.

## Risks / Trade-offs

- **False cycles from broad ancestors**: An ancestor namespace that captures unrelated namespaces (e.g., `MyApp`) could produce surprising cycle reports. Mitigation: policy authors control ancestor selection; documentation should recommend specific ancestors.
- **Large sibling groups**: An ancestor with dozens of siblings produces a large graph. Mitigation: the detector is O(nodes + edges) DFS; acceptable for source-scale graphs. Performance optimization deferred to if needed.
- **Multi-ancestor cycle overlap**: The same type could match multiple ancestors if prefixes overlap. Mitigation: evaluated independently per ancestor, so both cycles are reported — this is correct behavior.
