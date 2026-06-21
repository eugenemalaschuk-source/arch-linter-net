# Output Formats

ArchLinterNet supports human-readable output for local development and JSON output for CI artifacts and downstream automation.

## Human output

Use human output when reading diagnostics in a terminal or CI log:

```bash
arch-linter-net --mode strict --format human
```

Example shape:

```text
[VIOLATION] application-must-not-depend-on-infrastructure
  MyApp.Application.Services.LegacyService
    -> MyApp.Infrastructure.Repositories.UserRepository
```

Human output is optimized for readability, not machine parsing.

## JSON output

Use JSON output for CI artifacts, dashboards, or automation:

```bash
arch-linter-net --mode strict --format json > architecture-violations.json
```

Shortcut:

```bash
arch-linter-net --strict --json > architecture-violations.json
```

JSON output is written to stdout. When `--timings` is also enabled, timings are written to stderr so stdout remains parseable.

## CI artifact pattern

```yaml
- name: Validate architecture
  run: arch-linter-net --strict --json > architecture-violations.json

- name: Upload architecture violations
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: architecture-violations
    path: architecture-violations.json
```

For audit runs, keep the job non-blocking and always upload the artifact:

```yaml
- name: Architecture audit
  if: always()
  continue-on-error: true
  run: arch-linter-net --audit --json > architecture-audit.json
```
