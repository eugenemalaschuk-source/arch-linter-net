# Unity Boundaries

ArchLinterNet can help Unity projects keep runtime, editor, feature, and pure core boundaries explicit.

Unity support is optional and should remain outside the core engine.

## Runtime must not reference editor APIs

```yaml
external_dependencies:
  unity_editor:
    namespace_prefixes:
      - UnityEditor
    type_prefixes: []

contracts:
  strict_external:
    - id: runtime-no-unity-editor
      name: runtime-must-not-reference-unity-editor
      source: runtime
      forbidden: [unity_editor]
      reason: Runtime code must not expose UnityEditor APIs.
```

## asmdef runtime/editor separation

```yaml
contracts:
  strict_asmdef:
    - id: runtime-asmdefs-no-editor
      name: runtime-asmdefs-must-not-reference-editor-assemblies
      source_assemblies:
        - Runtime
      forbidden_editor_refs: true
      reason: Runtime asmdefs must not depend on editor-only assemblies.
```

## Pure core must not reference Unity runtime

```yaml
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes: []

contracts:
  strict_external:
    - id: core-no-unity-runtime
      name: core-must-not-reference-unity-runtime
      source: core
      forbidden: [unity_runtime]
      reason: Pure core must stay independent from Unity runtime types.
```

## Feature sibling cycles

```yaml
contracts:
  strict_acyclic_siblings:
    - id: game-features-acyclic
      name: game-feature-siblings-must-be-acyclic
      ancestors:
        - MyGame.Game.Features
      reason: Feature namespaces must not form dependency cycles.
```

## Condition sets

When source contains `#if UNITY_EDITOR`, define condition sets:

```yaml
analysis:
  condition_sets:
    runtime: []
    editor: [UNITY_EDITOR]
  default_condition_set: runtime
```

Run both when needed:

```bash
arch-linter-net --condition-set runtime
arch-linter-net --condition-set editor
```

## What not to claim

These rules are static architecture checks. They do not validate Unity runtime scene wiring, serialized references, dependency injection runtime behavior, or gameplay authorization/security logic.
