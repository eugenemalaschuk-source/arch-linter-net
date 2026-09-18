# Badge distribution and compatibility

> **Experimental / opt-in Private Relay.** This inventory describes included
> Relay code and its compatible assets, not adoption-stable support. Full
> hosted/lifecycle acceptance is still pending. Verify availability and integrity
> against the exact release or candidate; a source checkout is not shipment proof.
> Missing components block adoption; do not assemble a replacement distribution.
> Private repositories default to `none`, with no automatic cloud setup or badge
> egress. Stable core governance and private reporting do not require Relay.

Use [badge adoption](../guides/badge-adoption.md) to choose the mode,
[setup](../guides/badge-setup.md) for installation and doctor, and
[lifecycle operations](../guides/badge-lifecycle-operations.md) for administration.

## One compatible distribution

The release contains `ArchLinterNet.CEL`, `ArchLinterNet.Cli`,
`ArchLinterNet.Core` and `ArchLinterNet.Testing` at one selected package version.
The CLI exposes setup/doctor/lifecycle under `badge architecture-health` and
carries its schemas, fixtures and generated-consumer templates. Provider
runtime dependencies are not added to Core.

The existing package manifest binds primary `.nupkg` and symbol `.snupkg`
subjects. Relay transport has a separate, candidate-bound inventory, not fake
NuGet package entries or a second release engine. The transport subject names
are:

| Subject | Responsibility |
| --- | --- |
| `architecture-health-badge-relay-{version}.tar.gz` | Versioned Relay distribution archive |
| `architecture-health-badge-relay-{version}.json` | Compatibility metadata for that candidate |
| `architecture-health-badge-publisher-workflow.yml` | Approved reusable publisher workflow bytes |
| `architecture-health-badge-publisher-action.yml` | Approved composite publisher action bytes |
| `architecture-health-badge-release-distribution.json` | Exact transport filenames, sizes, media kinds, digests and source identities |
| `architecture-health-badge-release-checksums.txt` | Human-readable checksums derived from that inventory |

`{version}` is the selected candidate/released version, not `latest`, `main` or
an instruction to guess the next patch number. The outer manifest and checksum
evidence are not recursively hashed into their own subject inventory. A file
being present beside the manifests does not authorize its release attachment.

### Runtime and configuration contents

The archive must contain the compatible Worker entrypoint, public payload
validator, read-time expiry/stamped renderer, ownership registry, atomic storage,
security and lifecycle implementation. Deployment also needs the Wrangler
bindings/migration declaration, TypeScript configuration, dependency lockfile,
package metadata, third-party notices and bundle manifest. Runtime imports must
resolve inside the delivered set: a file in the source repository but absent
from the archive is not delivered functionality.

The configuration schema is `schema/0.8.0/badge-relay-config.schema.json`.
That path versions a machine contract; it does not claim the Relay was shipped
in the immutable historical v0.8.0 release. Config/workflow templates are
materialized by the compatible packed CLI; consumers do not write their own
verifier, renderer or server. The generated private registry binds the actual
repository/owner, alias, profile, audience and approved pins, never a synthetic
fixture identity.

### Compatibility identities

| Boundary | Identity |
| --- | --- |
| Relay bundle | `badge-relay/v1` |
| Configuration | `architecture-health-badge-relay-config/v1` |
| Compatibility plan | `architecture-health-badge-relay/v1` |
| Promotion | `architecture-health-badge-promotion/v1` |
| Publication | `architecture-health-badge-publication/v2` |
| Storage migration | `v1` |
| Distribution manifest | `architecture-health-badge-release-distribution/v1` |
| Compatibility metadata | `architecture-health-badge-relay-compatibility/v1` |

These identifiers are not interchangeable with package SemVer. Setup and doctor
must reject missing, unknown, incompatible or tampered components before use.
Review upgrades and rollback using the verified bundle and storage compatibility
plan, not merely matching filename prefixes.

The approved reusable publisher workflow and composite action are bound to
their own immutable **commit SHAs** in the verified distribution. Their source
blobs are also identified.
The generated consumer's `producer.workflow_sha`, in contrast, is the **Git blob
SHA of the generated producer workflow bytes**. It is neither the publisher
commit nor a mutable branch. Rotation requires a reviewed compatible update;
do not substitute `main`, a fork or an arbitrary caller-selected workflow.

## Obtain and verify without source checkout

Use the [release-provenance verification guide](../guides/release-provenance-verification.md)
and the [existing release process](release-process.md) to authenticate the
selected official package and transport evidence before relying on its digests.
A checksum file obtained from an untrusted replacement endpoint is not a trust
anchor. Check version, source, subject sizes/digests and compatible publisher
identity together. Preserve frozen bytes through verification and installation;
a locally repacked archive is a different subject.

A pre-publication evaluation uses an explicitly approved immutable candidate
and its bound evidence before a public release exists. A `main.N` development
build, passing unit test or merged source change is not a release or hosted
acceptance receipt. After publication, installation must be repeated against
the actual downloadable assets and released CLI. Keep the distinction between
project-controlled package bytes and later NuGet repository-signed downloads
as documented by the provenance guide.

The reference above describes verification responsibilities, not an alternative
installer. Follow the commands supported by the installed CLI in the setup
guide. If the package, runtime, pin, prerequisite or installer step is missing,
stop with that delivery defect rather than inventing a source-build workaround.

## Documentation and receipts are part of composition

The user journey consists of this inventory, badge adoption, the executable
setup guide and the lifecycle runbook. Freeze their exact source revision with
the candidate proof. The current transport archive member list does not itself
include those Markdown pages; do not describe them as files inside the tarball.
They must be available as the corresponding reviewed candidate documentation,
and subsequently as the release-owned public documentation.

A full private promotion receipt is not a public badge asset. Candidate
acceptance separately records the package/transport manifest identities,
publisher pins, compatible schemas, four-platform results, hosted synthetic
private-repository flow, cache observations, lifecycle and teardown. It must
prove the same guide and bytes, not a different source build. Public evidence
must not contain tokens or private adopter names, PR/run URLs or private source
provenance. A successful documentation build cannot replace that receipt.

The [release process](release-process.md) owns actual publication and
released-artifact read-back. Ordinary `main` changes do not deploy MkDocs/Pages.
Source documentation may describe this upcoming candidate, but stable-site and
NuGet-facing release claims must agree with what was actually published and
verified. Historical v0.8.0 artifacts remain unchanged.
