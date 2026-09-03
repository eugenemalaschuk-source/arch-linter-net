# Single-tool architecture governance workflow

ArchLinterNet v0.8 is designed around one product boundary: one packed `arch-linter-net` CLI owns the static architecture-governance semantics, while CI only invokes the CLI and transports the canonical artifacts it produces.

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

This guide is the primary end-to-end path. The linked reference pages remain authoritative for field-level details.

## 1. Pin the CLI

Prefer a repository-local .NET tool so local development and CI use the same package version:

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --version
```

For internal dogfooding, `0.8.0-main.N` packages are installable development builds from GitHub Packages. They are not release candidates, stable releases, or public-release authority. The public release workflow builds and proves its own immutable candidate before NuGet.org publication.

## 2. Declare and statically check the policy

Create a repository-owned root policy and validate its static shape before loading assemblies:

```bash
dotnet arch-linter-net policy check \
  --policy architecture/arch.yml \
  --format json
```

`policy check` validates schema, imports, identifiers and static configuration. Fact-dependent checks may be deferred; exit `0` therefore means the policy is statically valid, not that the repository architecture is clean.

Use policy `version: 1` to retain compatibility waiver defaults. Policy `version: 2` opts into strict structured-waiver defaults. See [Structured waivers](../policy-format/structured-waivers.md).

## 3. Prepare build evidence and validate

Normal validation does not silently rebuild. Either prepare outputs yourself:

```bash
dotnet restore
dotnet build Example.Product.slnx --no-restore
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

or explicitly let the CLI prepare and verify build state:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --mode strict,audit \
  --ensure-built \
  --report json=artifacts/architecture-results.json \
  --report sarif=artifacts/architecture-results.sarif
```

The combined strict/audit invocation uses one immutable analysis snapshot. Adding report sinks renders completed outcomes; it does not rerun architecture analysis.

## 4. Read applicability and completeness before trusting zero findings

ArchLinterNet separates conformance from evaluability. A policy can have zero ordinary violations and still be unassessable because required inputs were missing, empty, ambiguous, stale or outside the declared governed universe.

Use architecture coverage contracts for namespaces, projects, assemblies, dependency edges, rule inputs and semantic roles. Coverage makes policy omissions observable; it is not a quality percentage.

A trustworthy clean result requires both the relevant contracts to pass and the required applicability/completeness evidence to be evaluable. Never interpret an absent applicability record, missing policy inventory, empty topology universe or missing required SARIF artifact as a clean zero.

## 5. Review declared topology

Declare `topology` when the repository needs an explicit semantic component map. Start with `partial` during migration and move to `exhaustive` when every required first-party subject must be mapped or deliberately reviewed out of scope.

Use the native review workflow:

```bash
dotnet arch-linter-net topology capture \
  --policy architecture/arch.yml \
  --output artifacts/topology-current.json \
  --ensure-built

dotnet arch-linter-net topology diff \
  --declared architecture/arch.yml \
  --observed artifacts/topology-current.json

dotnet arch-linter-net topology verify \
  --policy architecture/arch.yml \
  --ensure-built
```

Keep mapped, unmapped, ambiguous, reviewed-out-of-scope and stale evidence distinct. In exhaustive mode, a new required first-party subject that cannot be mapped exactly is incomplete governance evidence, not an implicit new component and not a clean result. Mapping ratios are completeness transparency only; ArchLinterNet does not turn them into a repository quality score.

See [Topology review workflow](topology-review-workflow.md) and [Declared topology](../policy-format/declared-topology.md).

## 6. Govern visible contract surfaces

Dependency rules answer whether code references another boundary. Contract-surface exposure rules answer whether selected exported types expose forbidden types through their visible CLR signatures.

Compose reviewed public API membership with recursive exposure checks rather than replacing semantic roles with an `api` role:

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

Recursive exposure follows visible nested generic, tuple, array and wrapper positions and reports deterministic exposure paths. It is static metadata/signature governance; it does not claim runtime serialization, routing, DI or arbitrary semantic data-flow correctness.

See [Contract-surface exposure](../contracts/contract-surface-exposure.md).

## 7. Keep finding debt and waiver debt separate

A migration baseline records reviewed existing findings. A structured waiver is a manually authored exception with its own lifecycle and exact target identity. Intended scope exclusions are neither of those.

For persistent finding debt, use the baseline lifecycle and the read-only CI gate:

```bash
dotnet arch-linter-net baseline verify \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml

dotnet arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode all
```

For policy edits, export base/current policy contexts and review weakening:

```bash
dotnet arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > current-policy.json

dotnet arch-linter-net policy weakening \
  --base-context base-policy.json \
  --current-context current-policy.json
```

The canonical policy inventory counts effective controls once and projects explicit waiver lifecycle debt without inflating counts because a source set or runtime selector fans one authored control out over multiple subjects.

See [Migration baselines](migration-baselines.md) and [Structured waivers](../policy-format/structured-waivers.md).

## 8. Measure first, then set budgets

Declare architecture metrics, inspect their current value and contributors, and only then choose a reviewed budget:

```bash
dotnet arch-linter-net measure \
  --policy architecture/arch.yml \
  --format json
