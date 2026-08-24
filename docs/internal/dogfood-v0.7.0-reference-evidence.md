# v0.7.0 self-dogfood reference evidence

Issue: #631\
Parent story: #630\
Recorded: 2026-08-23

This record is repository-safe evidence for the public
[real-repository workflow](../guides/real-repository-workflow.md). It records
one immutable release-range run and a separate real public-revision drift
comparison. It is evidence, not an automatic refactoring instruction or a
release authority.

## Recorded inputs

| Input | Identity |
| --- | --- |
| Tool | `ArchLinterNet.Cli` 0.7.0, installed with `dotnet tool install --tool-path` in an isolated consumer-owned directory |
| Repository object format | `sha1` |
| Base tag | `v0.6.5` → `ff64174c2de750e5d18cb7072387173bffc26bd0` |
| Target tag | `v0.7.0` → `e381f12b58b7f91e680e54ed57a20906ee99f057` |
| Root policy blob | `e36701aaa99d9f21ea14c623a7c6749ed600f89d` |
| Imported policy blobs | `ff0ad7ba71aa88fc720862a8d52007810952b63c`, `58f643bea55724786ced6bf0990a6735e1a885b3`, `57a82fcf7faa28a85e91315d74650e770688e069`, `43fe933dac5f439018644302a72015739d5ce05c`, `df3a8667d28adae4939f408fd1ef3e09298bdabf`, `4d9e0d660cba816bcb96eac31224af8439d53282` |
| Canonical artifact | [`dogfood-v0.7.0-release-forensics.json`](dogfood-v0.7.0-release-forensics.json), 34,666,178 bytes |

The recorded forensics command was:

```text
arch-linter-net history analyze \
  --from v0.6.5 \
  --to v0.7.0 \
  --repository . \
  --policy architecture/dependencies.arch.yml \
  --format json
```

`--from` and `--to` are separate canonical operands. The command did not use
`v0.6.5...v0.7.0` as a revision expression.

The command ran from a clean detached checkout at `v0.7.0`, with the isolated
tool executable invoked directly rather than through a repository tool
manifest. It deliberately omits `--enrich-dotnet`, so the retained JSON has the
deterministic enrichment status `not_requested`.

Canonical artifact SHA-256: `adfda105c0f125319f5e5d8e71050268c39b4eac6750e4005b2790dd3c8e6d0e`

The raw JSON stream was regenerated into a separate file from the same clean
checkout and had the identical digest. `make lint-docs` runs
`tools/scripts/check_dogfood_reference_evidence.py`, which streams the retained
artifact and fails when its bytes no longer match this documented value. The
canonical report identity is the tool version, repository object format,
authored and resolved operands, effective policy identity above, and this
digest; it intentionally excludes checkout paths and machine state.

An earlier separately requested `--enrich-dotnet` observation was
`unavailable` with reason `worktree_verification_failed`. It is retained below
as advisory product evidence only: enrichment status and reason are serialized
into an enriched report, so that environment-specific result does not define
the canonical JSON or its digest.

## Release-forensics result

The run analyzed 26 commits and excluded no merge commits. The effective
history configuration had no extra extractor, ignore pattern, or `Gtheta`
threshold.

| Evidence | Observed result | Maintainer classification |
| --- | --- | --- |
| Highest hotspot: `openspec/specs/release-architecture-forensics/spec.md` (0.933767294; 8 commits; 1249 churn) | It concentrates the v0.7 theory/specification integration work. | Expected/intentional architecture. A release specification is a deliberately shared authority during a feature wave; its score does not justify splitting it. |
| `docs/internal/release-forensics.md` (0.759469182; 5 commits; 971 churn) | The contributor theory guide evolved with the implementation. | Insufficiently actionable signal. It documents a one-release delivery wave rather than a recurring code ownership conflict. |
| `HistoryIngestionJsonWriter.cs` (0.724866273; 6 commits; 789 churn) | Canonical reporting matured alongside the report contract. | Useful confirmed technical pressure, already addressed by the delivered reporting boundary. The result supports retaining focused reporting tests, not a new refactor. |
| Highest bottleneck: the release-forensics spec (0.908250070; 21 independent tasks; 200 `G0` neighbors) | Many independently numbered delivery commits altered the shared theory authority. | Expected/intentional architecture. This is a release-integration document, not production coordination evidence. |
| `HistoryIngestionResult.cs` and `HistoryIngestionService.cs` bottlenecks | They bridge ingestion, finalized evidence, and report projection. | Useful confirmed technical pressure. Existing History ingestion/evidence/reporting seams already protect this stable boundary; no duplicate task is warranted. |
| `Gtheta` clusters | No qualifying cluster. | Insufficiently actionable signal. The policy did not configure a significance threshold, so the run exposes pair evidence but makes no cluster claim. |
| Highest OCP signals: theory guide (0.748991507), release-forensics spec (0.733125174), `HistoryIngestionService.cs` (0.713775629) | Repeated edits reflect one feature wave and stable ingestion composition. | The document signals are intentional; the service signal is retained as a future investigation prompt only, not a refactoring mandate. |
| Canonical .NET enrichment projection | `not_requested`; the retained Git-only report completed normally. | Canonical report identity remains independent of local worktree and build state. |
| Separately requested .NET enrichment | `unavailable`, reason `worktree_verification_failed`. | Expected runtime-optional behavior. This advisory observation confirms enrichment is not Git-level correctness authority; it is not the canonical digest-bearing result. |

