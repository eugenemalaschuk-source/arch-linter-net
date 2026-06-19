# CLI Usage

## Usage

```bash
arch-linter-net [options]
```

## Options

| Option | Description | Default |
|--------|-------------|---------|
| `-p`, `--policy <path>` | Path to YAML policy file | `architecture/dependencies.arch.yml` |
| `-m`, `--mode <mode>` | Validation mode: `strict` or `audit` | `strict` |
| `--strict` | Shortcut for `--mode strict` | |
| `--audit` | Shortcut for `--mode audit` | |
| `--contract <id>` | Run only the contract with the given ID (may be repeated) | |
| `-f`, `--format <fmt>` | Output format: `human` or `json` | `human` |
| `--json` | Shortcut for `--format json` | |
| `-h`, `--help` | Show help message | |
| `-v`, `--version` | Show version | |

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

Contract IDs are defined in the YAML policy file. If a contract has no explicit
`id`, it is derived automatically from its `name`. Unknown contract IDs produce
exit code 2 with a diagnostic listing available IDs.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | All contracts passed |
| `1` | One or more contracts failed |
| `2` | Runtime error (invalid arguments, file not found, etc.) |
