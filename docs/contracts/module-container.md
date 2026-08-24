# Module Container Contracts

Module-container contracts discover direct feature modules below a namespace container and enforce a predefined module profile without maintaining a peer list by hand.

Groups:

- `strict_module_containers`
- `audit_module_containers`

## Example

```yaml
contracts:
  strict_module_containers:
    - id: cli-command-modules
      name: cli-command-modules
      container: MyApp.Cli.Commands
      profile: cli_command
      reason: Each direct command module follows the same reviewed feature-module boundary.
```

The built-in `cli_command` profile is used by ArchLinterNet's own CLI command container. It checks direct sibling isolation together with the profile's container-root/module-root purity, expected segments, and dependency-direction rules.

## Fields

| Field | Meaning |
| --- | --- |
| `name` | Human-readable contract name. |
| `id` | Stable contract identity used by selection, findings, and baselines. |
| `container` | Namespace container whose direct children are discovered as modules. |
| `profile` | Implemented module profile to enforce. |
| `allowed_container_root_types` | Narrow reviewed exceptions for types allowed at the container root. |
| `allowed_module_root_types` | Narrow reviewed exceptions for types allowed at a module root. |
| `ignored_violations` | Narrow migration exceptions with reasons. |
| `reason` | Architectural intent. |

Unknown fields are rejected by raw policy validation so a misspelled exception cannot silently disappear.

## When to use it

Use a module-container contract when every direct child below one namespace represents the same kind of feature/command module and new sibling modules should be governed automatically.

Use ordinary dependency, layer-template, layout, or contextual contracts instead when the children intentionally follow different architectures.
