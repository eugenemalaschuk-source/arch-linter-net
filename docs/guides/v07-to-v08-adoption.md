# Adopt v0.8 from v0.7

ArchLinterNet v0.8 is intentionally adoptable in stages. Existing v0.7-compatible policies can keep their current behavior while teams opt into topology, exposure, metrics, external evidence, waiver lifecycle, Health, report, and badge surfaces deliberately.

This page is the migration path for an existing adopter. For greenfield setup, start with the [complete single-tool workflow](single-tool-workflow.md).

## 1. Upgrade the tool before changing policy

Keep the currently reviewed policy and baseline unchanged, update the repository-local tool, and prove the old workflow still behaves as expected:

```bash
dotnet tool update ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --version
dotnet arch-linter-net policy check --policy architecture/arch.yml
dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

Record the reviewed package version and use that exact version for both base and candidate evidence. Do not weaken a rule simply because the newer CLI exposes previously hidden incomplete or stale evidence.

## 2. Keep v1 compatibility until waiver migration is ready

Policy `version: 1` preserves compatibility waiver defaults. You can therefore adopt most v0.8 features without immediately converting every legacy manual ignore.

Use this period to inspect canonical waiver lifecycle and policy-inventory output. Under compatibility semantics, matcher-only ignores remain visible as `metadata_incomplete` debt and retain their prior pass/fail behavior. Migrate each still-legitimate manual ignore to a structured waiver with stable ID, exact target fingerprint, reason, owner, issue/remediation, introduced date, and expiry.

When all manual exceptions are ready for strict lifecycle behavior, move the root policy to:

```yaml
version: 2
```

A version-2 policy can explicitly use `analysis.waiver_lifecycle_profile: compatibility` during a reviewed transition, but the target state is strict lifecycle governance. See [Structured waivers](../policy-format/structured-waivers.md).

## 3. Keep baseline finding debt separate

Your existing migration baseline remains the reviewed ledger of known normalized findings. Structured waiver debt is separate and should not be folded into the baseline just to preserve one generic debt count.

Run the read-only baseline checks after the CLI upgrade:

```bash
dotnet arch-linter-net baseline verify \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml

dotnet arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode all
```

If identity changes require a baseline migration, use the explicit baseline lifecycle commands and review the resulting diff. CI must not regenerate accepted debt automatically.

## 4. Add topology in partial mode first

Do not claim exhaustive topology before the repository has been reviewed as a complete bounded subject universe.

Capture observations:

```bash
dotnet arch-linter-net topology capture \
  --policy architecture/arch.yml \
  --subject-kind assembly \
  --ensure-built \
  --format json \
  --output artifacts/topology-capture.json
```

Hand-author the reviewed declaration, initially with `mode: partial`, then use:

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

Move to `mode: exhaustive` only after every required first-party subject is mapped exactly or explicitly reviewed out of scope. New unmapped or ambiguous required subjects then become incomplete/unassessable governance evidence rather than silently escaping the declaration.

## 5. Add visible contract-surface governance deliberately

Existing dependency rules do not need to be replaced. Add `contract_surface_exposure` where a published/protected CLR-visible surface must not disclose domain, persistence, transport, editor-only, or other forbidden types.

When you already use `public_api_surface`, reuse that reviewed membership as the source. Do not replace a type's existing semantic role with an API-only role just to make exposure rules work.

Start in audit if you expect existing leakage, then promote the contract to strict after reviewing or fixing the findings. Runtime serialization, endpoint routing and arbitrary semantic data flow remain outside this static contract.

## 6. Measure before introducing budgets

Declare metrics and inspect them before adding limits:

```bash
dotnet arch-linter-net measure \
  --policy architecture/arch.yml \
  --format json
