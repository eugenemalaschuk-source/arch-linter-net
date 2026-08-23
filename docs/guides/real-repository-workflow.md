# Real-repository workflow

This guide shows how to use ArchLinterNet on a real repository without turning
history scores, policy changes, or baseline files into automatic decisions. It
uses ArchLinterNet's own public repository as a reproducible example, but the
policy is intentionally repository-specific: adapt the workflow, not its
layer names or rules.

The matching contributor evidence records the exact immutable tags, commit
IDs, policy blobs, report digest, findings, and follow-up decision. This public
path stays evergreen; package and tag versions are evidence for one run, not
the identity of this guide.

## Prerequisites

Use a reviewed local tool pin. A local manifest makes the tool version a
reviewed repository input rather than an accidental workstation global:

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --version
```

The example needs a checkout containing both tags, a policy that is valid for
the selected repository state, and normal restore/build prerequisites for any
command that reads assemblies. Start with `policy check` after changing a
policy; it validates policy structure but does not validate architecture.

Commit the resulting tool manifest after reviewing the selected package. The
exact package version used for the historical ArchLinterNet run belongs in its
checked-in evidence record, not in this evergreen guide.

```bash
dotnet arch-linter-net policy check --policy architecture/dependencies.arch.yml --format json
```

## 1. Investigate a release range

Choose explicit base and target references. In the recorded run, the base was
`v0.6.5` and the target was `v0.7.0`; the tool resolved them to full commit IDs
before analysis.

```bash
dotnet arch-linter-net history analyze \
  --from v0.6.5 \
  --to v0.7.0 \
  --repository . \
  --policy architecture/dependencies.arch.yml \
  --enrich-dotnet \
  --format markdown
```

`--from` is exclusive and `--to` is inclusive. Do not substitute one
`v0.6.5...v0.7.0` revision-expression operand: that syntax is deliberately not
the canonical input contract.

The policy supplies bounded history configuration such as path categories,
task extractors, scoring profiles, ignores, and an optional cluster threshold.
The repository supplies the Git objects; the tool version and effective policy
complete the report identity. For a machine artifact, repeat the same command
with `--format json`, store the exact bytes, and record a SHA-256 digest.

The recorded run analyzed 26 commits. Its leading hotspot was the release
forensics specification (`0.933767294`, 8 commits, 1249 churn); the leading
production reporting file was `HistoryIngestionJsonWriter.cs`
(`0.724866273`, 6 commits, 789 churn). It produced no qualifying `Gtheta`
clusters. Optional .NET enrichment was unavailable while the Git-only report
remained complete.

Those observations mean:

- Churn measures change volume, not complexity or defect count.
- Co-change and bottleneck evidence identify coordination pressure, not module
  ownership or a required merge.
- OCP pressure is an investigation prompt, not proof that a class violates an
  open/closed principle.
- Enrichment is optional context; it must not change Git-level rankings,
  ordering, or correctness.

Classify material results with maintainer context before acting. A one-release
specification hotspot can be intentional integration work; a repeatedly edited
production seam may justify a narrowly scoped follow-up only when the evidence
names a stable boundary and preserved behavior.

## 2. Give an AI agent policy context before editing

Export the composed, effective policy from the actual repository state. This is
pre-edit context: it is not a substitute for validation or a discovered list of
all runtime subjects.

```bash
dotnet arch-linter-net policy context \
  --policy architecture/dependencies.arch.yml \
  --format json > current-policy-context.json
```

The agent can use the context to plan within existing contracts, sources,
imports, and provenance. It must not edit a policy merely to suppress a
diagnostic or to make a later example appear clean.

## 3. Compare complete results, not changed files

Build and analyze each revision independently. A worktree makes the base state
explicit:

```bash
git worktree add --detach ../example-base v0.6.5

# In ../example-base after normal restore/build preparation:
dotnet arch-linter-net change snapshot \
  --policy architecture/dependencies.arch.yml \
  --mode strict \
  --output ../base.architecture-change.json

