# Exit Codes

ArchLinterNet uses stable exit codes so CI can distinguish architecture violations from runtime/configuration failures.

| Code | Meaning | CI interpretation |
|------|---------|-------------------|
| `0` | Validation completed and no violations were found. | Pass |
| `1` | Validation completed and one or more selected contracts failed. | Fail for strict gates; expected for failing audit checks if run manually |
| `2` | Runtime or configuration error before normal validation completed. | Fail |

## Exit code 1

Exit code `1` means the tool worked and found architecture violations. Examples:

- a strict dependency contract found a forbidden reference;
- a cycle contract found a cycle;
- an allow-only contract found an unapproved layer reference;
- a namespace coverage contract reported `coverage_findings` while `analysis.coverage` is `error`;
- the policy-consistency pass reported `policy_consistency_findings` while `analysis.policy_consistency` is `error`;
- a stale ignored violation is treated as a blocking policy error by current configuration.

Note: coverage, policy-consistency, and unmatched-ignore failures do not appear in `--format sarif` results — see [Output Formats — SARIF output](output-formats.md#sarif-output) if you rely on SARIF alone in CI.

## Exit code 2

Exit code `2` means the run could not be trusted as normal validation. Examples:

- invalid arguments;
- missing policy file;
- invalid YAML shape;
- unknown contract ID passed to `--contract`;
- unknown condition set passed to `--condition-set`;
- invalid `analysis.coverage`, `analysis.policy_consistency`, or `analysis.unmatched_ignored_violations` value;
- unsupported coverage scope such as `project` or `assembly`;
- baseline references a contract ID that does not exist in the policy;
- required target assemblies cannot be resolved when configuration treats that as fatal.

CI should fail closed on exit code `2`.

## Strict + audit CI pattern

```yaml
- name: Validate architecture (strict)
  run: arch-linter-net --mode strict

- name: Architecture audit report
  if: always()
  continue-on-error: true
  run: arch-linter-net --mode audit --json > architecture-audit.json
```

Strict failures block the pull request. Audit failures are captured for visibility without changing the strict gate result.