```

Then choose one of the delivered budget styles:

- absolute `minimum`/`maximum`;
- `baseline_mode: no_worse_than_baseline`;
- `baseline_mode: max_delta` plus `max_delta`;
- optional absolute `maximum` as a hard cap on a relative budget.

Keep scalar metric baselines distinct from finding baseline debt and waiver debt. An incomplete measurement scope is unassessable, not a trustworthy low number.

## 7. Bind external SARIF only when freshness evidence is explicit

If your v0.7 pipeline already runs analyzers, keep those producer steps. Replace repository-owned SARIF interpretation with ArchLinterNet's first-class binding once the producer can supply reliable repository/revision/scope identity.

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --external-evidence "id=static-analysis,path=evidence/static-analysis.sarif" \
  --evidence-repository "$GITHUB_SERVER_URL/$GITHUB_REPOSITORY" \
  --evidence-revision "$GITHUB_SHA" \
  --evidence-scope "ci"
```

A successful current-context zero-result artifact is valid evidence. A missing, failed, malformed, stale, wrong-revision, or wrong-scope required artifact is unassessable. Do not carry forward a v0.7 script that treats filename, modification time, job name, or mere file presence as freshness proof.

## 8. Add policy weakening and architecture change evidence

Start in the candidate checkout, create one absolute artifact directory, and point `BASE_WORKTREE` at a reviewed base worktree. Run both states with the exact same v0.8 CLI executable.

```bash
ARTIFACTS="$(pwd)/artifacts"
BASE_WORKTREE="../architecture-base"
mkdir -p "$ARTIFACTS"

(
  cd "$BASE_WORKTREE"

  arch-linter-net policy context \
    --policy architecture/arch.yml \
    --format json > "$ARTIFACTS/policy-base.json"

  baseline_args=()
  if [[ -f architecture/baseline.arch.yml ]]; then
    baseline_args=(--baseline architecture/baseline.arch.yml)
  fi

  arch-linter-net change snapshot \
    --policy architecture/arch.yml \
    --mode strict \
    "${baseline_args[@]}" \
    --ensure-built \
    --output "$ARTIFACTS/architecture-base.json"
)

arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > "$ARTIFACTS/policy-current.json"

baseline_args=()
if [[ -f architecture/baseline.arch.yml ]]; then
  baseline_args=(--baseline architecture/baseline.arch.yml)
fi

arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --mode strict \
  "${baseline_args[@]}" \
  --ensure-built \
  --output "$ARTIFACTS/architecture-current.json"

arch-linter-net policy weakening \
  --base-context "$ARTIFACTS/policy-base.json" \
  --current-context "$ARTIFACTS/policy-current.json"

arch-linter-net change report \
  --base "$ARTIFACTS/architecture-base.json" \
  --current "$ARTIFACTS/architecture-current.json" \
  --execution-context pr-123 \
  --format json \
  --output "$ARTIFACTS/architecture-change.json"
```

Base and candidate baselines are selected independently. This is where a new/broadened waiver, relaxed exclusion, removed control, new finding debt, or resolved finding becomes explicit change evidence instead of being hidden inside a current-state pass/fail result.

## 9. Adopt Architecture Health

Once current validation, baseline, waiver, topology, metrics, and required external evidence are trustworthy, project Health from the same authority inputs. Include base/current policy contexts so weakening is represented, and repeat every required external-evidence binding because a new CLI process does not inherit evidence from an earlier validation command.

```bash
dotnet arch-linter-net health \
  --policy architecture/arch.yml \
  "${baseline_args[@]}" \
  --base-context "$ARTIFACTS/policy-base.json" \
  --current-context "$ARTIFACTS/policy-current.json" \
  --mode strict \
  --ensure-built \
  --execution-context pr-123 \
  --external-evidence "id=static-analysis,path=evidence/static-analysis.sarif" \
  --evidence-repository "$GITHUB_SERVER_URL/$GITHUB_REPOSITORY" \
  --evidence-revision "$GITHUB_SHA" \
  --evidence-scope "ci" \
  --format json > "$ARTIFACTS/architecture-health.json"
```