The findings contain no measured performance regression, security issue, or
stable production boundary that justifies a new refactoring task.

## AI-first drift-control result

The comparison used the immutable target tag above as the base and public
`main` commit `6504312cbad16ee7d30af751c2662fdeb486f267` as current. Both
states used the released command surface, actual repository policy, and
complete `architecture-change-snapshot/v2` artifacts.

| Artifact | SHA-256 | Result |
| --- | --- | --- |
| Base architecture snapshot | `18661F3B79B29CD78563EF31251FE29EDD0CCD9C4B36D221E87D9C095E006CB5` | 96,069 bytes |
| Current architecture snapshot | `18661F3B79B29CD78563EF31251FE29EDD0CCD9C4B36D221E87D9C095E006CB5` | 96,069 bytes |
| Base effective-policy context | `FD6C3B0BB6BCEB6FD803070D46E0239C11A209D6B221907F90AD93C6312DE235` | 3,009,054 bytes |
| Current effective-policy context | `FD6C3B0BB6BCEB6FD803070D46E0239C11A209D6B221907F90AD93C6312DE235` | 3,009,054 bytes |

`change report` completed with zero added or removed surfaces, new or existing
findings, and baseline debt. `policy weakening` returned an empty finding set
with `has_errors: false`. These are actual clean comparison results; the policy
and baseline were not weakened, ignored, or rewritten to create them.

The read-only debt gate was also invoked with the separately exported policy
contexts. On this Windows self-analysis run, preparation failed before it could
make a persistent-debt decision: the CLI process held
`ArchLinterNet.Testing.dll` open while `--ensure-built` attempted to rebuild it.
The generic deterministic remediation category was `fix_policy_input`; it did
not suggest modifying code or policy automatically. The focused, evidence-linked
follow-up is #639. The temporary empty baseline used only to invoke the gate was
not committed, reviewed, or treated as accepted debt.

## Self-policy applicability decisions

| Candidate | Decision | Rationale |
| --- | --- | --- |
| Turn forensics hotspot/bottleneck/OCP scores into strict self-policy rules | `not-applicable` | These scores are advisory history evidence, not architecture invariants. |
| Add history pipeline boundary rules | `already-covered` | The self-policy already names the canonical, Git, configuration, task, evidence, scoring, reporting, and optional enrichment layers, with directional constraints. |
| Require .NET enrichment for self-policy acceptance | `not-applicable` | The shipped contract makes enrichment optional at runtime; the successful Git-only report proves it is not correctness authority. |
| Add a new-debt baseline merely for the reference | `not-applicable` | No reviewed architecture debt exists in this run, and a showcase baseline would weaken the evidence model. |
| Make the combined debt gate a self-policy release prerequisite | `defer` | The command is valuable, but #639 blocks the observed Windows self-analysis path. Adoption requires a working, evidence-backed consumer run first. |
| Adopt a rule that protects the build-before-load sequencing boundary | `defer` | The confirmed defect is owned by #639; a durable policy decision follows the focused correction and regression evidence. |

## Product and documentation gap

The installed-tool history workflow is reproducible, but the same self-repository
consumer path exposed the `--ensure-built` assembly-lock defect above. #639
owns the correction. This record and the public guide remain intentionally
honest about that boundary: a blocked build-state preparation cannot be
presented as a clean architecture or debt-gate result.
