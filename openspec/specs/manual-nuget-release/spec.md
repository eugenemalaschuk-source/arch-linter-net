# Manual NuGet Release Specification

## Purpose

Documents and supports the manual NuGet release workflow, including the release package build.

## Requirements

### Requirement: Manual NuGet release workflow

ArchLinterNet SHALL provide a separate GitHub Actions workflow for official package builds and optional NuGet.org publication that maintainers run through the GitHub Actions UI using `workflow_dispatch`.

#### Scenario: Manual release inputs are required

- **WHEN** a maintainer starts the release workflow manually
- **THEN** the workflow requires a `release_type` choice input (`preview`, `patch`, `minor`, or `major`), exposes an optional `version_override` text input for emergency recovery, and exposes a `publish` boolean input that defaults to `false`

#### Scenario: Manual release starts from GitHub UI

- **WHEN** a maintainer releases preview packages
- **THEN** the maintainer starts the release from the GitHub Actions UI instead of publishing from a local machine

#### Scenario: Manual release validates version input

- **WHEN** the release workflow starts
- **THEN** it rejects empty or obviously invalid package version input before building packages

### Requirement: Manual release package build

The release workflow SHALL restore, run the repository acceptance gate, build, pack, and upload versioned NuGet package artifacts using the explicit version input.

#### Scenario: Dry-run release builds artifacts

- **WHEN** the release workflow runs with `publish=false`
- **THEN** it restores packages, builds in Release configuration with the explicit version, runs `make acceptance`, packs versioned `.nupkg` artifacts, uploads them to the workflow run, and publishes nothing

#### Scenario: Package version comes from workflow input

- **WHEN** the release workflow packs packages
- **THEN** it passes the explicit version input as `PackageVersion` for the package artifacts

#### Scenario: Expected package projects are packed

- **WHEN** the release workflow packs packages
- **THEN** it creates package artifacts for `ArchLinterNet.CEL`, `ArchLinterNet.Core`, `ArchLinterNet.Cli`, and `ArchLinterNet.Testing`
- **AND** `ArchLinterNet.CEL` is packed before `ArchLinterNet.Core` so the dependency is resolvable
- **AND** Unity `.asmdef` validation ships as part of `ArchLinterNet.Core`, not as a separate `ArchLinterNet.Unity` artifact

### Requirement: Controlled NuGet.org publication

The release workflow SHALL publish packages to NuGet.org only when the manual run explicitly requests publication.

#### Scenario: Publication requires publish input

- **WHEN** the release workflow runs with `publish=true`
- **THEN** it publishes package artifacts to `https://api.nuget.org/v3/index.json`

#### Scenario: Publication uses trusted publishing

- **WHEN** the release workflow publishes packages
- **THEN** it publishes through NuGet.org Trusted Publishing without a classic NuGet API key

#### Scenario: Publication is rerun-safe

- **WHEN** the release workflow publishes packages
- **THEN** it uses `--skip-duplicate` for NuGet push operations

### Requirement: Documentation publication

The release workflow SHALL publish the documentation site to GitHub Pages when public package publication is requested.

#### Scenario: Documentation deploys with public publication

- **WHEN** the release workflow runs with `publish=true` and package publication succeeds
- **THEN** it builds the documentation site and deploys it to GitHub Pages

#### Scenario: Documentation does not deploy during dry-run

- **WHEN** the release workflow runs with `publish=false`
- **THEN** it does not deploy documentation

### Requirement: Candidate-bound release history analysis

The manual release workflow SHALL run a standalone, read-only history-forensics job after preparing the immutable candidate. The job SHALL verify and use the exact candidate CLI package and candidate source commit/tree, read the complete required Git object graph, and run exactly one Git-only analysis that emits canonical JSON and Markdown from the same finalized result. It SHALL NOT build the analyzed solution or run automatic .NET enrichment. Candidate versions SHALL use the complete package-version syntax accepted by the existing `version_override` validation.

Stable candidates SHALL use the highest lower stable SemVer release tag whose peeled commit is an ancestor of the candidate. Prerelease candidates SHALL have a distinct `preview:X.Y.Z` series identity and use the highest lower SemVer prerelease tag in that same `X.Y.Z` line when one exists; prerelease identifiers SHALL be ordered using NuGet SemVer precedence. The first prerelease in a line SHALL use the highest lower stable ancestor. Build metadata SHALL remain part of the candidate version/tag identity but SHALL NOT affect version precedence. Stable selection SHALL ignore prerelease and development-build tags. Selection SHALL pin the authored tag and resolved full SHA before analysis and SHALL fail closed for shallow history, missing required objects, ambiguous SemVer-precedence tags, invalid candidate identity, or mismatched candidate package identity. If no eligible predecessor exists, the result SHALL be typed `not-applicable: no_previous_release`, with no fabricated root predecessor or empty successful analysis.

