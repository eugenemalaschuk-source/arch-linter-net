# Verify release provenance

This guide verifies **release artifacts**, not a checkout of this repository.
Use it for a GitHub Release or for the non-publishing release-rehearsal
candidate. A rehearsal is sufficient to authorize the first publication; the
live GitHub Release and NuGet.org checks are post-publication confirmation.

## What each mechanism proves

| Mechanism | Trust boundary | What it does not prove |
| --- | --- | --- |
| NuGet trusted publishing | The release workflow was authorized to obtain short-lived NuGet publishing credentials. | The provenance of arbitrary downloaded bytes. |
| Candidate manifest | Exact pre-publication project-controlled `.nupkg` and `.snupkg` inventory. | Its own authenticity or the identity of a later NuGet.org download. |
| Derived checksums | A convenient rendering of the canonical manifest. | A second digest authority. |
| GitHub build provenance | Signed binding of an exact subject to this repository, workflow, and source commit. | Package quality, security, or a formal SLSA level. |
| NuGet.org repository signing | NuGet.org's integrity/trust semantics for its downloadable primary package. | A project-controlled author signature. |
| NuGet author signing | A separate project-controlled X.509 signature. | GitHub provenance or NuGet.org repository signing. |
| SBOM | A component inventory for an artifact. | Provenance or an automatically accurate package-to-component mapping. |

The canonical `package-manifest.json` inventories project-controlled package
subjects only. `package-checksums.txt` is derived from it and is deliberately
outside that inventory: making either evidence file hash itself would create a
recursive authority. GitHub attests the manifest and checksum files as separate
outer evidence subjects.

## Verify GitHub Release or rehearsal assets

