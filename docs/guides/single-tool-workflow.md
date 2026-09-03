# Single-tool architecture governance workflow

ArchLinterNet v0.8 has one product boundary: one packed `arch-linter-net` CLI owns static architecture-governance semantics, while CI invokes the CLI and transports the canonical artifacts it produces.

```text
install/pin
  -> declare policy
  -> policy check
  -> analyze + prove applicability/completeness
  -> validate topology and visible contract surfaces
  -> govern finding debt, waiver debt, new debt and weakening
  -> measure architecture and enforce budgets
  -> bind required current-context SARIF evidence
  -> inspect architecture change
  -> Architecture Health
  -> PR Markdown / JSON / SARIF / Health badge
```

This is the primary end-to-end user path. The linked reference pages remain authoritative for individual fields and contract families.

## 1. Pin the CLI

Prefer a repository-local tool so development and CI use the same package version:

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --version
```

`0.8.0-main.N` packages are development/dogfood builds from GitHub Packages. They are not RCs, stable releases, or public-release authority. `release-nuget.yml` builds and proves a fresh immutable public candidate before NuGet.org publication.

## 2. Declare and statically check the policy

```bash
dotnet arch-linter-net policy check \
  --policy architecture/arch.yml \
  --format json
```

`policy check` validates schema, imports, identifiers and static configuration without claiming the analyzed architecture is clean. Fact-dependent checks can remain deferred.

Policy `version: 1` retains compatibility waiver defaults. Policy `version: 2` defaults to strict structured-waiver lifecycle governance. See [Structured waivers](../policy-format/structured-waivers.md).

## 3. Prepare build evidence and validate

Normal validation does not silently build. Either prepare outputs yourself or opt into CLI-owned preparation:

```bash
dotnet restore
dotnet build Example.Product.slnx --no-restore
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --mode strict,audit \
  --ensure-built \
  --report json=artifacts/architecture-results.json \
  --report sarif=artifacts/architecture-results.sarif
```

The combined strict/audit invocation uses one immutable analysis snapshot. Report sinks render completed outcomes; they do not rerun analysis.

## 4. Prove applicability and completeness

Zero ordinary findings are not enough when required analysis inputs are missing, empty, ambiguous, stale or outside the governed universe. Architecture coverage contracts make omissions observable across namespaces, projects, assemblies, dependency edges, rule inputs and semantic roles.

A required missing applicability record, empty exhaustive topology universe, incomplete recursive exposure universe, incomplete metric scope, or missing required SARIF artifact must remain unassessable rather than becoming a clean zero. Coverage and mapping ratios are transparency evidence, not quality percentages.

## 5. Review declared topology

Start with observation, then hand-author the architecture decision. Capture requires an explicit subject kind:

```bash
dotnet arch-linter-net topology capture \
  --policy architecture/arch.yml \
  --subject-kind assembly \
  --ensure-built \
  --format json \
  --output artifacts/topology-capture.json
```

After reviewing the observations and adding `topology` to the policy, use native diff and verify:

```bash
dotnet arch-linter-net topology diff \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built \
  --format json \
  --output artifacts/topology-diff.json

dotnet arch-linter-net topology verify \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built \
  --format json
```

Use `partial` while the declared map is being adopted. Move to `exhaustive` only when every required first-party subject is expected to map exactly or be explicitly reviewed out of scope. Keep mapped, unmapped, ambiguous, reviewed-out-of-scope and stale evidence distinct.

See [Topology review workflow](topology-review-workflow.md) and [Declared topology](../policy-format/declared-topology.md).

## 6. Govern visible contract surfaces

Dependency rules answer whether code references another boundary. Contract-surface exposure rules answer whether selected exported types expose forbidden types through visible CLR signatures.

A reviewed public API surface can be reused without replacing primary semantic roles:

```yaml
contracts:
  strict_public_api_surface:
    - id: orders-api
      name: orders-api
      assemblies: [Example.Orders.Api]
      surface_selector:
        has_attribute: Example.Orders.Api.PublicApiContractAttribute
      api_snapshot: architecture/api/orders-api.public-api.txt
      reason: Reviewed published API membership.

  strict_contract_surface_exposure:
    - id: orders-api-no-persistence
      name: orders-api-no-persistence
      source:
        public_api_surface: orders-api
      forbidden:
        - namespace: Example.Orders.Persistence
      reason: Published signatures must not expose persistence models.
```

Recursive exposure follows visible nested generic, tuple, array and wrapper positions and reports deterministic exposure paths. It is static metadata/signature governance; runtime serialization, routing, DI and arbitrary semantic data flow are outside this contract.

See [Contract-surface exposure](../contracts/contract-surface-exposure.md).

## 7. Keep debt categories separate

A migration baseline records reviewed existing findings. A structured waiver is a policy-authored exception with its own exact target identity and lifecycle. Intended topology/coverage exclusions are policy scope, not waiver debt.

Use the baseline and no-new-debt workflow:

```bash
dotnet arch-linter-net baseline verify \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml

dotnet arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode all
```

Review policy relaxation separately:

```bash
dotnet arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > current-policy.json

dotnet arch-linter-net policy weakening \
  --base-context base-policy.json \
  --current-context current-policy.json
