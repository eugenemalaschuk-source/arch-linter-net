# CI Integration

A good CI setup separates blocking strict validation from non-blocking audit visibility.

## Recommended GitHub Actions workflow

```yaml
name: Architecture validation

on:
  pull_request:
  push:
    branches: [main]

jobs:
  architecture:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Validate architecture (strict)
        run: dotnet arch-linter-net --mode strict --json > architecture-strict.json

      - name: Upload strict diagnostics
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-strict
          path: architecture-strict.json

      - name: Architecture audit report
        if: always()
        continue-on-error: true
        run: dotnet arch-linter-net --mode audit --json > architecture-audit.json

      - name: Upload audit diagnostics
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-audit
          path: architecture-audit.json
```

Use `dotnet tool restore` with a local tool manifest when the repository should pin the ArchLinterNet version. Use `dotnet tool install --global ArchLinterNet.Cli` only when global installation is acceptable for your pipeline.

## Exit code behavior

| Code | Meaning | CI action |
|------|---------|-----------|
| `0` | No selected contract violations | Pass |
| `1` | Validation completed and violations were found | Fail strict jobs; expected when manually inspecting failing audit rules |
| `2` | Invalid arguments, invalid configuration, missing files, or other runtime error | Fail closed |

See [Exit codes](../usage/exit-codes.md) for details.

## Strict vs audit jobs

Strict validation is the no-new-debt gate. It should fail a pull request when an enforced architecture boundary is violated.

Audit validation is visibility for migration work. It can be uploaded as an artifact, posted to a dashboard, or inspected periodically, but it should not accidentally become the strict gate unless the team intentionally promotes the audit rule.

## Baseline in CI

For existing repositories with known debt:

```yaml
- name: Validate architecture with baseline
  run: dotnet arch-linter-net \
    --policy architecture/dependencies.arch.yml \
    --baseline architecture/baseline.arch.yml \
    --mode strict
```

The baseline should be reviewed like code and cleaned up as violations are fixed.

## Baseline debt semantics in the coverage gate

When architecture coverage is wired into CI as a quality gate (see the `architecture-coverage` steps in this repository's `.github/workflows/ci.yml`, which run after the existing acceptance gate against the same already-built solution), baseline entries change how findings are reported, not whether they exist:

- **Existing accepted debt** lives in the baseline file and does not fail the pull request. The strict run still reports it in `coverage_findings`/`coverage_summary`, but a finding matched by a baseline entry is treated as known debt rather than a regression.
- **New coverage findings** — anything not matched by an existing baseline entry — fail the pull request. This is what keeps the gate "no new debt" instead of "no debt."
- **Resolved baseline entries** become stale: once the underlying violation no longer exists, the baseline entry has nothing left to match. Stale baseline entries should be removed during normal maintenance so the baseline file reflects only real outstanding debt.
- **Exclusions require a `reason`.** An exclusion is a deliberate, reviewed decision to leave a unit out of coverage scope — it is not a way to silently bypass the gate. Treat the `reason` field as required documentation, not boilerplate, and review exclusions the same way you'd review a baseline entry.

To inspect the same full-solution coverage report locally before pushing, run `make architecture-coverage-report`, which prints both the human-readable and JSON views.

**All-zero counts can mean two different things.** If `coverage_summary` is an empty list, the policy defines no coverage contracts at all (`strict_coverage`/`audit_coverage` are absent) — the report's note line calls this out explicitly. That is different from a policy that *does* define coverage contracts and reports zero uncovered/stale/unknown items, which means real coverage contracts exist and nothing is currently failing them. This repository's own `architecture/dependencies.arch.yml` is the first case today: it has no coverage contracts, so the gate currently passes trivially and the percentages you might expect (e.g. "X% of namespaces covered") aren't meaningful until coverage contracts are added.

## Azure Pipelines example

```yaml
- task: DotNetCoreCLI@2
  displayName: Restore local tools
  inputs:
    command: custom
    custom: tool
    arguments: restore

- script: dotnet arch-linter-net --mode strict
  displayName: Validate architecture
```

## Documentation publication note

Repository release automation may publish MkDocs to GitHub Pages, but PR CI should only validate docs and code. It must not publish packages, create releases, or deploy documentation.
