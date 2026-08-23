# Release Process

This page documents the maintainer workflow for publishing ArchLinterNet preview/stable releases. The release pipeline is intentionally manual: a maintainer chooses the release scenario, reviews a dry-run, and only then publishes packages and creates the public release record.

## Release records

A completed public release is recorded in three places:

- **NuGet.org packages** — each generated `.nupkg` includes release notes and public package metadata.
- **GitHub Release** — the GitHub Release body uses the generated release notes, and generated `.nupkg` / `.snupkg` files are attached as release assets.
- **GitHub Pages documentation** — the MkDocs public product site is deployed when publication is enabled.
- **Workflow artifacts** — every manual workflow run uploads generated release notes and package artifacts for review/audit of that run.

The GitHub Release is the durable human-facing release record. The workflow does not commit generated release-specific changelog or release-note pages to the repository.

## Packed-artifact authorization boundary

A release is authorized only by the packed-artifact gate for the immutable
candidate produced by that release workflow run. Internal integration evidence
is not package publication evidence.

The manual release workflow resolves the release version, packs one immutable
candidate artifact, runs the required consumer/platform gate against that
candidate, re-verifies its manifest and release evidence, attests the frozen
subjects, and verifies those attestations from a separate job before publication
is reachable. It must not repack or regenerate an attested subject after the
authorization gate. The workflow uploads its release-evidence artifact as the
audit record for that candidate; inspect its JSON/Markdown evidence for package
digests, candidate-manifest digest, commit, platform matrix, gate status,
consumer policy shape, and explicit PASS/FAIL statement.

A missing required platform artifact, invalid digest, failed required scenario,
or workaround-shaped consumer policy blocks publication. A dry-run is evidence
for its own immutable artifact only: a later publish run creates and validates a
new candidate artifact.

### Release-scope authority

The candidate's immutable manifest version selects exactly one reviewed
declaration from `tools/release/scopes/`. Declaration filenames are storage
only: the explicit `release_target` inside each declaration is the mapping
authority. The generator accepts no caller-provided declaration path and never
infers blockers from milestone membership or mutable issue text.

Each supported target has its own release authority. v0.6.4/#527 remains
available for a maintenance publication, while v0.7.0/#613 has its separate
required, non-blocking, and delivered-context inventory. A preview, unknown,
duplicate, malformed, or incompatible target has no authorization and fails
before publication. Release evidence records the selected declaration identity
and SHA-256 together with the candidate version, manifest digest, source commit,
and resolved required issue states; it cannot authorize a different candidate.

### Pre-publication package identity

The canonical candidate manifest is the one authority for project-controlled
pre-publication package bytes. For every shipped package ID it records an
explicit pair of the primary `.nupkg` and corresponding `.snupkg`, including
their filenames, sizes, and SHA-256 digests. Checkpoint B, NuGet publication,
and GitHub Release attachment re-verify and consume that exact paired inventory;
the workflow does not rebuild a publishable subject after manifest creation.

The workflow also attaches `package-checksums.txt`, a deterministic
human-readable rendering derived mechanically from the canonical manifest. It
is convenient verification evidence, not a second checksum authority, and is
not recursively included as a hashed subject in the package manifest. The
canonical JSON manifest is attached alongside it.

After Checkpoint B, GitHub-hosted build provenance attests each manifest-selected
`.nupkg` and `.snupkg` subject and separately attests the exact canonical
manifest/checksum bytes. The manifest does not hash either evidence file; their
attestations are the outer signed identity. The release workflow then verifies
every subject with the GitHub CLI in a separate job before it can upload to
NuGet.org or attach a GitHub Release asset.

The canonical [release-provenance verification guide](../guides/release-provenance-verification.md)
contains the consumer command sequence. It verifies `package-manifest.json` and
`package-checksums.txt` attestations before using their package digest
inventory, checks every GitHub Release package/symbol byte against the verified
manifest, and independently verifies every subject's GitHub attestation.

