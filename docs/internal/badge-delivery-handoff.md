# Badge delivery documentation and prepublication handoff

Status: **BLOCKED** for final #836 delivery closure. Documentation composition
is not a release or acceptance verdict. This maintainer record is excluded
from the public MkDocs site by `exclude_docs: internal/`.

## Audit boundary and ownership

The publisher source audit was refreshed after #933 at
`17caba8fd9c28b656f8bf1439098be5e1692d167` on 2026-09-17. That SHA is the
reviewed publisher revision for the #918 delivery-pin rotation, **not** a
frozen or approved final `0.8.Z` package candidate.
The original audit at `ffd5e97f99cedf4982b4a9c28f1f45b3827ad7e7` identified
the packaging defect described below; it is retained only as historical context.
No candidate version, package hash, deployment identity, or acceptance run is
inferred from either audit. Refresh this record against the chosen candidate
after any relevant source, schema, packaging, or pin change.

The consumer entry is [badge adoption](../guides/badge-adoption.md), leading to
[setup](../guides/badge-setup.md) and the
[lifecycle runbook](../guides/badge-lifecycle-operations.md). It composes the
existing contracts; it introduces no new CLI, protocol, schema, runtime, or
provider promise. OpenSpec change: not applicable to this documentation-only
composition. The existing #826 ADR and capability specifications remain
normative, not this inventory.

| Owner | Authority and evidence consumed here |
| --- | --- |
| #826 | Frozen storage, identity, disclosure, and freshness contract/ADR. |
| #827 | Canonical public projection and semantic validity; no new evaluator. |
| #828 | OIDC trust, atomic registry/storage, and fail-closed writes. |
| #830 | Trusted reusable publisher, authoritative PR proof, metadata renewal. |
| #831 | Public JSON/SVG rendering and read-time expiry. |
| #832 | Installer, embedded bundle/configuration, generated templates, setup commands. |
| #833 | Lifecycle implementation and operator runbook. |
| #835 | Exact release distribution, asset integrity, packed installation integration. |
| #834 | Packed/platform/adversarial matrix and real synthetic private GitHub -> deployed Relay -> README acceptance. |
| #836 | Documentation composition, exact-candidate evidence review, and final prepublication verdict. |
| #825 -> #806 | Parent closure, then reviewed release scope and verification of actually published artifacts. |

A completed implementation checkbox does not replace a matching package,
workflow, platform, or live acceptance result. Do not close #836/#825 merely
because this documentation PR merges.

## Distribution inventory: source contract, not shipment proof

The machine-readable owner is
`.github/badge-promotion/release-inventory.json`
(`architecture-health-badge-release-inventory/v2`), consumed by
`tools/release/release_distribution.py`. The bundle member/digest authority is
`relay/bundle-manifest.json`. Keep these authorities; do not create a parallel
packager or silently repair a released archive in a documentation command.

| Delivery component | Exact identity or path to bind in candidate evidence |
| --- | --- |
| NuGet package family | `ArchLinterNet.CEL`, `ArchLinterNet.Cli`, `ArchLinterNet.Core`, `ArchLinterNet.Testing`; exact candidate version and each package digest. |
| CLI setup surface | Packed `ArchLinterNet.Cli`; setup/doctor/lifecycle help and behavior from that installed package, not `dotnet run`. |
| Relay distribution archive | `architecture-health-badge-relay-{version}.tar.gz` |
| Compatibility description | `architecture-health-badge-relay-{version}.json` |
| Reusable workflow asset | `architecture-health-badge-publisher-workflow.yml` |
| Composite action asset | `architecture-health-badge-publisher-action.yml` |
| Distribution manifest | `architecture-health-badge-release-distribution.json` (`architecture-health-badge-release-distribution/v1`) |
| Distribution checksums | `architecture-health-badge-release-checksums.txt` |
| Workflow source | `.github/workflows/architecture-health-badge-promotion.yml` |
| Action source | `.github/actions/architecture-health-badge-promotion/action.yml` |
| Worker/storage/dependencies | `relay/src/`, `relay/wrangler.jsonc`, `relay/tsconfig.json`, `relay/package.json`, `relay/package-lock.json`, `relay/THIRD-PARTY-NOTICES.txt`, and `relay/bundle-manifest.json`; verify every required member and digest. |
| Configuration schema | `schema/0.8.0/badge-relay-config.schema.json` |
| Generated consumer outputs | Exact setup configuration and manifest, producer/publisher/optional renewal workflows, registry and Wrangler bindings, managed README block and approved endpoint; obtain bytes/hashes from the packed CLI run. |

