# CLI Usage

The ArchLinterNet CLI validates architecture policies and manages violation baselines.

## Commands

```bash
arch-linter-net [options]
arch-linter-net baseline generate --config <path> --output <path> [options]
arch-linter-net baseline update --config <path> --baseline <path> --output <path> [options]
arch-linter-net baseline prune --config <path> --baseline <path> --output <path> [options]
arch-linter-net baseline diff --config <path> --baseline <path> [options]
arch-linter-net baseline verify --config <path> --baseline <path> [options]
arch-linter-net policy check --policy <path> [options]
arch-linter-net schema list
arch-linter-net schema print <logical-id>
arch-linter-net public-api capture --policy <path> --contract <id> --output <path> [options]
arch-linter-net public-api diff --policy <path> --contract <id> --snapshot <path> [options]
arch-linter-net public-api update --policy <path> --contract <id> --snapshot <path> [options]
arch-linter-net public-api migrate --policy <path> --contract <id> --output <path> [options]
```

During repository development, replace `arch-linter-net` with:

```bash
dotnet run --project src/ArchLinterNet.Cli --
```

## Packaged schemas

Installed releases expose an immutable `adoption-stabilization/v1` schema registry without a repository checkout or network access.

```bash
arch-linter-net schema list
arch-linter-net schema print policy-root
```

`list` reports each logical ID, format version, immutable `$id`, and packaged path. `print` writes the exact release-matched JSON Schema to standard output.

## Validate options

## Policy check

`policy check` validates policy syntax, imports, composition, IDs, and static configuration without invoking MSBuild, evaluating projects, or loading target assemblies. It is intended for clean checkouts, editor feedback, and pre-commit checks; it never claims that an architecture is clean.

```bash
arch-linter-net policy check --policy architecture/dependencies.arch.yml --format json
```

The command exits `0` for valid static policy/configuration and reports fact-dependent validation as explicit deferred checks. Invalid policy/configuration exits `2`.

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
| `-f`, `--format <fmt>` | Output format for stdout: `human`, `json`, or `sarif`. Use `--report` to route additional formats to other destinations. `human` and `json` include a coverage summary (counts + exclusions) for any coverage contracts that ran; `sarif` covers violations and cycles only — see [Output Formats](../usage/output-formats.md). | `human` |
| `--json` | Shortcut for `--format json` | |
| `--report <format>=<destination>` | Repeatable. Route a format (`human`, `json`, `sarif`) to a destination (`stdout`, `stderr`, or a file path). Multiple `--report` flags are allowed. Format strings are computed once and dispatched to all requesting sinks. | |
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

### SARIF output

```bash
arch-linter-net --strict --format sarif > architecture-violations.sarif
```

### Multi-sink output

```bash
# Human to stdout, JSON to a file, SARIF to a file
arch-linter-net --strict --report json=results.json --report sarif=results.sarif

# JSON to stdout, human to stderr
arch-linter-net --format json --report human=stderr

# Human to stdout, JSON and SARIF files
arch-linter-net --strict --report json=ci-report.json --report sarif=ci-report.sarif
```

```powershell
# PowerShell: human to stdout, JSON to a file with explicit format
arch-linter-net --strict --report json=results.json

# PowerShell: JSON to stdout, SARIF to a file
arch-linter-net --format json --report sarif=ci-report.sarif
```

### Multi-mode combined output

```bash
arch-linter-net --mode strict,audit --report json=combined-results.json --report sarif=combined-results.sarif
```

```powershell
# PowerShell: combined strict+audit with JSON and SARIF files
arch-linter-net --mode strict,audit --report json=combined-results.json --report sarif=combined-results.sarif
```

Combined JSON contains one result per mode; combined SARIF merges runs into a single document.

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
| `--contract <id>` | Restrict to this contract ID. May be repeated. | |
| `--condition-set <name>` | Use a named condition set from `analysis.condition_sets` | policy `default_condition_set`, otherwise empty |
| `-h`, `--help` | Show help message | |

