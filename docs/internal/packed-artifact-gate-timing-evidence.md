# Packed-artifact gate timing evidence

Issue #587 tracks this record. Durations below are GitHub Actions job wall times; they include
runner setup, restore, test-project build, candidate installation, and evidence upload. They are
not estimates based on local execution.

## Baseline and first fan-out

The issue baseline on PR #585 was a Windows packed-artifact job of **7:38**, of which the
monolithic Checkpoint B test consumed **5:31**. A clean four-shard PR measurement on
[run 31868313623](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/31868313623)
reduced the Windows job to approximately **5:33**, but did not meet the ≤4 minute requirement.

The six-shard release measurement on
[run 31901827260](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/31901827260)
proved the candidate/evidence topology end-to-end. It also identified the remaining Windows
critical shards:

| Shard | Windows duration |
| --- | ---: |
| Adopter runtime | 3:28 |
| Public API selector | 3:19 |
| Consumer cleanup policy foundation | 3:03 |

The six resulting scenario buckets ran once per platform and merged to one canonical record;
the run completed successfully, with all four canonical platform-evidence jobs passing. Its
candidate job ran `make acceptance-repository` for **8:22**, then performed strict OpenSpec,
the release-version build, and package construction before any Checkpoint B shard began. No
generic acceptance stage reran packed-artifact proof.

## Follow-up split

The prior nine-shard topology separated the two independent Windows hot paths:

- adopter runtime: core fixtures versus extended/cache evidence;
- public-API selector: snapshot/role, delta/membership, and enforcement/Testing-adapter parity.

Its release rehearsal showed that consumer-cleanup policy foundation (3:03) still threatened the
four-minute PR critical path once candidate preparation and fan-in were included. The intermediate
ten-shard topology separated that work into policy execution and policy contracts plus typed
policy shape. Its Windows critical shard still reached 3:29 including runner setup, so the final
eleven-shard topology separates dependency-contract parity from layer overlap and typed policy
shape, while preserving every scenario ID exactly once.

Every producer emits exactly one named shard record. The merger requires all eleven records and
rejects duplicate, missing, or overlapping scenario IDs before it emits canonical platform
evidence; only the layer-overlap-and-policy-shape shard may report the typed policy-shape counters.
The final timing is recorded after the corresponding PR and non-publishing release workflows
complete. Platform-specific fan-ins depend only on their own producer matrix, so a constrained
Apple Silicon queue cannot inflate the Windows branch-protection critical path.

## Final PR measurement

The clean eleven-shard PR run [31905571593](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/31905571593)
completed successfully. Its immutable candidate ran from **20:01:55** to **20:02:38 UTC** (43 s).
All eleven Windows shards passed; the slowest was adopter-runtime extended at **3:02**. The stable
`Packed Artifact Test Suite (Windows)` fan-in completed at **20:05:54 UTC**, for an end-to-end
candidate-to-required-context critical path of **3:59** — under the issue target of four minutes.

Apple Silicon macOS completed its independent eleven-shard evidence and stable fan-in successfully
at **20:07:01 UTC**. The two platform fan-ins each merged exactly one canonical record from all
eleven named shard records, confirming that scenario evidence remains complete and fail-closed.

## Final release rehearsal

The non-publishing release rehearsal [31905879344](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/31905879344)
completed successfully with `publish=false`. Repository acceptance ran from **20:09:02** to
**20:20:23 UTC** (11:21), after which strict OpenSpec validation, the release-version build, and
immutable candidate packaging completed before any Checkpoint B job started. The single release
matrix then ran all **44** platform/shard jobs (eleven shards on each of Linux x64, Windows x64,
Apple Silicon macOS, and Intel macOS) successfully. Every canonical platform fan-in and the final
evidence aggregation passed. Package publication, GitHub release creation, and documentation
publication were skipped, so the rehearsal made no release-side writes.
