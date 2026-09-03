# Exit Codes

ArchLinterNet uses stable exit categories so CI can distinguish a completed failing gate from an invocation or evidence failure.

| Code | Meaning | CI interpretation |
| --- | --- | --- |
| `0` | The command completed and its requested validation/comparison gate passed. | Pass |
| `1` | The command completed, but its requested validation/comparison gate failed. | Fail blocking jobs; expected only for deliberately inspected non-blocking commands. |
| `2` | The command could not complete normally, or `health` completed with a valid `gate: unassessable` result. | Fail closed; inspect structured output. |

For `health`, code `2` can accompany a valid `architecture-health/v1` document whose gate is `unassessable`. Inspect `schema_id`, `gate`, and `health` to distinguish that result from an invalid invocation, which uses the command-error envelope when JSON output was selected.

## Exit code 1

Exit code `1` means ArchLinterNet completed the requested operation and the selected gate failed. Examples:

- a strict dependency contract found a forbidden reference;
- a cycle contract found a cycle;
- an allow-only contract found an unapproved layer reference;
- a coverage contract reported `coverage_findings` while `analysis.coverage` is `error`;
- the policy-consistency pass reported findings while `analysis.policy_consistency` is `error`;
- a stale ignored violation is blocking under the selected policy configuration;
- `gate` found new, resolved, stale, ambiguous, or configuration-error persistent debt, or an error-severity policy-weakening finding;
- `health` completed with `gate: fail` while retaining the normal `architecture-health/v1` document.

Coverage, policy-consistency, and unmatched-ignore failures are supplemental configuration/governance evidence and are not all represented in ordinary validation SARIF. See [Output Formats — SARIF output](output-formats.md#sarif-output) when CI consumes SARIF alongside JSON.

## Exit code 2

Exit code `2` means the run cannot be treated as an ordinary completed pass/fail gate, or Health explicitly determined that required evidence is unassessable. Examples:

- invalid arguments;
- missing policy file;
- invalid YAML shape;
- unknown contract ID passed to `--contract`;
- unknown condition set passed to `--condition-set`;
- invalid `analysis.coverage`, `analysis.policy_consistency`, or `analysis.unmatched_ignored_violations` value;
- an unsupported or malformed coverage `scope` value outside the documented closed vocabulary;
- a baseline references a contract ID that does not exist in the policy;
- a required `gate` or `health` baseline path is absent or unreadable;
- required target assemblies cannot be resolved when configuration treats that as fatal;
- a `--report` destination is not writable, collides with an input, or has an invalid format;
- `gate` or `health` receives only one of the paired base/current policy-context artifacts;
- supplied baseline, policy-context, build, applicability, topology, metric, or external-evidence input cannot be trusted;
- `health` successfully projects `gate: unassessable` from incomplete required evidence.

When a repeatable validation `--report` file sink fails, the validation result is still reported. Exit `2` carries typed output status: `output-failed` if no file sinks wrote, or `partial-output` if some sinks succeeded and some failed. Human diagnostics use stderr; structured modes retain their command/output error contract.

Cancellation also exits `2` with typed `cancelled` completion. A cancellation observed before full publication wins over a clean result; it must not be treated as a passed gate or reusable partial cache state.

CI should always fail closed on exit code `2`. A report-producing workflow may preserve and schema-check a valid unassessable Health document for reviewer presentation, but a separate required gate must still block acceptance.

## Strict and audit CI patterns

A strict validation gate is blocking:

```yaml
- name: Validate architecture (strict)
  run: arch-linter-net --mode strict
```

If audit is deliberately advisory, keep it in a separate explicitly non-blocking step and preserve its artifact:

```yaml
- name: Architecture audit report
  if: always()
  continue-on-error: true
  run: arch-linter-net --mode audit --json > architecture-audit.json
```

If both strict and audit are intended to contribute to one required decision from the same immutable build state, use the combined `--mode strict,audit` invocation instead. Its aggregate exit is `1` when either requested mode fails.