The selected range SHALL be the complete exclusive-base/inclusive-candidate Git range. It SHALL NOT be truncated to a fixed commit count, changed to a date range, or reduced to first-parent history.

#### Scenario: Stable candidate uses the previous stable ancestor

- **WHEN** a stable candidate has lower stable, prerelease, and `main.N` tags reachable from its commit
- **THEN** the analysis starts exclusively at the highest lower stable tag on candidate ancestry and ends inclusively at the exact candidate commit
- **AND** prerelease and development-build tags do not become the stable boundary

#### Scenario: Preview candidate remains in its own line

- **WHEN** an `alpha`, `beta`, `rc`, or `preview` candidate has a lower prerelease tag in the same `X.Y.Z` line on candidate ancestry
- **THEN** the analysis uses the highest lower tag by NuGet SemVer precedence and records series identity `preview:X.Y.Z`
- **WHEN** no prior prerelease in that line exists
- **THEN** the analysis uses the highest lower stable ancestor, if present
- **AND** build metadata is preserved in candidate identity but does not change predecessor ordering

#### Scenario: First release has no predecessor

- **WHEN** no eligible stable predecessor exists for a candidate
- **THEN** the workflow produces a typed `not-applicable: no_previous_release` result
- **AND** it does not invent a root predecessor or claim a successful empty-range analysis

#### Scenario: Incomplete or ambiguous Git identity

- **WHEN** the checkout is shallow, a required tag/object is missing, multiple eligible tags have equivalent SemVer precedence, or candidate/package identity differs
- **THEN** the history job fails and no successful forensics bundle is published

#### Scenario: Findings do not gate release

- **WHEN** a complete history analysis contains high hotspots, bottlenecks, or OCP findings
- **THEN** those findings remain evidence and do not alter release authorization
- **WHEN** range selection, ingestion, rendering, or required artifact production fails
- **THEN** the job fails visibly and package publication remains blocked

### Requirement: Release history evidence is durable and candidate-bound

Each successful history job SHALL create `release-forensics.json`, `release-forensics.md`, a compact provenance manifest, checksums, and separate operational observations. The manifest SHALL bind repository, candidate commit/tree/version, selected base tag/SHA when applicable, range and series identity, tool/package identity, history report schema/semantics, effective history-policy identity, and canonical report content digests. Run timestamps and operational measurements SHALL remain outside the canonical history JSON.

Dry-run candidates SHALL upload the complete bundle as workflow artifacts without public writes. For publishing candidates, the existing GitHub Release job SHALL wait for both required acceptance and the complete bundle, attach the verified files, and verify their read-back digests. An identical candidate/content retry MAY complete successfully; pre-existing conflicting asset content SHALL fail without silent overwrite. The existing release publication and tag lifecycle SHALL remain the only public publisher.

#### Scenario: Dry-run preserves a reviewable bundle

- **WHEN** a dry-run candidate completes history analysis
- **THEN** JSON, Markdown, manifest, checksums, and operational observations are available as workflow artifacts
- **AND** no public release asset or tag is written

#### Scenario: Publication verifies retained assets

- **WHEN** all required acceptance jobs and history-forensics succeed for a publishing candidate
- **THEN** the existing GitHub Release job attaches the candidate-bound bundle and verifies each downloaded asset digest
- **AND** an existing asset with different content causes a visible failure without replacement

#### Scenario: Analysis and orchestration measurements remain separate

- **WHEN** the history bundle is generated
- **THEN** CLI analysis/render phase durations and process memory are recorded separately from orchestration durations and run timestamps
- **AND** neither measurement is included in canonical history JSON or represented as the existing PR-performance KPI

### Requirement: Bundle rendering duration excludes analyzer process time

The operational observation `bundle_render_ms` SHALL measure runner-side report and provenance-manifest rendering after the candidate analyzer has returned. The analyzer process wall duration SHALL be recorded separately as `analyzer.process_wall_ms` and SHALL NOT be duplicated in `bundle_render_ms` or another orchestration duration.

#### Scenario: Analyzer process duration is separate from bundle rendering

- **WHEN** an applicable candidate completes the history analysis and bundle generation
- **THEN** `analyzer.process_wall_ms` records the candidate CLI process duration
- **AND** `bundle_render_ms` measures only the runner-side report/manifest work after that process completes
