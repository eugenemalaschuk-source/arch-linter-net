# CLI Usage

## Usage

```bash
arch-linter-net [options]
arch-linter-net baseline generate --config <path> --output <path> [--reason <text>]
```

## Validate Options

| Option | Description | Default |
|--------|-------------|---------|
| `-p`, `--policy <path>` | Path to YAML policy file | `architecture/dependencies.arch.yml` |
| `-m`, `--mode <mode>` | Validation mode: `strict` or `audit` | `strict` |
| `--strict` | Shortcut for `--mode strict` | |
| `--audit` | Shortcut for `--mode audit` | |
| `--contract <id>` | Run only the contract with the given ID (may be repeated) | |
| `--condition-set <name>` | Use a named condition set from `analysis.condition_sets` to control conditional compilation symbols during Roslyn source analysis | policy `default_condition_set`, otherwise empty |
| `--baseline <path>` | Path to baseline YAML file to merge with policy ignores | |
| `-f`, `--format <fmt>` | Output format: `human` or `json` | `human` |
| `--json` | Shortcut for `--format json` | |
| `--timings` | Print phase-level timing report to stderr | |
| `-h`, `--help` | Show help message | |
| `-v`, `--version` | Show version | |

## Baseline Subcommand

| Subcommand | Description |
|------------|-------------|
| `baseline generate` | Generate a baseline of current violations |

**Options for `baseline generate`:**

| Option | Description | Default |
|--------|-------------|---------|
| `--config <path>` | Path to YAML policy file | `architecture/dependencies.arch.yml` |
| `--output <path>` | Path to write the generated baseline file (required) | |
| `--reason <text>` | Reason text for baseline entries | `generated baseline` |
| `-h`, `--help` | Show help message | |

## Examples

### Basic validation (strict mode)

```bash
arch-linter-net
```

### Custom policy path

```bash
arch-linter-net --policy config/architecture.yml
```

### Audit mode

```bash
arch-linter-net --mode audit
```

### JSON output for CI

```bash
arch-linter-net --json > violations.json
```

### Strict mode with JSON

```bash
arch-linter-net --strict --json
```

### Run a single contract by ID

```bash
arch-linter-net --contract map-core-boundary
```

### Run multiple contracts

```bash
arch-linter-net --contract map-core-boundary --contract feature-no-cycles
```

### Use a condition set

```bash
arch-linter-net --condition-set editor
```

Condition sets control which `#if` preprocessor branches are active during
Roslyn source analysis. The named set must be defined in `analysis.condition_sets`
in the policy file. Only affects method-body contracts (Roslyn scanning).

### Generate a violation baseline

```bash
arch-linter-net baseline generate --config architecture/dependencies.arch.yml --output baseline.yml
```

### Validate with a baseline

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --baseline baseline.yml --mode strict
```

Contract IDs are defined in the YAML policy file. If a contract has no explicit
`id`, it is derived automatically from its `name`. Unknown contract IDs produce
exit code 2 with a diagnostic listing available IDs. Unknown contract IDs in
baseline files also produce exit code 2.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | All contracts passed |
| `1` | One or more contracts failed |
| `2` | Runtime error (invalid arguments, file not found, etc.) |

## Timing baseline

Use `--timings` to capture local validation baseline timings before performance work. Timing data is printed to stderr and is intended for human comparison between branches. Durations are machine-dependent; the output shape is stable, but numeric values are not part of tests.

This is a measurement tool, not a performance optimization feature. See [#19](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/19) for the performance planning story.

### Examples

```bash
# Basic timing report
arch-linter-net --strict --timings

# Audit mode with timings
arch-linter-net --audit --timings

# JSON output with timings (JSON on stdout, timings on stderr)
arch-linter-net --strict --json --timings 2>timings.txt
```

### Sample output

The timing report is printed to stderr in a stable columnar format:

```
Validation timings:
  total                                      452 ms

  load_and_setup                              51 ms
    yaml_loading                              12 ms
    root_resolution                            3 ms
    condition_set_resolution                   2 ms
    assembly_resolution                       34 ms

  configuration_check                          8 ms

  contract_checks                            389 ms
    dependency                 count=1         9 ms
    layer                      count=1        12 ms
    allow_only                 count=0         0 ms
    cycle                      count=1        20 ms
    method_body                count=2       345 ms
    asmdef                     count=0         0 ms
    independence               count=0         0 ms
    protected                  count=1         3 ms
    external                   count=0         0 ms
    acyclic_sibling            count=0         0 ms

  post_processing                              2 ms
```

`contract_checks` shows per-family breakdown with contract counts. The shape is deterministic — same policy always produces the same phase names and ordering.
