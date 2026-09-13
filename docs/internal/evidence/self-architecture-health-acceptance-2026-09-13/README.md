# Self-architecture health acceptance evidence — 2026-09-13

This bundle records the repository-side acceptance evidence for [#805](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/805) after the frozen #804 baseline. It is supplemental to the canonical CLI schemas: the JSON files are preserved as gzip/base64 payloads so the evidence remains reviewable after Actions artifact retention expires.

## Identity and method

| Field | Value |
| --- | --- |
| Repository | `eugenemalaschuk-source/arch-linter-net` |
| Final source revision | `d1a0c3b8` — implementation commit for this acceptance PR |
| Frozen baseline revision | `929983eb985dab934f886cc2f8e25824ac854984` — #804 authority |
| Policy | `architecture/dependencies.arch.yml`; imported audit fragment `architecture/policy/audit-conventions.arch.yml` |
| Policy blobs | final `1f6bc009aba9263d9e34bad284c4bb8e1770e506` / `d125d97c87db6e7a30c416bd58f26f3ba47b6f53`; baseline fragment `2d0211f73ba50eb9fa2c04d91e691e3dd623b71d` |
| Runtime and mode | .NET 10; strict and audit analysis with `--ensure-built`; Health with an explicit empty v3 baseline |
| Execution context | `local-issue-805-audit-d1a0c3b8` |

The frozen and final snapshots were collected against their corresponding source trees. The final
tree has no handwritten production type declared in more than one source file; test partial
fixtures remain covered by dedicated source-index tests.

## Before / after result

| Evidence | Frozen #804 baseline | Final tree | Interpretation |
| --- | ---: | ---: | --- |
| Strict gate | pass | pass | The strict gate stayed fail-closed and green. |
| Audit findings | 41 | 0 | 41 findings resolved; no new findings. |
| Audit change report | — | 41 resolved / 0 new / 11 added surfaces | Added surfaces are architecture inventory entries, not findings or debt. |
| Explicit declaration-count waivers | 14 metadata-incomplete entries in baseline evidence | 0 | The temporary ratchet and duplicate audit authority were removed. |
| Coverage | baseline recorded in #804 bundle | 348 covered, 0 excluded, 0 uncovered, 0 stale, 0 unknown | Full local coverage report is preserved below. |
| Effective controls | 81 in the frozen report | 80: 68 strict, 8 audit, 4 coverage | The removed audit duplicate is no longer counted as an independent control. |

The structural change is distinct from metadata, status, or configuration cleanup: the final audit
snapshot resolves the 41 source/layout findings, and the strict policy now directly enforces
`max_declarations_per_type: 1` over production `src`. The current Health projection is `gate=pass`,
`health=healthy`, with zero waiver debt; optional applicability, history, metrics, topology,
external-evidence, and policy-weakening dimensions remain explicitly `not_configured` rather than
being presented as successful evidence.

## Public parity readback

The existing public chain was verified on merged PR [#858](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/858): the producer CI run [34748754018, attempt 1](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/34748754018) passed Architecture Coverage and Architecture PR Report Gate, and the sticky report [comment](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/858#issuecomment-5647951010) was complete with `gate=pass` and `health=healthy`.

The current merged-main publication readback is intentionally recorded as a blocker, not silently
treated as fresh:

- `main` is `a76bed1c2288b0fe53fa3beaa5ed2c485085d417`.
- The latest badge promotion run [34749051933](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/34749051933) failed closed with `artifact_download_failed`.
- The raw [publication receipt](https://raw.githubusercontent.com/eugenemalaschuk-source/arch-linter-net/architecture-health-badge/architecture-health-publication.json) still identifies the last verified publication as PR #856, publisher run 34693263174, for `main_sha=b46d5e752c65554c4b5a5f8f632e196bac613d7a`; it therefore exposes the mismatch instead of claiming current freshness.
- The raw [badge payload](https://raw.githubusercontent.com/eugenemalaschuk-source/arch-linter-net/architecture-health-badge/architecture-health.json) is `PASS · HEALTHY · 0 ignores · 81 rules`, but its receipt is older than current `main` and must not be used as a current verdict until a successful post-merge promotion replaces it.

This is the required recovery diagnostic for the existing static path: the publisher did not
overwrite newer state with an unbound artifact. The final private request-time Relay/lease path
remains outside #805 and belongs to the separately owned #825 → #806 work; no Relay/runtime bytes
were added here. A maintainer must complete a successful post-merge publication readback before
#784 can be closed as fully accepted.

## Payload verification

Decode a payload with `base64 -D < file.gz.base64 | gzip -dc > file` on macOS or
`base64 --decode < file.gz.base64 | gzip -dc > file` on GNU systems. The first hash is the original
output SHA-256; the second protects the repository-committed encoded payload.

| Original output | Original SHA-256 | Encoded-file SHA-256 |
| --- | --- | --- |
| `architecture-strict.json` | `ddc9b283b4f860da7dbcaef73d42e1469823262b0763ce1a7f6df0008fb65001` | `26cc830c3a59ec8ad3642df1de0372de65faceb0225cb55782ec2046283e4001` |
| `architecture-audit.json` | `1fe39915de0bc808b4f71cdc9f5dddbb49cdc6a259bccb7aa3682910c584f060` | `3da3de3cbd8e413779aa74cd2e0a23cf208ae391079fbb6a38b15b202862c68d` |
| `architecture-health.json` | `37bb77b189370d73902cb31f48fab2ebf24295a2092fd1c464e59b59bdc105e0` | `9ffac720f94084a9df81cf0c832e487c2ff1b9f8093b62bc3385949b992dbe85` |
| `architecture-coverage.md` | `cb5d1a0b70b197a3b0c7ae994ca7afb261d4dc099adb8ac14b36d16d1b70571a` | `da8279725fa46271919b5bcaf19d1c986f5b5909b532c30d6974f8371d6841ce` |
| `architecture-frozen-audit-snapshot.json` | `13dd7f19060831735fcac4fff410a8dcbee0905c7776801d0a369ffb530adca1` | `2ba5b4aaea53ab6f6547f63cfe65adf966c655947f8b207b83b3cb188b2e08f2` |
| `architecture-current-audit-snapshot.json` | `1cd129061dd89b418b39ea9cbbd3c99d7ec30754076cd9c0e4a7e038cc1caa62` | `eac12feb678e26811b4f291852a2fb58f15a4c282ca6b2ecf8803a9cb1b5a080` |
| `architecture-audit-change.json` | `df24d2e5880e985065a04dab02e679b24410a90ae9d64549cbbe42485559bf48` | `c12223cd2bfcc4434b441f8f88101f88f42212d80616818fc31beba935faf0f1` |

The frozen raw bundle remains at [self-architecture-health-baseline-2026-09-07](../self-architecture-health-baseline-2026-09-07/README.md). This acceptance bundle is not a release-publication artifact and does not authorize package, tag, GitHub Release, or documentation deployment.

## Local validation record

Passed: `make fmt`, the focused Core self-policy fixtures (27/27), `make lint-architecture`,
`make lint`, `make public-api-check`, and `openspec validate --all` (165/165). `make lint-docs`
also passed with the repository's existing informational navigation/link notices.

The full local `make test` attempt was not clean on this macOS host: the CLI shard had one known
filesystem identity failure, two Core lifecycle tests exceeded the existing 15-second duration
guard under the loaded host, and the separate Checkpoint B process was interrupted after it
stopped producing output. The change-related architecture tests passed; cross-platform CI remains
the authoritative full-suite check for this PR.