```

The canonical policy inventory counts effective authored controls once and projects explicit waiver debt without source-set/runtime fan-out inflating the rule count.

See [Migration baselines](migration-baselines.md) and [Structured waivers](../policy-format/structured-waivers.md).

## 8. Measure first, then set budgets

```bash
dotnet arch-linter-net measure \
  --policy architecture/arch.yml \
  --format json
```

Inspect the value, effective scope and contributors before authoring a budget. Delivered budgets support absolute bounds and baseline-relative no-worse-than/delta ratchets with an optional hard cap. Incomplete measurement scope is unassessable, not an artificial low value. Metric baselines remain distinct from finding baselines and waiver debt.

See [Architecture metrics](../policy-format/architecture-metrics.md).

## 9. Bind required external SARIF evidence

The analyzer executes outside ArchLinterNet and writes a repository-local SARIF file. ArchLinterNet then owns bounded trust, filtering, normalization and applicability:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --external-evidence "id=static-analysis,path=evidence/static-analysis.sarif" \
  --evidence-repository "$GITHUB_SERVER_URL/$GITHUB_REPOSITORY" \
  --evidence-revision "$GITHUB_SHA" \
  --evidence-scope "ci"
```

A successful current-context zero-result artifact is valid evidence. Missing, malformed, failed, wrong-revision, wrong-scope or otherwise untrusted required evidence is unassessable. Filename, mtime, artifact order and CI job name are never freshness proof.

See [External evidence](../policy-format/external-evidence.md).

## 10. Inspect architecture change

```bash
dotnet arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --output artifacts/architecture-current.json

dotnet arch-linter-net change report \
  --base artifacts/architecture-base.json \
  --current artifacts/architecture-current.json \
  --execution-context pr-123
```

Change evidence stays distinct from current-state Health. Resolved findings are improvement evidence; new findings, broadened/new waivers and policy weakening must not disappear behind unrelated healthy dimensions.

## 11. Produce Architecture Health

```bash
dotnet arch-linter-net health \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode strict \
  --ensure-built \
  --execution-context pr-123 \
  --format json \
  > artifacts/architecture-health.json
```

Read `gate` and `health` separately:

- gate: `pass | fail | unassessable`;
- health: `healthy | debt | degrading | failing | unassessable`.

Reviewed debt can therefore coexist with `gate: pass` / `health: debt`. Missing required evidence cannot be compensated by healthy dimensions. There is no weighted score, letter grade or universal architecture percentage.

See [Architecture Health](../reference/architecture-health.md).

## 12. Render reviewer artifacts

The PR renderer consumes canonical Health and change artifacts; it does not rerun analysis:

```bash
dotnet arch-linter-net report pr \
  --health artifacts/architecture-health.json \
  --change artifacts/architecture-change.json \
  --output artifacts/architecture-pr-report.md
```

Generate the real Health badge payload from canonical Health evidence:

```bash
dotnet arch-linter-net badge architecture-health \
  --input artifacts/architecture-health.json \
  --output artifacts/architecture-health-badge.json
```

The badge carries CLI-owned Health, explicit-ignore debt and effective-rule counts. Missing evidence remains unknown/unassessable; CI must not fabricate zeroes or retain an older healthy payload as current.

## 13. Keep CI as transport

A recommended split is:

```text
pull request
  -> complete authoritative ArchLinterNet validation
  -> canonical architecture artifacts
  -> CLI-generated PR Markdown and badge payload
  -> required merge gate

main
  -> focused generic quality telemetry where desired
  -> independent development-package publication where desired
  -> trusted promotion of ready PR evidence only after exact merged-tree proof
```

A privileged publisher may validate repository/PR/head/run/schema/size/hash transport metadata and move inert Markdown or badge JSON. It must not recompute PASS/FAIL/UNASSESSABLE, Health, waiver debt, effective controls, report sections or badge colors/messages.

ArchLinterNet's own repository keeps complete architecture validation PR-authoritative. Ordinary main quality refreshes focused coverage/Sonar/Codecov telemetry; `main.N` packages are independent dogfood builds. Public MkDocs/GitHub Pages deployment remains owned by a real `release-nuget.yml` publication with `publish: true`.

## Health paths at a glance

| Gate | Health | Typical meaning |
| --- | --- | --- |
| `pass` | `healthy` | Assessable current state, required controls pass, zero explicit waiver debt. |
| `pass` | `debt` | Reviewed finding debt and/or valid explicit waiver debt remains. |
| `pass` or `fail` | `degrading` | New debt, weakening, new/broadened waiver or metric regression shows movement in the wrong direction. |
| `fail` | `failing` | A current blocking architecture requirement fails. |
| `unassessable` | `unassessable` | Required applicability, topology, build, metric, baseline or external evidence is not trustworthy. |

## Moving from v0.7

Existing v0.7-compatible policies can adopt v0.8 incrementally. Keep `version: 1` while confirming compatibility, migrate legacy ignores to structured waivers before deliberately enabling v2 strict lifecycle defaults, introduce partial topology before exhaustive claims, measure before budgets, bind SARIF only after producer context is reliable, and replace repository-owned report/counting scripts with first-class Health/report/badge projections.

Follow the complete [v0.7 to v0.8 adoption guide](v07-to-v08-adoption.md).