`{version}` is a filename template, not an approved version or a working
download link. A manifest containing a component name is not proof that the
archive includes a usable copy. The four-package release family is not a
requirement to install four packages just to use the CLI.

After the #933 parser-safe nested-action fix, the approved publisher commit is
`17caba8fd9c28b656f8bf1439098be5e1692d167`. The inventory binds the workflow
blob `6975574d5028c975fea3981c88fb0057af624076` and action blob
`0628a09ad75bfb6c4da4acdae041a49681964a69` at that commit. These are Git
object identities, not distribution SHA-256 digests. Copy the full pinned
workflow/action refs from the chosen candidate inventory, not from `main` or
this source-audit paragraph. Bind the separate generated producer Git-blob SHA
from setup as well.

The workflow blob changed in #926 to use an immutable repository-qualified
action reference. The immutable revision also includes the #917 exact-job
attempt fix and the #919 consumer registry loader. Matching YAML alone does not prove that the called Python
publisher contains those fixes. The shipped-publisher regression exports the
approved revision and runs the current loader/attempt contract tests against
that runtime in a separate interpreter, without using working-tree product
code. CLI defaults, schema enums, bundle pins and release inventory must agree;
schema and whole-bundle digests are recomputed as part of the reviewed change.
Existing prepublication configurations are not silently rewritten or granted
new authority. Recreate/review candidate setup outputs from the updated CLI
and rerun affected proofs; this rotation is not hosted #834 acceptance.

| Compatibility field | Audited identity |
| --- | --- |
| Bundle | `badge-relay/v1` |
| Configuration | `architecture-health-badge-relay-config/v1` |
| Plan | `architecture-health-badge-relay/v1` |
| Promotion | `architecture-health-badge-promotion/v1` |
| Publication | `architecture-health-badge-publication/v2` |
| Storage | `v1` |
| Distribution compatibility manifest | `architecture-health-badge-relay-compatibility/v1` |

## Packaging repair: #905 / #907 in the #835 distribution path

At the original audit SHA, `relay/src/index.ts` imported `./lifecycle.js`, and
`relay/bundle-manifest.json` declared `src/lifecycle.ts`, but both distribution
member allowlists omitted `relay/src/lifecycle.ts`. This source-level defect
was tracked separately as #905, not repaired inside the documentation change.

#907 fixed both `release-inventory.json:bundle_members` and
`release_distribution.py:_REVIEWED_BUNDLE_MEMBERS`, and was merged at
`dc54fa9a0528c2f56ce6c260cfc9e12d38cfbde0`. The regression module
`tools/release/tests/test_release_distribution_bundle_manifest.py` checks all
declared bundle files against both allowlists and builds an archive to check
the exact `relay/src/lifecycle.ts` bytes. The omission is no longer an open
source-level blocker on this baseline.

The following ordinary PR runs succeeded on the tested #907 head
`828d2fb80b12e7635107138309776c1388a3c7d5`:

| Validation | Run reference for the tested head | Result |
| --- | --- | --- |
| CI | [35081939550](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35081939550) | PASS |
| Package Validation | [35081939478](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35081939478) | PASS |
| CodeQL | [35081939467](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35081939467) | PASS |

These results are evidence for that head, not a final #834 acceptance receipt
or validation of the rebased documentation head. The repaired archive member
set invalidates affected pre-fix completeness proofs: produce a fresh immutable
candidate and rerun the matching packed/install, platform, and live checks.
Do not carry old package hashes or PASS forward to new bytes, copy a missing
file into an extracted archive, relax the bundle manifest, or treat ordinary
PR CI as the real private-consumer-to-README acceptance.

