# Adopt in an Existing Repository

Existing repositories often have architecture debt. The goal is to freeze known violations while preventing new ones.

## 1. Inspect real code first

Before writing YAML, identify:

- project and assembly names;
- namespace roots;
- current project references;
- existing architecture seams;
- known migration issues;
- build output paths needed by the CLI.

Do not start from an ideal diagram that does not map to real code.

## 2. Start with one strict rule

Pick a boundary that already passes or has a small known violation set:

```yaml
contracts:
  strict:
    - id: domain-not-infrastructure
      name: domain-must-not-depend-on-infrastructure
      source: domain
      forbidden: [infrastructure]
      reason: Domain code must remain independent of infrastructure.
```

Run:

```bash
arch-linter-net --mode strict
```

## 3. Put future-state rules in audit

```yaml
contracts:
  audit:
    - id: audit-application-to-legacy
      name: audit-application-to-legacy
      source: application
      forbidden: [legacy_runtime]
      reason: Discover legacy coupling before migration.
```

Audit rules give visibility without turning the first adoption PR into a large refactoring.

## 4. Generate a baseline for known debt

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output architecture/baseline.arch.yml \
  --reason "Initial adoption baseline"
```

Commit the baseline only after reviewing it. Each entry should represent known debt, not a new hiding mechanism.

## 5. Add CI

Use strict validation as the blocking gate and audit validation as a non-blocking artifact. See [CI integration](ci-integration.md).

## 6. Tighten over time

As violations are fixed:

1. Remove stale baseline entries.
1. Promote mature audit rules to strict.
1. Add coverage checks for unmapped namespaces when the policy shape is stable.
1. Keep policy examples and AI guidance up to date.

## Avoid false confidence

Do not add unsupported YAML fields just because they look plausible. If the schema does not support a field, the policy does not enforce that behavior. Check [supported capabilities and non-goals](../policy-format/supported-capabilities.md).
