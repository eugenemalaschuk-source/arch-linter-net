# CLI Usage

## Commands

### `validate`

Run architecture validation against the policy file.

```bash
dotnet arch-linter-net validate [options]
```

Options:

| Option | Description | Default |
|--------|-------------|---------|
| `--policy` | Path to the architecture policy file | `architecture/dependencies.arch.yml` |
| `--repository-root` | Repository root directory | Current directory |
| `--mode` | Validation mode: `strict` or `audit` | `strict` |
| `--output` | Output format: `human` or `json` | `human` |
| `--search-paths` | Additional assembly probe paths (semicolon-separated) | |
| `--project` | Path to a test project for context | |

### `--help`

Display help and available options.

```bash
dotnet arch-linter-net --help
```

## Examples

### Basic validation

```bash
dotnet arch-linter-net validate
```

### Custom policy path

```bash
dotnet arch-linter-net validate --policy config/architecture.yml
```

### Audit mode

```bash
dotnet arch-linter-net validate --mode audit
```

### JSON output for CI

```bash
dotnet arch-linter-net validate --output json > violations.json
```

### Specify assembly search paths

```bash
dotnet arch-linter-net validate --search-paths "./build-output;./libs"
```

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | No violations (or audit mode with only audit violations) |
| `1` | Strict violations found |
| `2` | Configuration or resolution error |
