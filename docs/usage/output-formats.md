# Output Formats

ArchLinterNet supports human-readable output for local development and JSON output for CI artifacts and downstream automation.

## Human output

Use human output when reading diagnostics in a terminal or CI log:

```bash
arch-linter-net --mode strict --format human
```

Example shape:

```text
- [application-not-infrastructure] [application-must-not-depend-on-infrastructure] MyApp.Application.Services.LegacyService -> MyApp.Infrastructure: MyApp.Infrastructure.Repositories.UserRepository
```

Human output is optimized for readability, not machine parsing.

When enabled and non-empty, supplemental diagnostics are emitted in dedicated sections:

- `Coverage findings:` for namespace and rule-input coverage contracts;
- `Coverage summary:` for the per-contract coverage counts described in [Coverage contracts](../contracts/coverage.md#coverage-summary) — printed whenever any coverage contract ran, regardless of `analysis.coverage` severity;
- `Unmatched ignored violations:` for stale baseline/ignore entries;
- `Policy consistency findings:` for internal contradictions in the policy document.

Example supplemental section:

```text
Coverage findings:
- [feature-namespace-coverage] [feature-namespace-coverage] MyApp.Features.Payments -> uncovered namespace: MyApp.Features.Payments.PaymentsRepresentative

Coverage summary:
- [feature-namespace-coverage] [feature-namespace-coverage] scope: namespace covered=4 excluded=1 uncovered=1 stale=0 unknown=0
    uncovered: MyApp.Features.Payments (MyApp.Features.Payments.PaymentsRepresentative)
```

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

Current JSON output is a single top-level object with these arrays:

- `violations`
- `cycles`
- `coverage_findings`
- `unmatched_ignored_violations`
- `policy_consistency_findings`
- `coverage_summary`

Example shape:

```json
{
  "passed": false,
  "mode": "strict",
  "violations": [],
  "cycles": [],
  "coverage_findings": [
    {
      "contract": "feature-namespace-coverage",
      "contract_id": "feature-namespace-coverage",
      "source": "MyApp.Features.Payments",
      "forbidden_namespace": "uncovered namespace",
      "forbidden_references": ["MyApp.Features.Payments.PaymentsRepresentative"]
    }
  ],
  "unmatched_ignored_violations": [],
  "policy_consistency_findings": [
    {
      "kind": "policy_consistency",
      "check_kind": "duplicate-id",
      "contract": "domain-boundaries",
      "contract_id": "domain-boundaries",
      "reason": "Contract ID is used more than once.",
      "conflicting_contract_ids": ["domain-boundaries", "domain-boundaries"],
      "conflicting_contract_names": ["domain-boundaries", "domain-boundaries-copy"],
      "layers": []
    }
  ],
  "coverage_summary": [
    {
      "contract": "feature-namespace-coverage",
      "contract_id": "feature-namespace-coverage",
      "scope": "namespace",
      "counts": { "covered": 4, "excluded": 1, "uncovered": 1, "stale": 0, "unknown": 0 },
      "excluded_items": [
        { "item": "MyApp.Features.Video.Generated", "reason": "Generated code is excluded from manual architecture coverage." }
      ],
      "uncovered_items": [
        { "item": "MyApp.Features.Payments", "evidence": "MyApp.Features.Payments.PaymentsRepresentative" }
      ]
    }
  ]
}
```

`coverage_summary` is always present as an array (empty when no coverage contracts ran) and is reported independent of `analysis.coverage` severity, since it summarizes state rather than gating the run. See [Coverage contracts — Coverage summary](../contracts/coverage.md#coverage-summary) for the count semantics, including how `scope: rule_input` maps to `stale`/`unknown`.

Behavior for non-violation finding families is controlled separately:

- `analysis.coverage: error|warn|off` controls whether `coverage_findings` fail the run, report without failing, or are suppressed.
- `analysis.policy_consistency: error|warn|off` controls whether `policy_consistency_findings` fail the run, report without failing, or are suppressed.
- `analysis.unmatched_ignored_violations: error|warn|off` controls whether stale ignore entries fail the run, report without failing, or are suppressed.

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
