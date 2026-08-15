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

The nine-shard topology separates the two remaining independent Windows hot paths:

- adopter runtime: core fixtures versus extended/cache evidence;
- public-API selector: snapshot/role, delta/membership, and enforcement/Testing-adapter parity.

Every producer emits exactly one named shard record. The merger requires all nine records and
rejects duplicate, missing, or overlapping scenario IDs before it emits canonical platform
evidence. The final timing is recorded after the corresponding PR and non-publishing release
workflows complete.