# In the current checkout after normal restore/build preparation:
dotnet arch-linter-net change snapshot \
  --policy architecture/dependencies.arch.yml \
  --mode strict \
  --output current.architecture-change.json

dotnet arch-linter-net change report \
  --base ../base.architecture-change.json \
  --current current.architecture-change.json \
  --format json
```

The recorded tagged-to-current comparison had zero added or removed surfaces,
new or existing findings, and baseline debt. That is a real result for those
two complete analyses, not proof that every future changed file is safe.

## 4. Review policy weakening separately

Export context from the base checkout as well as the current checkout, then
compare the artifacts. Do not compare two YAML paths directly or rewrite either
policy to manufacture a demonstration.

```bash
# In the base checkout:
dotnet arch-linter-net policy context \
  --policy architecture/dependencies.arch.yml \
  --format json > ../base-policy-context.json

# In the current checkout:
dotnet arch-linter-net policy context \
  --policy architecture/dependencies.arch.yml \
  --format json > current-policy-context.json

dotnet arch-linter-net policy weakening \
  --base-context ../base-policy-context.json \
  --current-context current-policy-context.json \
  --format json
```

The recorded comparison had an empty finding set and `has_errors: false`.
That does not approve a baseline or prove policy changes are harmless. A
weakening result is a separate guardrail: it can require review even when no
persistent architecture finding is new.

## 5. Use remediation and debt gating safely

Validation diagnostics can carry deterministic remediation categories and
evidence. A hint describes a safe direction, such as repairing build input or
using an existing seam; it never edits code, accepts debt, or approves a policy
change.

Once a repository has a reviewed baseline, combine complete candidate analysis
with the optional context comparison in the read-only gate:

```bash
dotnet arch-linter-net gate \
  --policy architecture/dependencies.arch.yml \
  --baseline architecture/baseline.arch.yml \
  --base-context ../base-policy-context.json \
  --current-context current-policy-context.json \
  --mode all \
  --format json
```

The gate keeps three questions separate:

| Evidence | It answers | It does not answer |
| --- | --- | --- |
| Architecture change report | What changed across complete results | Whether debt is approved |
| Policy weakening | Whether the effective policy lost enforcement, scope, permission, or exception strength | Whether existing findings are baseline debt |
| Debt gate | Whether complete current findings are new, matched, resolved, stale, or ambiguous against a reviewed baseline | Whether a policy weakening is acceptable |
| Remediation hint | A deterministic repair category and evidence | Permission to mutate code, policy, or a baseline |

The historical self-run that informed this guide exposed a Windows
`--ensure-built` assembly-lock defect before the debt gate could make a debt
decision. That transparent product gap is tracked in [#639](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/639). Do not treat a blocked
preflight, an empty temporary baseline, or a generic remediation message as a
passing debt-gate result. Run the gate after normal build preparation and keep
baseline creation, update, and approval as explicit review operations.

## Adapt the workflow to your repository

Replace the following inputs deliberately:

- Pick a base and target ref that answer a real release or maintenance question.
  Record their resolved full commits beside the report.
- Use your root policy and its imports. Do not copy this repository's layers,
  source sets, assembly names, or exceptions.
- Keep JSON context, snapshots, reports, and CI artifacts in a caller-owned
  directory; review them as evidence, not configuration.
- Run a complete strict or audit analysis for each snapshot. Changed-file,
  changed-project, and diff information can help triage, but it is not proof of
  whole-repository compliance.
- Create a follow-up only when a material signal identifies a stable behavior
  or boundary that must remain protected. Link the report evidence and state
  the non-goals.

For CI wrappers and exact exit-code handling, see [Reference
entrypoints](reference-entrypoints.md). For baseline lifecycle and gate states,
see [Migration baselines](migration-baselines.md). For the full CLI contract,
see [CLI reference](../cli/index.md).
