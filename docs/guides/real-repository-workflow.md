# Real-repository workflow

This guide shows how to adopt and operate ArchLinterNet against a real repository without turning scores, baselines, policy edits, or AI suggestions into automatic architecture decisions.

Use the workflow, not this repository's layer names. ArchLinterNet's own repository is referenced only where it provides reproducible evidence.

## 1. Pin the tool used by the repository

For normal development and CI, prefer a repository-local .NET tool manifest:

```bash
dotnet tool restore
dotnet arch-linter-net --version
```

When first adopting:

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
```

Commit `.config/dotnet-tools.json` and review version updates like any other dependency change. Do not rely on an arbitrary developer-global install as the repository's version policy.

Historical reproduction is different: use an isolated explicit tool version for the evidence being reproduced. That workflow is covered later in this guide.

## 2. Check the policy before architecture analysis

Start with static policy validation:

```bash
dotnet arch-linter-net policy check \
  --policy architecture/arch.yml \
  --format json
```

`policy check` validates syntax, imports, composition, references, and static configuration. It does **not** prove architecture compliance for checks that require project, assembly, or source facts.

If an AI agent or reviewer is about to edit architecture-sensitive code, export the effective policy context:

```bash
dotnet arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > policy-context.json
```

The context is planning/review evidence. It must not be used as a substitute for strict validation or as permission to weaken the policy.

## 3. Build trustworthy analysis inputs

You can build through the normal repository workflow and then validate:

```bash
dotnet build MyApp.sln
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

or opt in to ArchLinterNet's explicit build-state workflow:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built
```

`--ensure-built` builds the selected project graph once, verifies the build receipt, and then validates. It is never implicit. Add `--no-restore` when CI must fail closed instead of restoring.

The Apple Silicon self-dogfood build-lock failure tracked in [#639](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/639) is a **historical defect**, not the current behavior: it was fixed by [#648](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/648). Keep that distinction when reading old evidence or reproducing an older release.

## 4. Make strict validation reproducible in CI

A minimal local-tool gate is:

```yaml
- name: Restore tools
  run: dotnet tool restore

- name: Validate architecture
  run: dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

For machine consumers, prefer normalized JSON and an additional SARIF sink where applicable:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built \
  --format json \
  --report sarif=artifacts/architecture.sarif \
  > artifacts/architecture.json
```

Do not infer a clean run from an empty SARIF file alone: some coverage, policy-consistency, baseline, or build-state findings are represented in JSON rather than SARIF.

## 5. Baseline existing debt, not desired rules

When a strict rule expresses the target architecture but existing code already violates it, capture the current debt:

```bash
dotnet arch-linter-net baseline generate \
  --config architecture/arch.yml \
  --output architecture/baseline.arch.yml \
  --reason "Reviewed adoption baseline"
```

Then validate with the reviewed baseline:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode strict
```

A baseline is not an approval mechanism. Review entries, keep reasons meaningful, prune resolved debt, and do not weaken a contract merely to make the baseline smaller.

Use `baseline diff`, `verify`, `update`, `prune`, and `migrate` for lifecycle operations. See [Migration baselines](migration-baselines.md).

## 6. Add architecture coverage

Dependency rules can pass while new architecture is never mapped to a rule. Coverage contracts close that gap.

Current coverage scopes are:

- `namespace`
- `project`
- `assembly`
- `dependency_edge`
- `rule_input`
- `semantic_role`

A practical adoption often starts with namespace/project/assembly coverage and then adds dependency-edge, rule-input, or semantic-role coverage where those inventories matter.

Coverage runs as part of validation. For PR reporting, post-process strict JSON:

```bash
dotnet arch-linter-net coverage report \
  --input artifacts/architecture.json \
  --changed-files artifacts/changed-files.txt \
  --repo-root . \
  --output artifacts/architecture-coverage.md
```

See [Coverage contracts](../contracts/coverage.md).

## 7. Review architecture changes as complete snapshots

Changed files are useful for triage, but architecture compliance is a whole-analysis property. Capture complete snapshots from the base and candidate repository states:

```bash
# Base checkout/worktree
arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built --configuration Debug --framework net10.0 \
  --output ../base.architecture-change.json

# Candidate checkout
arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built --configuration Debug --framework net10.0 \
  --output current.architecture-change.json

arch-linter-net change report \
  --base ../base.architecture-change.json \
  --current current.architecture-change.json \
  --format json
```

For consumers that opt into a shared framework, the supported snapshot path is
`--ensure-built`. Use identical build-state selection for the base and candidate
snapshots; add `--no-restore` when the consumer has already restored its build
prerequisites and the workflow must remain offline.

A change report answers what changed between two complete results. It does not decide whether existing debt is accepted or whether a policy relaxation is appropriate.

## 8. Review policy weakening separately

Export policy context from both repository states:

```bash
# Base checkout/worktree
arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > ../base-policy-context.json

# Candidate checkout
arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > current-policy-context.json

arch-linter-net policy weakening \
  --base-context ../base-policy-context.json \
  --current-context current-policy-context.json \
  --format json
```

Policy weakening is a bounded change-time guardrail. It detects typed reductions in enforcement/scope/permissions/exceptions that the exporter can prove. `impact_not_proven` means review is required; it is not evidence of safety.

## 9. Combine no-new-debt and weakening checks in the gate

Once the repository has a reviewed baseline, use the CI gate:

```bash
arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --base-context ../base-policy-context.json \
  --current-context current-policy-context.json \
  --mode all \
  --ensure-built \
  --format json
```

Keep the evidence types separate:

| Evidence | It answers | It does not answer |
| --- | --- | --- |
| Architecture validation | Does the current code satisfy the effective contracts/baseline? | Whether a policy change was a justified relaxation. |
| Architecture change report | What changed between complete analyses? | Whether debt is approved. |
| Policy weakening | Did the effective policy lose enforcement/scope/strength in a way the tool can prove? | Whether existing findings are accepted debt. |
| Debt gate | Are current findings new, matched, resolved, stale, or ambiguous against the reviewed baseline? | Whether the baseline itself was a good design decision. |
| Remediation evidence | What bounded repair direction/evidence applies? | Permission to mutate code, policy, or baseline automatically. |

## 10. Investigate dependencies and history

For the current graph:

```bash
arch-linter-net graph \
  --policy architecture/arch.yml \
  --mode all \
  --level namespace \
  --format mermaid

arch-linter-net explain \
  --policy architecture/arch.yml \
  --source MyApp.Application \
  --target MyApp.Infrastructure \
  --level namespace
```

For change history, use explicit base and target refs:

```bash
arch-linter-net history analyze \
  --from <base-ref> \
  --to <target-ref> \
  --repository . \
  --policy architecture/arch.yml \
  --format json
```

History scores are investigation evidence, not architecture verdicts. Churn measures change volume, co-change identifies coordination pressure, and a hotspot does not by itself prove a design defect.

## Reproduce the recorded v0.6.5 → v0.7.0 evidence

The repository retains a historical dogfood record for the release range `v0.6.5` (exclusive) to `v0.7.0` (inclusive). Reproduction must intentionally use the historical 0.7.0 CLI rather than the repository's current local-tool manifest.

Install it into a caller-owned directory outside analyzed worktrees:

```bash
export ARCH_LINTER_TOOLS="$(mktemp -d)"
dotnet tool install \
  --tool-path "$ARCH_LINTER_TOOLS" \
  ArchLinterNet.Cli \
  --version 0.7.0

if [[ "${OS:-}" == "Windows_NT" ]]; then
  export ARCH_LINTER_NET="$ARCH_LINTER_TOOLS/arch-linter-net.exe"
else
  export ARCH_LINTER_NET="$ARCH_LINTER_TOOLS/arch-linter-net"
fi

"$ARCH_LINTER_NET" --version
```

Use a detached target worktree so source, policy, and tag identity are immutable:

```bash
git worktree add --detach ../example-v070 v0.7.0
cd ../example-v070

export ARCH_LINTER_ARTIFACTS="$(mktemp -d)"
"$ARCH_LINTER_NET" history analyze \
  --from v0.6.5 \
  --to v0.7.0 \
  --repository . \
  --policy architecture/dependencies.arch.yml \
  --format json > "$ARCH_LINTER_ARTIFACTS/release-forensics.json"

sha256sum "$ARCH_LINTER_ARTIFACTS/release-forensics.json"
```

`--from` is exclusive and `--to` is inclusive. The canonical historical artifact intentionally records its own tool/policy/source identity; do not replace those inputs with the current manifest and still call it the same reproduction.

The checked-in evidence record captures the immutable refs, policy blobs, digest, findings, and maintainer interpretation. Historical product defects described there — including the #639 self-dogfood lock — describe that recorded execution context. Current behavior must be evaluated against current source/runtime and, for #639 specifically, includes the #648 fix.

## Decision discipline

When using ArchLinterNet in a real repository:

- Keep policy changes, code changes, and baseline changes independently reviewable.
- Do not treat a clean diff, empty SARIF projection, or missing changed-file finding as proof of whole-repository compliance.
- Do not let an AI agent weaken a rule or broaden an exclusion merely to make validation pass.
- Record exact source/tool/policy identities when a report is used as release or architecture evidence.
- Create follow-up work only when evidence identifies a stable boundary or behavior worth protecting; document the intended invariant and non-goals.
- Prefer the narrowest contract that expresses the decision, and add coverage when omission itself should be a failure.

For exact command options see [CLI reference](../cli/index.md). For CI wrappers and exit-code handling see [Reference entrypoints](reference-entrypoints.md).