Omit the external-evidence options only when the policy declares no such requirement. Interpret `gate` and `health` separately:

- pass + healthy: all required evidence is assessable, configured authorities pass, and reviewed finding debt, explicit waiver debt, new debt, weakening, and metric regression are absent;
- pass + debt: reviewed debt remains but the current gate passes;
- degrading: regression/change evidence exists, with the owning authority still deciding whether the independent gate blocks;
- fail + failing: a blocking current requirement fails;
- unassessable: required evidence cannot be trusted as complete/current.

See [Architecture Health](../reference/architecture-health.md).

## 10. Replace repository-owned reporting logic

If v0.7 CI used Python, JavaScript, shell or PowerShell to count rules, classify debt, render architecture sections, or decide badge color, retire that logic after moving to the v0.8 projections.

Use the JSON change artifact created in step 8 together with the Health artifact carrying the same `pr-123` execution context and `strict` selected mode:

```bash
dotnet arch-linter-net report pr \
  --health "$ARTIFACTS/architecture-health.json" \
  --change "$ARTIFACTS/architecture-change.json" \
  --output "$ARTIFACTS/architecture-pr-report.md"
```

Generate the Health badge payload separately:

```bash
dotnet arch-linter-net badge architecture-health \
  --input "$ARTIFACTS/architecture-health.json" \
  --output "$ARTIFACTS/architecture-health-badge.json"
```

CI may validate repository/PR/head/run/schema/size/hash transport metadata and publish these finished bytes. It should not reconstruct their semantics.

## 11. Keep PR authority and main responsibilities separate

For ArchLinterNet's own repository, the intended pattern is:

```text
PR
  complete authoritative validation
  -> canonical architecture artifacts
  -> CLI-generated report/badge payload
  -> required merge gate

main quality
  focused Linux coverage
  -> canonical coverage evidence
  -> SonarCloud + Codecov refresh

main packages
  development version + monotonic run
  -> 0.8.0-main.N
  -> GitHub Packages
```

The PR remains the complete architecture merge authority. Generic main telemetry does not become Architecture Health evidence, and an ordinary merge does not rerun the full architecture matrix merely to refresh a report or badge.

## 12. Understand `main.N` correctly

`0.8.0-main.N` is a development/dogfood package identity. It is not an RC and is not the public release candidate.

The public release workflow (`release-nuget.yml`) creates and validates a fresh immutable candidate, verifies its package/provenance evidence, and only then may publish to NuGet.org and create the GitHub Release. No `main.N` package is promoted or renamed into a stable release.

Package visibility is not the trust boundary; exact version, source identity, package-set integrity, deterministic restore, and the release workflow's authorization evidence are. The repository retains only the newest five complete four-package `main.N` sets; stable and other prerelease families are outside that retention selection. See [Release process](../reference/release-process.md) for exact package-set and provenance verification.

## 13. Documentation publication behavior

Ordinary `main` workflows do not deploy MkDocs/GitHub Pages. Public documentation is deployed by the real public release workflow only when it runs with `publish: true`.

Therefore source documentation on `main` may temporarily be newer than the currently published stable documentation site. Do not infer public release status from documentation source changes alone.

## Migration completion checklist

The v0.7 -> v0.8 migration is complete when:

- the pinned v0.8 CLI runs the repository's unchanged baseline behavior correctly;
- any retained manual ignores have a deliberate structured-waiver migration state;
- policy v2 strict lifecycle is enabled when ready;
- topology completeness claims match the reviewed repository universe;
- contract-surface exposure protects the intended visible boundaries;
- metric budgets are based on inspected canonical measurements;
- required SARIF is bound to current repository/revision/scope evidence;
- finding debt, waiver debt, weakening, metrics, topology, and external evidence remain distinct in Health;
- PR Markdown and the real Health badge come from CLI-owned canonical artifacts;
- CI contains no second architecture-governance/counting/reporting implementation;
- `main.N` remains dogfood distribution and `release-nuget.yml` remains public-release authority.
