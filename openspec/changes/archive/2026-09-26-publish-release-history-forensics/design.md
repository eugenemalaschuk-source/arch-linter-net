## Context

The existing `prepare-candidate` job calculates the package version and produces a verified package manifest from the workflow's source commit. The CLI package is a global tool and supports repeatable report sinks plus `--timings`. The current `release` and `create-release` jobs own package publication, GitHub Release creation, and asset attachment. See the proposal and delta specs for the external contract.

## Goals / Non-Goals

**Goals:**

- Resolve and pin the appropriate release predecessor against the exact candidate commit and the complete Git object graph.
- Use the verified packed CLI package to generate both reports in one invocation and preserve the CLI's canonical JSON bytes.
- Fail closed when range identity, package identity, analysis, or bundle completeness cannot be established.
- Keep analysis read-only and allow it to run alongside independent Checkpoint B work.
- Gate package publication on the completed bundle, then attach and verify it using existing release jobs.

**Non-Goals:**

- Change version calculation, forensics scoring, policy semantics, .NET enrichment, or PR validation.
- Add nightly or PR analyses, rolling commit windows, trends, graphs, hosted storage, or a second release publisher.
- Make hotspot, bottleneck, or OCP findings a release-quality gate.

## Decisions

### Candidate and package identity

The history job consumes `prepare-candidate` outputs for package version, target tag, candidate commit, and candidate tree. It downloads the existing candidate package artifact, validates the full package manifest and all package digests, and confirms that the CLI package record matches the version and source commit. It installs `ArchLinterNet.Cli` from that verified local artifact at the exact version; it does not build the solution or resolve a floating/latest CLI.

The job checks out with `fetch-depth: 0`, disables persisted checkout credentials, and has only `contents: read`. It verifies that the checkout is not shallow and that the checked-out commit/tree match the candidate outputs before resolving release tags.

### Release predecessor selection

Keep the existing release version selector as the sole source of candidate version and target tag. A release-forensics resolver consumes those exact values and the candidate SHA; it does not calculate a second release version.

For stable candidates, consider only lower stable SemVer tags on candidate ancestry. For preview candidates, first consider lower preview tags in the same `X.Y.Z` line; when none exists, use the highest lower stable ancestor. Series identity is `stable` for stable candidates and `preview:X.Y.Z` for previews. Tags such as `main.N` are not release tags. Enumerate authored tag names once, reject duplicate names for the same applicable SemVer identity, peel annotated tags to commits, verify required objects, and verify ancestry. Pin the chosen authored tag and full lowercase SHA before calling the CLI; pass the SHA, not a symbolic tag, as `--from` so a tag move cannot change the analysis after selection.

When there is no eligible predecessor, produce an explicit typed not-applicable result, not an analysis with an invented root SHA or empty range. If the exact target tag already exists for a rerun, accept it only when it resolves to the same candidate SHA; a conflicting target tag fails closed.

### Analysis and evidence bundle

One CLI process runs `history analyze` with the selected base SHA, candidate SHA, repository policy, and two file report sinks (`json` and `markdown`), plus `--timings`. It omits `--enrich-dotnet`. A non-zero result, missing/empty output, or report range/schema/tool-version mismatch fails the job. The canonical report is passed through as emitted; orchestration does not deserialize and rewrite it.

Generate a deterministic manifest containing candidate and predecessor identity, range/series semantics, CLI package ID/version/digest, report schema and history-semantics version, effective history-configuration digest, policy input digest, and JSON/Markdown content digests. The typed not-applicable JSON and Markdown identify their result kind and reason and explicitly say no analysis ran. A checksums file covers the JSON, Markdown, operational observations, and manifest.

Write operational observations to a separate JSON file. It contains workflow run/attempt and UTC timestamps, range/package/verification/install/bundle orchestration durations, CLI phase timings (including JSON and Markdown rendering), analysis-process wall time, and peak process memory. These values never enter canonical history JSON or alter the prior PR performance KPI. A bounded Actions summary reports candidate/range/status and links to the workflow run artifacts; report text and paths are treated as data and never evaluated as shell code.

### Release publication and retry behavior

The `history-forensics` job needs only `prepare-candidate`, so it can run while Checkpoint B proceeds. The existing package `release` job adds history forensics to its dependencies, making a complete bundle necessary before NuGet publication. Existing `create-release` downloads the history bundle and verifies its candidate identity before attaching the same files with the package assets.

Keep asset publication in `create-release`; do not grant write permissions to the analysis job or create a second publisher/tag job. On rerun, verify an already attached same-name asset before accepting it. Upload missing assets without a clobber option; fail if an existing asset has a different digest. Download the resulting release assets and compare every digest with the manifest/checksum inventory before the job succeeds.

## Risks / Trade-offs

- **[Tag aliases or unusual candidate versions make predecessor identity unclear]** → restrict selection to the existing supported stable/preview SemVer forms, detect same-version eligible aliases, and fail closed rather than guess.
- **[Full Git history increases checkout time and storage]** → the analysis requires the complete ancestry/object graph; keep this limited to the manual release run and record orchestration observations separately.
- **[GitHub Release creation can partially succeed before read-back fails]** → make reruns idempotent for identical bytes, prohibit clobber, and verify all existing/missing assets before reporting success.
- **[Candidate package installation may need runtime dependencies]** → install the exact tool version from the candidate source and allow normal pinned package dependency resolution; the candidate `.nupkg` itself remains digest-verified.

## Migration Plan

No migration is required. Dry runs immediately retain the bundle as workflow artifacts. Publishing runs attach the same bundle to the GitHub Release after candidate verification. If the new analysis or asset check fails, the workflow stays failed and maintainers can rerun the same candidate after correcting the underlying input or transient transport issue; conflicting public content requires an explicit corrected release path.
