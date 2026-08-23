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

Install a current [GitHub CLI](https://cli.github.com/), `jq`, and work in one
empty directory. Use exactly one of the following acquisition paths. Each path
downloads `package-manifest.json`, `package-checksums.txt`, and every
`.nupkg`/`.snupkg` for one release or rehearsal, and sets `SOURCE_COMMIT` from
the selected GitHub object. Do not mix assets from different runs, versions,
or commits.

```bash
export REPOSITORY=eugenemalaschuk-source/arch-linter-net
export WORKFLOW="$REPOSITORY/.github/workflows/release-nuget.yml"
```

### Obtain a GitHub Release

Use the exact release tag shown on the GitHub Release page. The API lookup
resolves that tag to the commit whose SHA is recorded in the candidate manifest:

```bash
export RELEASE_TAG='<release-tag>'

gh release download "$RELEASE_TAG" \
  --repo "$REPOSITORY" \
  --dir . \
  --pattern 'package-manifest.json' \
  --pattern 'package-checksums.txt' \
  --pattern '*.nupkg' \
  --pattern '*.snupkg'

export SOURCE_COMMIT="$(gh api "repos/$REPOSITORY/commits/$RELEASE_TAG" --jq '.sha')"
if [ -z "$SOURCE_COMMIT" ] || [ "$SOURCE_COMMIT" = "null" ]; then
  echo "Could not resolve the release tag to a source commit." >&2
  exit 1
fi
```

### Obtain a release-rehearsal candidate

Use the workflow run ID and candidate version from the rehearsal run. The
artifact name is the one used by `release-nuget.yml`:

```bash
export RUN_ID='<rehearsal-run-id>'
export CANDIDATE_VERSION='<candidate-version>'

gh run download "$RUN_ID" \
  --repo "$REPOSITORY" \
  --name "nuget-candidate-$CANDIDATE_VERSION" \
  --dir .

export SOURCE_COMMIT="$(gh run view "$RUN_ID" \
  --repo "$REPOSITORY" \
  --json headSha \
  --jq '.headSha')"
if [ -z "$SOURCE_COMMIT" ] || [ "$SOURCE_COMMIT" = "null" ]; then
  echo "Could not resolve the rehearsal run head SHA." >&2
  exit 1
fi
```

The two acquisition blocks are alternatives: run only the block matching the
artifact being verified, then continue with the common trust-order steps below.

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
verify. The manifest-selected filenames are the complete package/symbol
subject set. Compare that set with every downloaded `.nupkg` and `.snupkg`
before calculating any hashes; this fails closed on both missing and extra
release assets:

```bash
jq -r '.packages[] | .package.file, .symbols.file' package-manifest.json \
  | LC_ALL=C sort -u > expected-package-subjects.txt

for asset in ./*.nupkg ./*.snupkg; do
  if [ -f "$asset" ]; then
    printf '%s\n' "${asset#./}"
  fi
done | LC_ALL=C sort -u > actual-package-subjects.txt

if ! diff -u expected-package-subjects.txt actual-package-subjects.txt; then
  echo "Package/symbol release assets do not match the verified manifest." >&2
  exit 1
fi
```

An empty release directory, a missing manifest-selected file, or an extra
`.nupkg`/`.snupkg` therefore stops the procedure before a package can be
accepted. Now derive the expected SHA-256 lines from the verified manifest and
compare both the derived checksum evidence and the downloaded
project-controlled package bytes:

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
- Removing an asset or adding one not named by the verified manifest makes the
  filename-set diff fail before hashing or attestation; do not substitute a
  similarly named file.
- Downloading from a source other than NuGet.org, or changing the downloaded
  package's embedded ID/version, makes the source or `.nuspec` identity check
  fail before signature verification.

## Verify a package downloaded from NuGet.org

This is a distinct post-publication path. NuGet.org repository-signs submitted
primary `.nupkg` files, adding a repository signature (or a countersignature
when an author signature exists). That is expected platform behavior: the raw
SHA-256 of a NuGet.org-downloaded `.nupkg` is therefore **not expected** to
equal the pre-upload manifest digest or GitHub Release attachment digest.

Start this path in a fresh empty directory, separate from any GitHub Release
asset directory. Set the expected primary package ID and version, and use only
`https://api.nuget.org/v3/index.json` as the source. Create the consumer
configuration there so this path does not depend on a machine- or user-level
NuGet configuration. The `packageSources` entry pins
the source, `signatureValidationMode=require` makes signature policy
mandatory, and `dotnet nuget trust source` obtains the current NuGet.org
repository certificates from its v3 service index. The identity check below
uses only the Python 3 standard library:

```bash
cat > nuget.config <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="signatureValidationMode" value="require" />
  </config>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

dotnet nuget trust source nuget.org \
  --source-url https://api.nuget.org/v3/index.json \
  --configfile ./nuget.config

dotnet nuget trust list --configfile ./nuget.config
```

Review the list and keep the generated `trustedSigners` repository entry with
the verification record. If NuGet.org rotates its repository certificates,
rerun the `trust source` command to refresh this consumer policy. The
generated block must contain a `repository` whose `serviceIndex` is
`https://api.nuget.org/v3/index.json` and one or more current `certificate`
entries with `hashAlgorithm="SHA256"` and
`allowUntrustedRoot="false"`; do not hand-copy stale certificate fingerprints.
The configuration is intentionally explicit: `--configfile` makes these
`packageSources` and `trustedSigners` settings the only settings used by the
verification command. Then verify the downloaded primary package with NuGet's
supported signature tooling. Downloading through the pinned source is part of
the check; do not substitute a package obtained from an unrecorded URL:

```bash
export PACKAGE_ID=ArchLinterNet.Core
export PACKAGE_VERSION='<version>'
export NUGET_SOURCE=https://api.nuget.org/v3/index.json

dotnet package download "$PACKAGE_ID@$PACKAGE_VERSION" \
  --source "$NUGET_SOURCE" \
  --configfile ./nuget.config \
  --prerelease \
  --output ./nuget-package

export PACKAGE_FILE="./nuget-package/$PACKAGE_ID.$PACKAGE_VERSION.nupkg"
if [ ! -f "$PACKAGE_FILE" ]; then
  echo "NuGet did not produce the expected package path: $PACKAGE_FILE" >&2
  exit 1
fi

python3 - "$PACKAGE_FILE" "$PACKAGE_ID" "$PACKAGE_VERSION" <<'PY'
from __future__ import annotations

import sys
from pathlib import Path
from zipfile import ZipFile
from xml.etree import ElementTree


package_path = Path(sys.argv[1])
expected_id, expected_version = sys.argv[2:]


def local_name(tag: str) -> str:
    return tag.rsplit("}", maxsplit=1)[-1]


def child_text(parent: ElementTree.Element, name: str) -> str | None:
    for child in parent:
        if local_name(child.tag) == name:
            return (child.text or "").strip()
    return None


with ZipFile(package_path) as package:
    nuspecs = [name for name in package.namelist() if name.lower().endswith(".nuspec")]
    if len(nuspecs) != 1:
        raise SystemExit(f"Expected exactly one .nuspec in {package_path}, found {len(nuspecs)}.")
    root = ElementTree.fromstring(package.read(nuspecs[0]))

metadata = next((child for child in root if local_name(child.tag) == "metadata"), None)
if metadata is None:
    raise SystemExit(f"Package {package_path} has no metadata element.")

actual_id = child_text(metadata, "id")
actual_version = child_text(metadata, "version")
if (actual_id, actual_version) != (expected_id, expected_version):
    raise SystemExit(
        f"Package identity mismatch: expected {expected_id}@{expected_version}, "
        f"found {actual_id}@{actual_version}."
    )
print(f"Verified package identity: {actual_id}@{actual_version}")
PY

dotnet nuget verify "$PACKAGE_FILE" \
  --all \
  --configfile ./nuget.config
```

The `dotnet package download` command proves the selected source and requested
ID/version, the embedded `.nuspec` check proves the downloaded bytes carry that
same identity, and `dotnet nuget verify` validates the repository signature
under the explicit trusted-signer configuration. See the
[.NET verification command](https://learn.microsoft.com/dotnet/core/tools/dotnet-nuget-verify)
and [NuGet trust-boundary guidance](https://learn.microsoft.com/nuget/consume-packages/installing-signed-packages)
for supported platform and trusted-signer configuration details. The
[NuGet trust command](https://learn.microsoft.com/dotnet/core/tools/dotnet-nuget-trust)
and [nuget.config reference](https://learn.microsoft.com/nuget/reference/nuget-config-file)
describe the `trustedSigners` repository and certificate properties used above.

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
