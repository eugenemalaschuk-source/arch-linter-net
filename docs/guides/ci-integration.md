# CI Integration

## GitHub Actions

### Basic validation

```yaml
name: Architecture validation

on: [push, pull_request]

jobs:
  architecture:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4

      - name: Install ArchLinterNet
        run: dotnet tool install --global ArchLinterNet.Cli

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Validate architecture (strict)
        run: dotnet arch-linter-net

      - name: Audit report (non-blocking)
        if: always()
        run: dotnet arch-linter-net --mode audit --json > audit-report.json

      - name: Upload audit report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-audit
          path: audit-report.json
```

### Strict validation with artifact

```yaml
- name: Validate architecture (strict)
  run: dotnet arch-linter-net --json > violations.json

- name: Upload violations
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: architecture-violations
    path: violations.json
```

## Azure Pipelines

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Install ArchLinterNet'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'install --global ArchLinterNet.Cli'

- script: 'dotnet arch-linter-net'
  displayName: 'Validate architecture'
```

## Exit codes for CI

| Code | Meaning | CI Action |
|------|---------|-----------|
| `0` | No violations | Pass |
| `1` | Strict violations found | Fail |
| `2` | Configuration error | Fail |
