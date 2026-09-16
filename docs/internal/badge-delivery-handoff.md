# Badge documentation and delivery handoff

Internal composition record for #836 -> #825 -> #806. This file is excluded
from MkDocs and is not a release manifest or candidate acceptance receipt.

## Verdict at the inspected source

**BLOCKED; no candidate, hosted, or released-artifact PASS is asserted.**

Audit date: 2026-09-16. Inspected source:
`ffd5e97f99cedf4982b4a9c28f1f45b3827ad7e7`.
This is the documentation branch base, not a frozen candidate identity.
Re-evaluate this snapshot against the final integrated source and candidate.

The current #834 issue is open, explicitly requires approved real synthetic
private GitHub/Relay acceptance, and has no attached PASS in the inspected
issue/comment record. The #825 checklist leaves #834 and #836 incomplete.
A closed implementation issue or this documentation PR cannot replace that
proof. #806 remains downstream publication authority; #650/#787 belong to v0.9
performance and are not delivery prerequisites.

### Concrete distribution defect: missing lifecycle module

At the inspected source:

- `relay/src/index.ts` exports `./lifecycle`.
- `relay/bundle-manifest.json` lists `src/lifecycle.ts` with SHA-256
  `920275c1647d6060b3c76ab47814ae1f22bdcb5d236f2ed6d8f0b621c400693c`.
- `.github/badge-promotion/release-inventory.json` omits
  `relay/src/lifecycle.ts` from its closed archive member list.
- `tools/release/release_distribution.py` also omits it from
  `_REVIEWED_BUNDLE_MEMBERS`. Editing only the JSON allowlist is insufficient.

The source composition check reports:

```text
BLOCKED: source inventory composition
- Release inventory omits setup bundle file: relay/src/lifecycle.ts
```

This is an inventory defect, not a claim that a hosted deployment was attempted
and failed. The release archive cannot claim to include the complete lifecycle
runtime while omitting a required exported module. Source checkout and the
CLI's broader `relay/src/**` package glob do not prove completeness of the
separately downloadable transport archive.

**Owning boundary: #835 distribution.** Reconcile the reviewed archive member
allowlists and verify the actual frozen archive/compatibility set; do not ask
an adopter to copy source into a downloaded bundle. Then #834 must exercise
those exact bytes, including lifecycle operations. This PR does not modify
#835 packaging or silently waive the defect.

## Source identities inspected, not candidate digests

| Source record | Observed identity |
| --- | --- |
| Release inventory Git blob | `cdb9039e08e3ec2d8fa361e19b302191e1ac8dd4` |
| Setup bundle manifest Git blob | `0e223988bbdc8bb9a07df0ccfa96f9adc575cd35` |
| Approved publisher commit declared by both manifests | `ff9b19bfe5abcab233d490ea53f55a387dc4a8db` |
| Approved reusable-workflow source blob | `88d05c010023488c29225dda8391ec43268a443b` |
| Approved composite-action source blob | `b400bd026eb8a7ab6e7d55619f0d39cfc65111e6` |

These are observed declarations/source identities. They are not a verified
package manifest digest, tarball digest, attestation, or live receipt. The
inspection did not select a free release version or download frozen candidate
assets. The JSON inventory still declares `publication: not-authorized`.
Do not treat the documentation branch base SHA as the future package source.

## Exact documentation composition

The candidate acceptance must bind the final source revision of these pages:

| Page | Scope and owner |
| --- | --- |
| `docs/guides/badge-adoption.md` | Modes, disclosure, evidence, freshness/cache, cost and migration composition; #836 |
| `docs/reference/badge-distribution.md` | Package/transport/schema/pin inventory and release boundary; #836 consumes #835 |
| `docs/guides/badge-setup.md` | Executable setup/doctor procedure; #832, exercised by #834 |
| `docs/guides/badge-lifecycle-operations.md` | Operator actions and recovery/rollback; #833, exercised by #834 |
| `docs/reference/release-process.md` | Existing publication mechanics and transport inventory authority |
| `docs/guides/release-provenance-verification.md` | Existing package/provenance verification contract |
| `mkdocs.yml` | Stable public navigation and exclusion of internal records |

The source transport allowlist contains no public Markdown pages. Record how
reviewed candidate documentation accompanies the candidate; do not claim it is
embedded in the tarball. #806 must publish the corresponding public site from
the release-owned workflow, not ordinary `main` deployment. Remove the
upcoming-candidate notices only when that release claim is evidenced. No
historical v0.8.0 artifact may be rewritten.

OpenSpec: not applicable to this PR. The public pages compose existing setup,
transport, disclosure and lifecycle contracts; the read-only inspection script
compares existing manifests without changing them. There is no new runtime,
schema, policy, transport guarantee, publication authority or lifecycle API.
No OpenSpec validation/archive execution is claimed.

## Candidate handoff required before #836/#825 closure

Record evidence in the existing Checkpoint B/candidate conventions, not a new
release ledger. The final handoff must include:

- Exact candidate version and source; paired CEL/Core/Cli/Testing package
  manifest and digest; transport manifest/checksum identities and every selected
  asset digest; approved workflow/action blobs/pins; bundle/config/profile and
  storage compatibility. **Not yet verified in this source inspection.**
- #834's public-safe immutable receipt for the same candidate and guide:
  Linux x64, Windows x64, macOS arm64 and macOS x64 packed consumers; privacy,
  adversarial and lifecycle results; real isolated private GitHub -> Relay ->
  README; stopped-all-workflow expiry; measured cache/resource/cost observations
  and teardown. Missing authorization/environment is BLOCKED, not SKIP=PASS.
- All mandatory defects resolved by their owners and the affected evidence
  repeated. Changed candidate bytes or material guide steps invalidate the
  affected previous proof. Do not copy a prior PASS onto a new digest.

Canonical order:

```text
#832 setup + #833 operations + #835 complete frozen distribution
  -> #834 exact-candidate/platform/live receipt
  -> #836 final guide/inventory reconciliation
  -> #825 candidate closure
  -> #806 actual publication and released-artifact read-back
```

Guide preparation can proceed alongside #834; its final reconciliation cannot
pretend that #834 has passed. #806 does not block upstream candidate proof and
cannot be used to defer a missing required component out of #825.

After publication, #806 must verify actual public install/downloads, provenance,
all transport subjects/pins, the same private/public/none journeys and matching
public documentation. Candidate-only or source-only success is not the final
engineering-wave verdict.

## Reproducible source checks

From a full checkout:

```text
python -m pytest -q tools/scripts/tests/test_check_badge_delivery_inventory.py
python tools/scripts/check_badge_delivery_inventory.py
make fmt
make lint-docs
```

The first command tests the inspector's refusal/success behavior. The second
compares the existing setup manifest's required files/pins with the release
owner's existing inventory. It is read-only and never signs evidence, creates
assets, changes required checks, authorizes publishing or turns source
consistency into packed/live acceptance. A nonzero result blocks this handoff.

For this draft, the focused tests and source check are executable on the
API-retrieved files. Full checkout, repository formatting and MkDocs strict
validation were unavailable in the editing environment. The PR records exact
commands/results and these limits. The normal full docs gate and #834 remain
required; an unavailable toolchain is not a successful check.