Those digests identify exact project-controlled bytes before upload and in
GitHub Release attachments. A later NuGet.org primary-package download can have
different raw bytes because NuGet.org repository-signs submissions; the guide
uses NuGet repository-signature/trusted-repository and expected package-ID/
version rules instead of false pre-upload SHA-256 equality. It makes no
unverified post-upload symbol-service claim and records distinct deferred
decisions for NuGet author signing and package-level SBOMs. GitHub provenance
does not claim that a package is secure or that the release meets a formal SLSA
level.

Before publication, verify the generated release notes, the
[evergreen adoption/upgrade guide](../guides/upgrading.md), and installed-schema
commands against the candidate packages. Release-specific history belongs in
the GitHub Release/tag and workflow evidence, not in a new version-named page in
the evergreen documentation tree.

## Versioning

ArchLinterNet follows Semantic Versioning 2.0.

Pre-1.0 preview releases use versions such as `0.1.0-preview.1`. The manual release workflow calculates package versions from git tags based on the selected release scenario (`preview`, `patch`, `minor`, or `major`). Do not update `Directory.Build.props` just to run a release.

### Version calculation rules

The workflow detects the latest SemVer-compatible git tag and calculates the next version:

| Latest tag | Release type | Calculated version |
|------------|--------------|--------------------|
| `v0.1.1-preview.2` | `preview` | `0.1.1-preview.3` |
| `v0.1.0` | `preview` | `0.1.1-preview.1` |
| `v0.1.1-preview.2` | `patch` | `0.1.1` |
| `v0.1.0` | `patch` | `0.1.1` |
| `v0.1.0` | `minor` | `0.2.0` |
| `v0.1.0` | `major` | `1.0.0` |

Tags use the `v` prefix. Package versions are emitted without `v`.

### Schema registry identity

Every candidate package ships the packaged schema registry owned by that
candidate. Registry entries version persisted format contracts independently
from package SemVer; a package version must never be transformed mechanically
into a `$schema` URL.

Before publication, run `arch-linter-net schema list` from the candidate package
and confirm every documented machine-contract identifier that is relevant to
that release is actually listed there. Exact immutable schema IDs may contain a
version as part of the compatibility contract; that is distinct from using the
current package SemVer as an evergreen documentation identity.

### Version override

Use `version_override` only when automatic tag-based calculation cannot be used:

- first release with no SemVer-compatible tags;
- emergency recovery from a broken/manual versioning situation.

For normal preview continuation, leave `version_override` empty.

An override does not create or redirect release-scope authority. Its calculated
candidate version must still have exactly one reviewed stable declaration in
`tools/release/scopes/`, or the release workflow fails closed.

## Public documentation boundary

GitHub Pages publishes only the public product documentation generated by MkDocs.

Evergreen product documentation describes durable concepts and current product
behavior. It must not create a new page/route/navigation identity for every
package release. Release-specific history belongs in GitHub Releases, tags,
issues/milestones, and workflow evidence. Genuine machine/protocol/document
versions remain documented where those versions are themselves the contract.

Internal project documentation remains in repository Markdown files and must not appear in the MkDocs navigation or generated site:

- `docs/internal/`;
- OpenSpec/change archives;
- backlog governance;
- issue-writing rules;
- repository-agent instructions;
- implementation planning notes.

The release workflow should deploy the generated MkDocs site only. It should not publish internal documentation as product docs.

## NuGet metadata and links

Before publication, inspect package metadata and confirm:

- `PackageProjectUrl` points to the public GitHub Pages documentation site;
- `RepositoryUrl` points to the GitHub repository;
- `PackageReadmeFile` is a concise user-facing README;
- `PackageLicenseExpression` matches the repository license;
- release notes are user-facing enough for NuGet.org;
- no NuGet-facing link points to internal project documentation.

See [NuGet package metadata](nuget-metadata.md) for the canonical link model.

## Workflow separation

Pull request CI and package publication are intentionally separate:

- PR CI validates code and documentation.
- The manual release workflow owns official package build, optional NuGet publication, GitHub Release creation, and GitHub Pages deployment.