Validate with a baseline:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode strict
```

### Baseline lifecycle subcommands

`update`, `prune`, `diff`, and `verify` all accept `--config`, `--baseline`,
`--mode`, `--condition-set`, and `--contract`; `update` and `prune` also take
`--output` (required — where the modified baseline is written), and `prune`,
`diff`, and `verify` accept `--json` for machine-readable output.

```bash
# Add new debt, keep valid entries' reason text untouched
arch-linter-net baseline update \
  --config architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml \
  --output architecture/baseline.arch.yml

# Remove entries whose violation was fixed or whose contract ID no longer exists
arch-linter-net baseline prune \
  --config architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml \
  --output architecture/baseline.arch.yml

# Read-only report of new/matched/resolved/stale/ambiguous entries
arch-linter-net baseline diff \
  --config architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml

# CI gate: exit 1 if the baseline has drifted out of sync
arch-linter-net baseline verify \
  --config architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml
```

See [Migration baselines](../guides/migration-baselines.md) for the full
lifecycle walkthrough.

## public-api

`public-api` manages the reviewed snapshot behind a
[public API surface contract](../contracts/public-api-surface.md), so a large exported
surface is reviewed as a file diff instead of a hand-maintained inline `declared_api` list.

All four subcommands take `--policy` (default `architecture/dependencies.arch.yml`,
aliased `--config`), a required `--contract <id>` naming a strict or audit public API
surface contract, an optional `--condition-set`, and `--format`. Build-state preflight runs
first: a missing, stale, or wrong-target-framework assembly fails the command before anything
is captured or written.

`--format human|json` is supported everywhere; `diff` additionally accepts `sarif`, because it
is the one subcommand whose output is a pure finding set. `capture`, `update`, and `migrate`
reject `sarif` rather than silently emitting human text, and their `json` output is a single
parsable document (status, destination, delta, proposed content) with no prose appended.

Every path is resolved against the policy boundary before any read or write, so an absolute
path, a `../` escape, or the policy file itself is refused — and the resolved destination is
what actually gets written, even when the command runs from outside the repository root.

```bash
# Write the current exported surface to a reviewed snapshot
arch-linter-net public-api capture \
  --policy architecture/dependencies.arch.yml \
  --contract module-api \
  --output architecture/api/module-api.txt

# CI gate: exit 1 when the live surface drifted from the snapshot
arch-linter-net public-api diff \
  --contract module-api \
  --snapshot architecture/api/module-api.txt

# Review the proposed snapshot change without writing it
arch-linter-net public-api update \
  --contract module-api \
  --snapshot architecture/api/module-api.txt \
  --dry-run

# Convert an existing inline declared_api list into a snapshot
arch-linter-net public-api migrate \
  --contract module-api \
  --output architecture/api/module-api.txt
```

Safety rules worth knowing before wiring this into a pipeline:

- `capture` refuses to overwrite an existing snapshot whose content differs unless `--force`
  is passed; capturing over a byte-identical file succeeds and reports that it is already current.
- `update` writes only when not in `--dry-run`, and reports the structured delta either way.
  Unchanged entries are rewritten exactly as before, so the file diff shows only real movement.
- `update` against a contract that declares its surface inline (no `api_snapshot`) is refused:
  rewriting the policy YAML cannot preserve the surrounding comments safely. Run `migrate` first.
- `migrate` refuses to write while the inline list differs from the live surface, listing every
  stale inline entry and every undeclared exported member. Pass `--accept-drift` to record the
  live surface deliberately; the drift is still reported.
- Exit code `1` means a completed gate found drift (`diff` drift, or unaccepted `migrate` drift).
  Exit code `2` means the operation never completed: invalid arguments, unknown contract, unusable
  snapshot, unsafe path, or blocked build state.
- `update --snapshot` must resolve to the contract's own `api_snapshot`; pointing it at another
  file is refused rather than leaving the policy pointing at a stale snapshot.

## Related pages

- [Output formats](../usage/output-formats.md)
- [Exit codes](../usage/exit-codes.md)
- [Timings](../usage/timings.md)
- [Migration baselines](../guides/migration-baselines.md)
