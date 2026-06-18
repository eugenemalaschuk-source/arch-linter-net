# CLI Usage

## Usage

```bash
dotnet arch-linter-net [options]
```

## Options

| Option | Description | Default |
|--------|-------------|---------|
| `-p`, `--policy <path>` | Path to YAML policy file | `architecture/dependencies.arch.yml` |
| `-m`, `--mode <mode>` | Validation mode: `strict` or `audit` | `strict` |
| `--strict` | Shortcut for `--mode strict` | |
| `--audit` | Shortcut for `--mode audit` | |
| `-f`, `--format <fmt>` | Output format: `human` or `json` | `human` |
| `--json` | Shortcut for `--format json` | |
| `-h`, `--help` | Show help message | |
| `-v`, `--version` | Show version | |

## Examples

### Basic validation (strict mode)

```bash
dotnet arch-linter-net
```

### Custom policy path

```bash
dotnet arch-linter-net --policy config/architecture.yml
```

### Audit mode

```bash
dotnet arch-linter-net --mode audit
```

### JSON output for CI

```bash
dotnet arch-linter-net --json > violations.json
```

### Strict mode with JSON

```bash
dotnet arch-linter-net --strict --json
```

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | All contracts passed |
| `1` | One or more contracts failed |
| `2` | Runtime error (invalid arguments, file not found, etc.) |