```

Absolute budgets constrain current values. Baseline-relative budgets support no-worse-than-baseline and bounded-delta ratchets, optionally combined with an absolute hard cap. Missing, ambiguous or incomplete metric scope is unassessable; ArchLinterNet does not substitute an artificial low value.

Metric baselines are scalar measurement evidence and remain separate from finding baselines and waiver debt. ArchLinterNet exposes no arbitrary metric formulas or universal repository score.

See [Architecture metrics](../policy-format/architecture-metrics.md).

## 9. Bind required external SARIF evidence

ArchLinterNet can consume a repository-local SARIF artifact through a policy-declared `external_evidence` requirement. The external analyzer still executes separately; ArchLinterNet owns trust validation, filtering, normalization and applicability after the file exists.

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --external-evidence "id=static-analysis,path=evidence/static-analysis.sarif" \
  --evidence-repository "$GITHUB_SERVER_URL/$GITHUB_REPOSITORY" \
  --evidence-revision "$GITHUB_SHA" \
  --evidence-scope "ci"
```

The logical evidence ID, expected tool/run and required repository/revision/scope bindings come from the policy and invocation. A valid successful zero-result SARIF artifact is evaluable evidence. Missing, malformed, failed, wrong-revision, wrong-scope or otherwise untrusted required evidence is unassessable. Filename, modification time, artifact order and CI job name are never freshness proof.

See [External evidence](../policy-format/external-evidence.md).

## 10. Capture and compare architecture change

Use canonical snapshots when reviewers need a bounded base/current architecture delta:

```bash
dotnet arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --output artifacts/architecture-current.json

dotnet arch-linter-net change report \
  --base artifacts/architecture-base.json \
  --current artifacts/architecture-current.json \
  --execution-context pr-123
```

Change evidence is distinct from current-state Health. A resolved finding is improvement evidence; a new finding, broadened waiver or policy weakening must not be hidden by unrelated healthy dimensions.

## 11. Produce canonical Architecture Health

Project the non-compensating Health model from the current assessment:

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

Read `gate` and `health` separately. `gate` is `pass`, `fail` or `unassessable`; `health` is `healthy`, `debt`, `degrading`, `failing` or `unassessable`. Reviewed existing debt can therefore produce `gate: pass` with `health: debt`. Missing required evidence cannot be compensated by healthy dimensions.

See [Architecture Health](../reference/architecture-health.md).

## 12. Render the reviewer report and Health badge

The PR Markdown renderer consumes canonical artifacts; it does not run analysis again:

```bash
dotnet arch-linter-net report pr \
  --health artifacts/architecture-health.json \
  --change artifacts/architecture-change.json \
  --output artifacts/architecture-pr-report.md
```

Generate the real Architecture Health badge payload from the same canonical Health evidence:

```bash
dotnet arch-linter-net badge architecture-health \
  --input artifacts/architecture-health.json \
  --output artifacts/architecture-health-badge.json
```

The badge carries Health plus canonical explicit-ignore and effective-rule counts. Missing evidence must remain unknown/unassessable; CI must not fabricate zeroes or retain an older healthy payload as current.

## 13. CI is transport, not a second governance engine

A recommended split is:

```text
pull request
  -> complete authoritative ArchLinterNet validation
  -> canonical JSON/SARIF/change/Health artifacts
  -> CLI-generated PR Markdown and badge payload
  -> required merge gate

main
  -> focused generic quality telemetry where desired
  -> independent development-package publication where desired
  -> trusted promotion of already-generated PR evidence only after exact merged-tree proof
```

A privileged publisher may validate repository/PR/head/run/schema/size/hash metadata and transport inert Markdown or badge JSON. It must not recompute PASS/FAIL/UNASSESSABLE, Health, waiver debt, rule counts, report sections or badge colors/messages.

ArchLinterNet's own repository follows this split: complete validation is PR-authoritative; ordinary main quality refreshes focused coverage/Sonar/Codecov telemetry; `main.N` packages are independent dogfood builds. Public MkDocs/GitHub Pages deployment remains owned by a real `release-nuget.yml` publication with `publish: true`.

## Interpreting the five Health paths

| Gate | Health | Typical meaning |
| --- | --- | --- |
| `pass` | `healthy` | Current evidence is assessable, required controls pass, and explicit waiver debt is zero. |
| `pass` | `debt` | Current gate passes but reviewed finding debt and/or valid explicit waiver debt remains. |
| `pass` or `fail` depending on the owning gate | `degrading` | New debt, weakening, broadened/new waiver or metric regression shows architectural movement in the wrong direction. |
| `fail` | `failing` | A current blocking architecture requirement fails, including blocking invalid/expired waiver state where applicable. |
| `unassessable` | `unassessable` | Required applicability, topology, build, metric, baseline or external evidence is missing, ambiguous, stale or otherwise not trustworthy. |

These states are deterministic and non-compensating. There is no weighted score, letter grade or universal architecture percentage.

## v0.7 adoption summary

Existing v0.7-compatible policies do not need to adopt every v0.8 capability at once. A safe sequence is:

1. pin and run the new CLI against the unchanged policy;
1. keep `version: 1` while confirming compatibility behavior;
1. migrate legacy ignores to structured waivers, then deliberately move to policy `version: 2` for strict lifecycle defaults;
1. introduce partial topology before claiming exhaustive coverage;
1. add contract-surface exposure only to reviewed published/protected surfaces;
1. measure before authoring metric budgets;
1. bind external SARIF only after producer repository/revision/scope metadata is reliable;
1. add change, Health, PR Markdown and badge projections;
1. remove repository-owned scripts that were previously recalculating architecture report/counting semantics.

The full migration checklist is in [Adopt or upgrade](upgrading.md#v07-to-v08-adoption).
