## Context

The configuration pre-check in `ArchitectureContractRunner.CheckConfiguration()` validates that every layer referenced by a contract has at least one type in the loaded assemblies. If not, it reports `[] -> empty layer namespace` and the tool exits code 1.

This works well for first-party layers whose assemblies are always present. But it breaks for layers that intentionally reference namespaces outside the current scan environment:

- External SDKs / engine namespaces (UnityEngine, Sentry, Datadog, EasyAntiCheat)
- First-party MonoBehaviour layers (unloadable without Unity engine assemblies)
- Platform-conditional namespaces (Windows-only APIs on Linux CI)
- Future/contractual namespaces used as architectural boundaries before code exists

The dependency checks themselves don't need target-side types — they match namespace *strings* in source-type IL metadata. So the configuration check is the only blocker.

## Goals / Non-Goals

**Goals:**
- Add an `external: true` property to `ArchitectureLayer` that suppresses the empty-layer diagnostic
- Keep all contract checks (dependency, layering, cycles, independence, allow-only, method-body) working unchanged
- If types ARE found for an external layer, use them normally
- Update the JSON schema, model, and tests

**Non-Goals:**
- No CLI-level `--skip-configuration` flag (too broad, per-layer granularity is correct)
- No diagnostic downgrade to warning — suppress entirely for external layers
- No changes to dependency scanning or namespace matching logic
- No changes to how `legacy_runtime_layers` work

## Decisions

### Decision: `external: true` as the flag name

| Name | Pro | Con |
|------|-----|-----|
| `external` 👍 | Clear intent — "external to this project" | Could imply external assembly vs external namespace |
| `unloadable` | Describes mechanism | Negative connotation, sounds like error state |
| `allow_empty` | Explicit about effect | Implementation-leaky, verbose |
| `soft` | Short | Vague |

**Chosen: `external: true`** — matches the mental model of "these namespaces are external to my codebase, they may not be available."

### Decision: Suppress, don't downgrade

The diagnostic is either meaningful (typo in namespace, missing assembly) or not (intentionally external). There's no useful middle ground. The `external` flag is the user's explicit declaration of intent — suppression is the correct response.

### Decision: `external` is orthogonal to `namespace_suffix`

A layer can be both external and have a suffix constraint. The two properties don't interact.

### Decision: No warning when external layer has no types in source context

If an external layer is used as a `source` in a contract, `FindTypesInLayer` returns empty and no violations are produced. This is correct — you can't scan what isn't loaded. Adding a warning would create noise for a situation the user explicitly opted into.

## Risks / Trade-offs

- **Risk**: User marks first-party layer as `external` by mistake, masking a real empty-layer bug. **Mitigation**: The configuration check already catches truly empty layers — removing the check only applies when the user explicitly sets `external: true`. This is an intentional opt-out.
- **Risk**: Silent contract gap — external source layer produces no violations. **Mitigation**: Use case is rare and intentional. Documentation will note this.
- **Risk**: Schema backward compatibility — older tools might not recognize `external`. **Mitigation**: JSON Schema marks it optional with `default: false`. YamlDotNet has `IgnoreUnmatchedProperties()` already configured, so loading a schema with `external: true` in an older version would silently ignore it — which is safe since `external: true` only suppresses diagnostics, it doesn't change behavior in unsafe ways.
