# Output Formats

ArchLinterNet supports human-readable output for local development, JSON output for CI artifacts and downstream automation, and SARIF output for code-scanning viewers.

For report routing, partial-output, profile, and cancellation workflows, see
[Adopt or Upgrade ArchLinterNet](../guides/upgrading.md#reports-artifacts-and-completion-status).

## Release forensics report output

`arch-linter-net history analyze --from <rev> --to <rev>` writes the version-1
Release Architecture Forensics JSON report to standard output. Its canonical
bytes use UTF-8 without BOM, LF, two-space indentation, exactly one terminal LF,
fixed schema/property order, exact integer fields, and nine-place canonical
real values. Artifact equality is byte equality, not semantic JSON equality.

Use `--format markdown` for the deterministic human reading view. It summarizes
the range/configuration, hotspots, co-change clusters, bottlenecks, OCP pressure,
candidates, enrichment, and interpretation limits; it never changes the JSON
artifact. Git-only analysis remains valid when enrichment is not requested,
inapplicable, or unavailable. Failed canonical analysis writes a separate stable
diagnostic and no partial report, ranking, or candidate set.

## Policy context output

`arch-linter-net policy context --format json` writes one deterministic
`architecture-policy-context` document with `schema_version: 3`. It is a
policy-only artifact for coding-agent context: it describes effective declared
policy facts and portable provenance, and does not report an architecture
validation result. `--format markdown` renders the same model as a compact
prompt-ready summary. Neither format includes local absolute paths, build
receipts, target-assembly results, or runtime environment values.

Version 3 additionally records typed ignored-violation matchers alongside the
typed declared analysis inputs and explicit `analysis.policy_weakening`
severity. It is not backward-compatible as a
weakening-comparison input: regenerate base and current contexts with the same
supported CLI version rather than treating a missing section as empty.

## Policy weakening output

`arch-linter-net policy weakening --base-context <path> --current-context <path>` compares two separately generated policy-context JSON artifacts. It
does not load YAML or perform project/assembly analysis. The normalized result
has kind `architecture-policy-weakening`, schema version 1, the current
configured severity, and deterministically ordered findings. Every finding
contains a stable identity, weakening kind, control identity, semantic versus
`impact_not_proven` classification, base/current values and provenance,
optional canonical affected subjects, and existing schema-backed rationale.
Project include/exclude glob changes are emitted as `impact_not_proven` unless
complete resolved project membership is supplied; they are never treated as
literal-string inventories. Prefix/glob/call-pattern facts and cross-field
location unions are also `impact_not_proven` until their effective membership
or containment is proven. A required source expansion made empty-tolerant is a
semantic finding.
Changes to authored analysis `target_assemblies`, `projects`, and `source_roots`
are likewise `impact_not_proven` until effective discovery/scanner scope is
available as trusted evidence.

Human, JSON, and SARIF project that same result. JSON is suitable for CI and
SARIF uses one `ArchLinterNet.PolicyWeakening.<kind>` rule for each weakening
kind. An `error` finding exits 1; `warn` and `off` findings remain visible and
exit 0. Invalid, incomplete, or incompatible context input exits 2 instead of
being interpreted as a clean comparison.

## Architecture debt gate output

`arch-linter-net gate --policy <path> --baseline <path>` composes complete
current persistent-debt comparison with optional explicitly exported base/current
policy contexts. Its result has kind `architecture-debt-gate` and three
independent sections: `evaluation`, `persistent_debt`, and
`policy_weakening`. Persistent entries retain the exact baseline identity and
`new`/`matched`/`resolved`/`stale`/`ambiguous`/`configuration-error` lifecycle
status. Weakening entries retain their own identity, classification, severity,
values, provenance, and rationale; they never get a baseline status.

Human output is a readable sectioned report. JSON is one deterministic document
for automation. SARIF emits `gate_section: persistent_debt` or
`gate_section: policy_weakening` result properties with separate rule
namespaces. A matched entry is still visible, but only new or untrusted
persistent-debt state fails the debt dimension. The command is read-only and
does not add a `ratchet` validation mode.

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

## Remediation hints

When a diagnostic contains enough typed policy and analysis evidence, its
normalized finding can include an optional deterministic remediation hint. A
Human report appends a concise `remediation: <category>: <summary>` clause; JSON
exposes the full structured value as `remediation_hint`; and SARIF retains the
same normalized value under `properties.arch_linter_net.remediation_hint`.

Hints are guidance, not edits. They never create code changes, rewrite YAML,
baselines, or reviewed public-API snapshots, and SARIF output does not emit
`fixes` for them.

The category is a finite machine-readable token:

- `move_code` — move code to an already-evidenced architectural owner;
- `depend_on_abstraction` / `invert_dependency` — only when policy evidence
  already establishes the required abstraction or direction;
- `introduce_adapter` / `use_declared_port` — use an already-declared adapter
  or port seam;
- `fix_classification` / `fix_policy_input` — correct role, location,
  coverage, build, or policy input facts before changing structure;
- `narrow_exception` — a precise exception may need explicit review;
- `remove_or_replace_dependency` — remove a forbidden dependency when no
  approved seam is evidenced;
- `review_contract` — existing evidence is insufficient to prescribe a safe
  structural repair.

Every populated hint carries its category, summary, stable contract identity,
structured canonical finding identity, ordered evidence, optional expected seam
or direction, caveat, and review flag. The structured identity keeps
same-named subjects from different assemblies distinct; never use the display
text as identity.

For example, a port-boundary result with a declared seam includes compact data
like this:

```json
"remediation_hint": {
  "category": "use_declared_port",
  "summary": "Use the declared port seam instead of the direct cross-context dependency.",
  "contract_identity": "orders-boundary",
  "finding_identity": { "source_assembly": "App", "source_type": "App.Orders.OrderService" },
  "evidence": [
    { "kind": "evidence_kind", "value": "direct_edge" },
    { "kind": "expected_seam", "value": "role:Port, name: Orders" }
  ],
  "expected_seam_or_direction": "role:Port, name: Orders",
  "caveat": "The declared seam is the only supported alternative; do not add a broad exception.",
  "requires_review": false
}
```

Treat a hint as an evidence-backed starting point, not permission to make the
policy easier to satisfy. In particular, do not respond by adding broad ignores
or exclusions, expanding allow-lists merely to permit the observed edge,
reducing governed scope, baselining new debt, changing `strict` to `audit`, or
deleting a contract without evidence that it is wrong. When no safe specialized
hint is present, keep the existing diagnostic unchanged and review the contract
and policy context.

When enabled and non-empty, supplemental diagnostics are emitted in dedicated sections:

- `Coverage findings:` for namespace, rule-input, project, assembly, and dependency-edge coverage contracts;
- `Coverage summary:` for the per-contract coverage counts described in [Coverage contracts](../contracts/coverage.md#coverage-summary) — printed whenever any coverage contract ran, regardless of `analysis.coverage` severity;
- `Unmatched ignored violations:` for stale baseline/ignore entries;
- `Policy consistency findings:` for internal contradictions in the policy document.

Example supplemental section:

```text
Coverage findings:
- [feature-namespace-coverage] [feature-namespace-coverage] MyApp.Features.Payments -> uncovered namespace: MyApp.Features.Payments.PaymentsRepresentative
- [layer-edge-coverage] [layer-edge-coverage] MyApp.Cli.Commands -> MyApp.Testing.Fixtures -> uncovered dependency edge: MyApp.Cli.Commands.DeployCommand

Coverage summary:
- [feature-namespace-coverage] [feature-namespace-coverage] scope: namespace covered=4 excluded=1 uncovered=1 stale=0 unknown=0
    uncovered: MyApp.Features.Payments (MyApp.Features.Payments.PaymentsRepresentative)
- [layer-edge-coverage] [layer-edge-coverage] scope: dependency_edge covered=1 excluded=0 uncovered=1 stale=0 unknown=0
    uncovered: MyApp.Cli.Commands -> MyApp.Testing.Fixtures (MyApp.Cli.Commands.DeployCommand)
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

JSON output is written to stdout by default. Use `--report json=<path>` to write JSON to a file while routing a different format to stdout. When `--timings` is also enabled, timings are written to stderr so stdout remains parseable.

For baseline-configuration and public-API snapshot or build-state failures after a command has selected `--format json`, stdout remains one parseable JSON error document and the existing exit code is retained. Those newly unified paths use a common envelope containing `schema_version: 1`, `status: "error"`, `kind: "command_error"`, and an `error` object with `category`, `message`, and typed `details` when the command has diagnostic evidence. Validation, policy-check, graph, and explain preserve their existing structured JSON error documents. Human output remains on stderr for the same failures.

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
    },
    {
      "contract": "layer-edge-coverage",
      "contract_id": "layer-edge-coverage",
      "source": "MyApp.Cli.Commands -> MyApp.Testing.Fixtures",
      "forbidden_namespace": "uncovered dependency edge",
      "forbidden_references": ["MyApp.Cli.Commands.DeployCommand"]
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
      ],
      "stale_items": [],
      "unknown_items": [],
      "covered_items": [
        { "item": "MyApp.Features.Billing", "evidence": "MyApp.Features.Billing.BillingRepresentative" }
      ]
    },
    {
      "contract": "layer-edge-coverage",
      "contract_id": "layer-edge-coverage",
      "scope": "dependency_edge",
      "counts": { "covered": 1, "excluded": 0, "uncovered": 1, "stale": 0, "unknown": 0 },
      "excluded_items": [],
      "uncovered_items": [
        { "item": "MyApp.Cli.Commands -> MyApp.Testing.Fixtures", "evidence": "MyApp.Cli.Commands.DeployCommand" }
      ],
      "stale_items": [],
      "unknown_items": [],
      "covered_items": [
        { "item": "MyApp.Cli.Commands -> MyApp.Core.Deployment", "evidence": "MyApp.Cli.Commands.DeployCommand" }
      ]
    }
  ]
}
```

Every `coverage_summary` entry always includes `uncovered_items`, `stale_items`, `unknown_items`, and `covered_items`; only the array(s) matching the contract's `scope` are ever non-empty (`uncovered_items` for `scope: namespace`/`scope: project`/`scope: assembly`/`scope: dependency_edge`; `unknown_items` additionally for `scope: project`; `stale_items`/`unknown_items` for `scope: rule_input`) — they are kept distinct so a `stale` finding can't be mistaken for an `unknown` one or vice versa. `covered_items` names the specific units found covered with supporting evidence, for every scope — this is the only positive evidence of coverage in the JSON output; a unit's absence from every list (including `covered_items`) does not mean it is covered, it means no configured contract's scope/roots include that unit at all.

`coverage_summary` is always present as an array (empty when no coverage contracts ran) and is reported independent of `analysis.coverage` severity, since it summarizes state rather than gating the run. See [Coverage contracts — Coverage summary](../contracts/coverage.md#coverage-summary) for the count semantics, including how `scope: rule_input` maps to `stale`/`unknown`.

Behavior for non-violation finding families is controlled separately:

- `analysis.coverage: error|warn|off` controls whether `coverage_findings` fail the run, report without failing, or are suppressed — this applies uniformly across every implemented coverage scope (`namespace`, `rule_input`, `project`, `assembly`, `dependency_edge`), not just namespace/rule-input coverage.
- `analysis.policy_consistency: error|warn|off` controls whether `policy_consistency_findings` fail the run, report without failing, or are suppressed.
- `analysis.unmatched_ignored_violations: error|warn|off` controls whether stale ignore entries fail the run, report without failing, or are suppressed.

## SARIF output

Use SARIF output to feed violations into GitHub code scanning or other standard static-analysis viewers:

```bash
arch-linter-net --mode strict --format sarif > architecture-violations.sarif
```

SARIF output is a single SARIF 2.1.0 document (`version: "2.1.0"`, with a `$schema` pointing at the SARIF 2.1.0 schema) containing one `run`. Use `--report sarif=<path>` to write SARIF to a file directly instead of redirecting stdout (also works in PowerShell):

- `tool.driver.name` identifies the CLI, and `tool.driver.rules` lists every contract ID that produced a result, deduplicated by rule ID.
- Each `result.ruleId` is the violating contract's ID (or a normalized fallback derived from its name when no ID is set).
- Each `result.level` is `error` in `--mode strict` and `warning` in `--mode audit` — SARIF severity reflects the run's mode uniformly, not a per-contract setting.
- Method-body violations (source-scanned forbidden calls) include a `physicalLocation` with the source file and line number. Every other violation kind (dependency/layer, external-dependency, package-dependency, type-placement, IL-scanned method-body calls, etc.) includes a `logicalLocations` entry naming the type, namespace, assembly, or package involved, since no file position is available for those checks.

**SARIF output only covers violations and cycles.** Coverage findings, unmatched-ignored violations, and policy-consistency findings — the same supplemental categories shown in the human and JSON output above — are *not* included in SARIF results, since they describe the policy configuration itself rather than a violation found in scanned code. If a run fails (exit code `1`) because of one of those categories with zero violations or cycles, the SARIF document will report an empty `results` array even though the run failed. Use `--format json` (or human output) alongside SARIF if you need visibility into those categories in CI.

## CI artifact pattern

```yaml
- name: Validate architecture
  run: arch-linter-net --strict --report json=architecture-violations.json

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
  run: arch-linter-net --audit --report json=architecture-audit.json
```

For combined strict + audit with multi-sink output:

```yaml
- name: Validate architecture (strict + audit)
  run: arch-linter-net --mode strict,audit \
    --report json=architecture-results.json \
    --report sarif=architecture-results.sarif

- name: Upload architecture results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: architecture-results
    path: |
      architecture-results.json
      architecture-results.sarif
```
