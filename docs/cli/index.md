# CLI Usage

The ArchLinterNet CLI validates architecture policies and manages violation baselines.

## Commands

```bash
arch-linter-net [options]
arch-linter-net baseline generate --config <path> --output <path> [options]
```

During repository development, replace `arch-linter-net` with:

```bash
dotnet run --project src/ArchLinterNet.Cli --
```

## Validate options

| Option | Description | Default |
|--------|-------------|---------|
| `-p`, `--policy <path>` | Path to YAML policy file | `architecture/dependencies.arch.yml` |
| `-m`, `--mode <mode>` | Validation mode: `strict` or `audit` | `strict` |
| `--strict` | Shortcut for `--mode strict` | |
| `--audit` | Shortcut for `--mode audit` | |
| `--contract <id>` | Run only the contract with the given ID. May be repeated. | |
| `--condition-set <name>` | Use a named condition set from `analysis.condition_sets` for Roslyn source analysis. | policy `default_condition_set`, otherwise empty |
| `--baseline <path>` | Path to baseline YAML file to merge with policy ignores. | |
| `-f`, `--format <fmt>` | Output format: `human` or `json` | `human` |
| `--json` | Shortcut for `--format json` | |
| `--timings` | Print phase-level timing report to stderr. | |
| `-h`, `--help` | Show help message. | |
| `-v`, `--version` | Show version. | |

## Examples

### Strict validation

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
```

### Audit validation

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode audit
```

### JSON output

```bash
arch-linter-net --strict --json > architecture-violations.json
```

### Run selected contracts

```bash
arch-linter-net --contract map-core-boundary --contract feature-no-cycles
```

Unknown contract IDs produce exit code `2` with a diagnostic listing available IDs.

### Use a condition set

```bash
arch-linter-net --condition-set editor
```

Condition sets control which `#if` branches are active during Roslyn source/method-body analysis. Unknown condition set names produce exit code `2`.

## Baseline subcommand

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output architecture/baseline.arch.yml \
  --reason "Initial baseline"
```

| Option | Description | Default |
|--------|-------------|---------|
| `--config <path>` | Path to YAML policy file | `architecture/dependencies.arch.yml` |
| `--output <path>` | Path to write the generated baseline file | required |
| `--mode <mode>` | Contract mode: `strict`, `audit`, or `all` | `all` |
| `--reason <text>` | Reason text for baseline entries | `generated baseline` |
| `--condition-set <name>` | Use a named condition set from `analysis.condition_sets` | policy `default_condition_set`, otherwise empty |
| `-h`, `--help` | Show help message | |

Validate with a baseline:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode strict
```

## Related pages

- [Output formats](../usage/output-formats.md)
- [Exit codes](../usage/exit-codes.md)
- [Timings](../usage/timings.md)
- [Migration baselines](../guides/migration-baselines.md)