## Evidence required for a final verdict

Each result must bind to the same reviewed candidate and expose a reproducible
command/run reference, expected result, actual result, and redacted evidence
location. A mutable branch name or an unbound screenshot is insufficient.

| Required record | Current state / closing evidence |
| --- | --- |
| Candidate selection | BLOCKED: no frozen final `0.8.Z` version/source SHA, approved scope, or matching candidate manifest recorded here. |
| Packages and distribution | BLOCKED for final candidate proof: #905/#907 repaired the source member set; require fresh exact package hashes, archive/compatibility/workflow/action assets, distribution manifest/checksums, and verified bundle contents. |
| Packed command proof | BLOCKED: require installed-package setup/doctor/lifecycle command outputs, dry-run/no-write behavior, generated output hashes and endpoint/identity bindings; source tests are insufficient. |
| Platform and adversarial proof | BLOCKED: require #834's matrix for the exact candidate, including supported platforms, trust/storage/expiry/cache/lifecycle negatives and no weakened checks. |
| Real private-to-public reference | BLOCKED: #834 remains open at this refresh; no reviewed real private GitHub -> deployed Relay -> README PASS is attached to this record. |
| Documentation verification | PENDING: require strict MkDocs build, Markdown formatting/lint, navigation/link checks, and command-to-packed-evidence reconciliation on the final documentation/candidate combination. |
| Public artifact verification | DEFERRED TO #806: only after authorized publication; not claimed by this prepublication record. |

The live record must cover first authoritative PR/squash/tree proof, unchanged
headline with a fresh receipt, opt-in disclosure, metadata-only renewal, stopped
jobs and origin expiry, semantic invalidation, revoke/restore/rollback and
negative authorization, plus separate origin versus cached-README observations.
It must demonstrate `none` and public-only `github-raw` compatibility as well
as the Relay path, without exposing the private source repository's identity
or payload in public evidence.

Before live execution, record explicit maintainer approval for the isolated
synthetic GitHub account/repository, bounded Cloudflare namespace, quotas and
cost ceiling, and scoped operator authority through a protected pinned workflow.
No such approval is established by a merge or an ordinary PR CI success. Missing
or unverified prerequisites leave the live gate BLOCKED; do not provision cloud
resources or enable privileged live execution implicitly.

Retain exact private repository/deployment/run/receipt bindings only in the
restricted acceptance record. Publicly retain redacted outcomes and hashes
where approved; never publish credentials, JWTs, full provider responses,
private source paths, Health payloads, or identifying proof material. Synthetic
fixtures and screenshots alone do not satisfy the real-run requirement.

Use the repository's existing validation entrypoints for the documentation:

```text
make venv
make docs-build
make fmt-docs
make lint-docs
python -m pytest tools/release/tests/test_badge_adoption_documentation.py
```

The focused Python checks cover documentation links, public/internal boundaries,
and inventory drift only. They do not execute the CLI, build a Relay archive,
validate provider capabilities, or replace #834. Record unavailable tools,
credentials, accounts, permissions, or quotas as BLOCKED for the affected
acceptance, never as SKIP/PASS.

## Closure and publication boundary

Current verdict: **BLOCKED**. The source packaging repair is merged; next obtain
a fresh immutable #835 candidate and #834's matching packed/platform/live
evidence. Then #836 reconciles every consumer command, inventory identity, and
prerequisite against those results.
Only a complete reviewed prepublication PASS may close #836 and then #825.
Pass the frozen candidate and redacted evidence index to #806; #806 separately
authorizes publication and verifies the actually released artifacts.

Already released `v0.8.0` assets remain immutable. Do not select a patch version,
rewrite stable README/NuGet adoption claims, publish packages, deploy a
project-owned Relay, or enable Pages deployment on ordinary `main` pushes in
this task. No badge command changes `badge architecture-policy` or removes the
public `github-raw` compatibility path. `v0.9-performance`, #650, and #787 are
explicitly excluded from this v0.8.x closure.