PR CI must not call official release publication steps, request publishing identity tokens, push packages, create tags, create GitHub Releases, or deploy docs.

Local `make pack` is only for developer inspection. Official publication is performed by the manual GitHub Actions workflow.

## NuGet.org trusted publishing setup

Before the first public publication:

1. Configure a NuGet.org trusted publishing policy with these fields:
   - package owner: `eugene.malaschuk`;
   - repository owner: `eugenemalaschuk-source`;
   - repository: `arch-linter-net`;
   - workflow file: `release-nuget.yml`;
   - environment: empty.
1. Enable GitHub Pages for the repository and use GitHub Actions as the Pages source.

Classic long-lived NuGet API keys are not stored as repository secrets for this workflow. The release job uses GitHub's publishing identity flow to obtain the NuGet publish credential during the run.

NuGet.org is the package publication target for preview consumption. GitHub Packages is not used as package storage or as a mirror in the initial release pipeline.

## Manual release procedure

Always run releases from the GitHub Actions UI. Do not publish official packages from a local machine.

### Step 1: dry-run review

Run the release workflow with `publish: false`.

Expected dry-run result:

- the required packed-artifact platform/consumer matrix and aggregated release
  evidence pass for the candidate;
- restore, Release build, and acceptance validation pass;
- release notes are generated as workflow artifacts;
- package artifacts are built with one calculated package version;
- packages contain public metadata and package README;
- every frozen package, symbol, canonical manifest, and checksum subject is
  attested and independently verified when GitHub attestation permissions are
  available;
- nothing is pushed to NuGet.org;
- no GitHub tag or GitHub Release is created;
- docs are not deployed.

Before continuing, inspect:

- release notes artifact;
- package artifacts;
- generated package metadata;
- package README;
- project/repository/license links.

### Step 2: public publication

After dry-run artifacts are checked, rerun the workflow with the same release scenario and `publish: true`.

Expected public result:

- packages are pushed to NuGet.org;
- an existing primary package causes a fail-closed error; inspect the paired
  primary/symbol state on NuGet.org before deciding on a corrected release path;
- GitHub tag and release are created from the workflow commit;
- the attested package, symbol, canonical manifest, and checksum assets are
  attached to the GitHub Release without regeneration;
- MkDocs product documentation is built and deployed to GitHub Pages.

After publication, verify:

- NuGet.org shows expected package versions;
- NuGet package project links open the public product docs;
- NuGet repository links open the GitHub repository;
- NuGet package README is product-facing;
- GitHub Release exists and contains every expected attested package, symbol,
  manifest, and checksum asset;
- GitHub Pages deployment completed successfully;
- internal docs are not visible in the published site navigation.

For post-publication integrity confirmation, download the GitHub Release assets,
verify their GitHub attestations with the command above, then compare their
SHA-256 values with the verified canonical manifest. For a NuGet.org-downloaded
primary package, verify NuGet repository-signature/trusted-repository semantics
and expected package ID/version instead; do not report its expected
repository-signing byte change as tampering. Do not assume the same raw-byte or
signature behavior for downloaded `.snupkg` files without documented
symbol-service evidence.

Record the published package IDs, version, GitHub Release URL, NuGet package URL, and GitHub Pages URL in the related issue or pull request notes.

## Failure and rerun notes

- If the dry-run fails, fix the underlying problem and rerun with `publish: false`.
- If public publication fails before NuGet push completes, no GitHub Release should be created.
- If NuGet publication partially succeeds, inspect NuGet.org and workflow logs before rerunning. A duplicate primary-package push is fail-closed because it cannot prove the paired symbol state; do not use duplicate-success behavior.
- If a GitHub Release already exists for the target tag, do not overwrite it blindly. Inspect the existing release and decide whether to fix the release manually or publish a new version.

## Non-goals

The release workflow does not:

- publish from pushed tags automatically;
- publish docs independently from package publication;
- publish internal project documentation as product docs;
- commit generated changelog files;
- maintain a custom changelog website;
- publish packages to GitHub Packages.