Install a current [GitHub CLI](https://cli.github.com/) and download all of the
following into one empty directory: `package-manifest.json`,
`package-checksums.txt`, and every `.nupkg` and `.snupkg` attached to the same
GitHub Release or release-rehearsal candidate artifact. Do not mix assets from
different runs, versions, or commits.

Set the release identity from the GitHub Release tag/commit or rehearsal run:

```bash
export REPOSITORY=eugenemalaschuk-source/arch-linter-net
export WORKFLOW="$REPOSITORY/.github/workflows/release-nuget.yml"
export SOURCE_COMMIT=<40-or-64-character-release-source-commit>
```

### 1. Authenticate evidence before using it

Do not treat a downloaded manifest or checksum file as trusted metadata until
its own attestation verifies. Run both commands from the asset directory:

```bash
gh attestation verify package-manifest.json \
  --repo "$REPOSITORY" \
  --signer-workflow "$WORKFLOW" \
  --source-digest "$SOURCE_COMMIT"

gh attestation verify package-checksums.txt \
  --repo "$REPOSITORY" \
  --signer-workflow "$WORKFLOW" \
  --source-digest "$SOURCE_COMMIT"
```

An absent, modified, wrong-repository, wrong-workflow, or wrong-commit evidence
file fails this step. Stop rather than using its digest inventory.

### 2. Check the verified inventory and GitHub asset bytes

After both evidence files verify, inspect the expected release identity and
paired package/symbol inventory:

```bash
jq '{schema, version, source_commit,
     packages: [.packages[] | {id, version, package: .package.file, symbols: .symbols.file}]}' \
  package-manifest.json

jq -e --arg commit "$SOURCE_COMMIT" \
  '.schema == "checkpoint-b-candidate-manifest/v2" and .source_commit == $commit' \
  package-manifest.json
```

Confirm the displayed package IDs and version are the release you intended to
verify. Then derive the expected SHA-256 lines from the verified manifest and
compare both the derived checksum evidence and the downloaded project-controlled
package bytes:

```bash
jq -r '.packages[] | .package, .symbols | "\(.sha256)  \(.file)"' \
  package-manifest.json > expected-package-subjects.sha256

diff -u expected-package-subjects.sha256 \
  <(grep -Ev '^#|^$' package-checksums.txt)
sha256sum --check expected-package-subjects.sha256
```

On Windows, use `Get-FileHash -Algorithm SHA256 <asset-path>` and compare each
result to the verified manifest's corresponding `sha256` value. The
`package-checksums.txt` rendering is convenience evidence only; the verified
manifest remains the canonical package-subject inventory.

### 3. Verify every package and symbol attestation

The byte match does not replace each subject's own provenance. Verify every
manifest-selected `.nupkg` and `.snupkg` independently:

```bash
while IFS= read -r subject; do
  gh attestation verify "$subject" \
    --repo "$REPOSITORY" \
    --signer-workflow "$WORKFLOW" \
    --source-digest "$SOURCE_COMMIT"
done < <(jq -r '.packages[] | .package.file, .symbols.file' package-manifest.json)
```

Successful verification binds each exact file digest to the repository,
`release-nuget.yml`, and source commit. Keep the successful output with your
release evidence if you need an audit trail.

### Expected failures

These checks are intentionally fail-closed:

- Modifying a package or symbol makes `sha256sum --check` fail and makes its
  `gh attestation verify` subject digest fail.
- Modifying `package-manifest.json` or `package-checksums.txt` makes its own
  GitHub attestation verification fail before it can be trusted.
- Removing an asset or selecting one not named by the verified manifest means
  the release-subject set is incomplete or unexpected; do not substitute a
  similarly named file.

## Verify a package downloaded from NuGet.org

This is a distinct post-publication path. NuGet.org repository-signs submitted
primary `.nupkg` files, adding a repository signature (or a countersignature
when an author signature exists). That is expected platform behavior: the raw
SHA-256 of a NuGet.org-downloaded `.nupkg` is therefore **not expected** to
equal the pre-upload manifest digest or GitHub Release attachment digest.

First verify that you selected the expected package ID, version, and
`https://api.nuget.org/v3/index.json` source. Then verify the downloaded primary
package with NuGet's supported signature tooling and your organization's
trusted-repository policy:

```bash
dotnet nuget trust list --configfile ./nuget.config
dotnet nuget verify ./ArchLinterNet.Core.<version>.nupkg \
  --all \
  --configfile ./nuget.config
```

`dotnet nuget verify` validates the signed package; the explicit configuration
lets a consumer apply its required package sources and trusted signers. See the
[.NET verification command](https://learn.microsoft.com/dotnet/core/tools/dotnet-nuget-verify)
and [NuGet trust-boundary guidance](https://learn.microsoft.com/nuget/consume-packages/installing-signed-packages)
for supported platform and trusted-signer configuration details.

Do not strip or rewrite signatures to manufacture raw-byte equality, and do not
report the expected repository-signing difference as tampering. `.snupkg`
behavior is not assumed to mirror the primary package: check it only through
the supported NuGet.org symbol-server contract after a real release exists.

## Project decisions

### NuGet author signing: deferred

The project does not currently produce project-controlled NuGet author
signatures. GitHub provenance and NuGet.org repository signing are not author
signatures. Deferral is deliberate for a solo-maintained project: author signing
needs an owned code-signing certificate, secure key custody, renewal/rotation,
NuGet.org signer registration, and RFC3161-compatible timestamping.

If author signing is selected later, it needs a separate implementation issue
and release-order review. It must sign the final `.nupkg` **before** the
candidate manifest freezes its digest and before GitHub attests that subject;
NuGet.org can then repository-countersign the submitted package.

### Package-level SBOM: deferred

The project does not currently publish package-level SBOMs. A repository-wide
or low-confidence inventory would overstate its precision: a useful SBOM must
reproducibly map each shipped package, including source-only and build assets,
to its actual components.

If an SBOM is selected later, a separate focused issue must define the package
mapping, deterministic generation/versioning, freeze point relative to the
manifest, and whether each SBOM becomes a separately attested release-evidence
subject. No SBOM or provenance badge is added merely for a score, and this
guide makes no formal SLSA-level claim.

## Post-publication confirmation

After the first real release, rerun the GitHub-asset steps against its release
attachments, use the NuGet.org path for the primary package, and confirm live
symbol-package behavior through the supported service contract. Any unexpected
signature, identity, or attestation failure is a release defect; this follow-up
does not block the publication that creates the live artifacts.
