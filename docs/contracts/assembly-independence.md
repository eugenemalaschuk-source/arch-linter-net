# Assembly Independence Contracts

Assembly independence contracts enforce mutual separation across a set of compiled .NET assemblies, based on real assembly references rather than namespace patterns.

Groups:

- `strict_assembly_independence`
- `audit_assembly_independence`

## Example

```yaml
contracts:
  strict_assembly_independence:
    - id: feature-assemblies-independent
      name: feature-assemblies-must-remain-independent
      assemblies:
        - MyApp.Features.Billing
        - MyApp.Features.Shipping
        - MyApp.Features.Notifications
      reason: Feature assemblies must not directly reference each other.
```

Every assembly listed in `assemblies` must also be listed in `analysis.target_assemblies`; a name that isn't a declared target assembly fails policy loading with an actionable error instead of silently being skipped.

## Semantics

For every configured assembly pair, a direct assembly reference in either direction is a violation. Detection uses each assembly's own referenced-assembly metadata (`Assembly.GetReferencedAssemblies()`), matched by assembly simple name — **direct references only**. A transitive path (A references B, B references C) between two listed assemblies is not detected by this contract family.

Violations identify the source assembly, the forbidden target assembly, and the contract ID/name.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, but for this family `source_type` and `forbidden_reference` hold **assembly simple names**, not C# type names.

## Assembly independence vs namespace/layer independence vs Unity asmdef checks

These three checks operate at different boundaries and are independent of one another:

- **[Independence contracts](independence.md)** (`strict_independence`/`audit_independence`) check *namespace/layer* boundaries — useful when a module's ownership is cleanly expressed as a namespace prefix.
- **Assembly independence contracts** (this page) check *compiled .NET assembly* boundaries directly — useful when assembly ownership doesn't map cleanly to namespace prefixes (e.g. feature assemblies, plugin packages, or a `Domain.Abstractions`/`Infrastructure.*` split), or when you want to catch an accidental project/assembly reference even if namespaces would otherwise look fine.
- **Unity `.asmdef` checks** (`strict_asmdef`/`audit_asmdef`) validate Unity's own `.asmdef` JSON assembly-definition manifests and editor-reference rules — a Unity-specific mechanism, unrelated to generic .NET assembly references, and unaffected by this contract family.

Use assembly independence contracts when you need to guarantee that two or more assemblies never directly reference each other at the compiled-assembly level, regardless of what their namespaces look like.
