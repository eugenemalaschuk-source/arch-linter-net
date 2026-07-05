# Assembly Dependency Contracts

Assembly dependency contracts enforce *directional* rules across compiled .NET assemblies — one named source assembly must not reference (or must only reference) other named assemblies — based on real assembly references rather than namespace patterns.

Two families are covered on this page:

- **Assembly dependency** (`strict_assembly_dependency`/`audit_assembly_dependency`) — a source assembly must not directly reference any assembly in a `forbidden` list.
- **Assembly allow-only** (`strict_assembly_allow_only`/`audit_assembly_allow_only`) — a source assembly may only directly reference assemblies in an `allowed` list (plus itself); any other direct reference to a declared assembly is a violation.

## Assembly dependency example

```yaml
contracts:
  strict_assembly_dependency:
    - id: domain-no-infrastructure
      name: domain-must-not-reference-infrastructure
      source: MyApp.Domain
      forbidden:
        - MyApp.Infrastructure
      reason: Domain must stay free of infrastructure concerns.
```

## Assembly allow-only example

```yaml
contracts:
  strict_assembly_allow_only:
    - id: application-allowed-refs
      name: application-may-only-reference-abstractions
      source: MyApp.Application
      allowed:
        - MyApp.Domain
        - MyApp.Domain.Abstractions
      reason: Application may depend on abstractions, not concrete adapters.
```

Every assembly name referenced by these contracts (`source`, `forbidden`, `allowed`) must also be listed in `analysis.target_assemblies`; a name that isn't a declared target assembly fails policy loading with an actionable error instead of silently being skipped.

## Semantics

Both families detect **direct assembly references only**, using each assembly's own referenced-assembly metadata (`Assembly.GetReferencedAssemblies()`), matched by assembly simple name. A transitive path (A references B, B references C) is not detected by either family — this is a deliberate MVP scope decision, matching [assembly independence contracts](assembly-independence.md)'s existing direct-only behavior.

**Assembly dependency**: for each entry in `forbidden` (declaration order), a violation is reported if `source` directly references that assembly. A `source` name that also appears in its own `forbidden` list is never flagged as a self-violation.

**Assembly allow-only**: a violation is reported once per source, listing every direct reference that is (a) present in `analysis.target_assemblies` (a *declared* assembly) and (b) not in `allowed` and not the source itself. References to assemblies outside `analysis.target_assemblies` (framework, BCL, or NuGet assemblies never declared to ArchLinterNet) are not violations — this mirrors how the namespace-level [allow-only contract](allow-only.md) excludes references to types outside any declared layer.

Violations identify the source assembly, the forbidden/disallowed target assembly (or assemblies, for allow-only), and the contract ID/name.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, but for these families `source_type` and `forbidden_reference` hold **assembly simple names**, not C# type names.

## Assembly dependency vs namespace/layer dependency contracts vs assembly independence vs Unity asmdef checks

These checks operate at different boundaries and are independent of one another:

- **[Dependency contracts](dependency.md)** (`strict`/`audit`) and **[allow-only contracts](allow-only.md)** (`strict_allow_only`/`audit_allow_only`) check *namespace/layer* boundaries — useful when a module's ownership is cleanly expressed as a namespace prefix.
- **Assembly dependency and assembly allow-only contracts** (this page) check *compiled .NET assembly* boundaries directly and directionally — useful when project/assembly ownership doesn't map cleanly to namespace prefixes, or when you want to guarantee `MyApp.Domain` never references `MyApp.Infrastructure` regardless of what namespaces look like.
- **[Assembly independence contracts](assembly-independence.md)** (`strict_assembly_independence`/`audit_assembly_independence`) check *mutual* assembly independence (neither of a pair may reference the other) — use assembly dependency/allow-only instead when the relationship is directional rather than mutual.
- **Unity `.asmdef` checks** (`strict_asmdef`/`audit_asmdef`) validate Unity's own `.asmdef` JSON assembly-definition manifests and editor-reference rules — a Unity-specific mechanism, unrelated to generic .NET assembly references, and unaffected by this contract family.

## Scope: what's not covered here

Ordered assembly/project layers (an assembly-axis analog of [layer contracts](layers.md)) and assembly/project cycle detection (an assembly-axis analog of [cycle contracts](cycles.md)) are not covered by these two families. They are deferred to a follow-up contract family, matching how `layer` and `cycle` are already separate families from `dependency`/`allow_only` at the namespace level.
