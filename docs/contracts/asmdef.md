# Unity asmdef Contracts

Unity asmdef contracts validate Unity assembly definition dependency boundaries.

Groups:

- `strict_asmdef`
- `audit_asmdef`

## Example

```yaml
contracts:
  strict_asmdef:
    - id: runtime-no-editor-refs
      name: runtime-must-not-reference-editor-assemblies
      source_assemblies:
        - Runtime
      forbidden_editor_refs: true
      reason: Runtime assemblies must not depend on UnityEditor-only code.
```

## Common rules

Use asmdef contracts to check that:

- runtime assemblies do not reference editor-only assemblies;
- pure core assemblies do not reference Unity runtime assemblies when that boundary is required;
- feature assemblies do not reference unrelated feature assemblies;
- Unity assembly references follow the same direction as namespace or project policy.

## Scope

Unity support is optional. Keep Unity-specific checks behind the Unity package and do not make the core engine depend on Unity.

For larger Unity adoption guidance, see [Unity boundaries](../guides/unity-boundaries.md).
