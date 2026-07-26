## 1. Shared lifecycle model and comparison

- [x] 1.1 Add `BaselineEntryLifecycle` with canonical wire names and a lifecycle-tagged entry model in Core.
- [x] 1.2 Make `ArchitectureBaselineComparer` count matching candidates so exactly-one and more-than-one are distinguishable, and classify ambiguous entries.
- [x] 1.3 Add optional `issue` metadata to the baseline entry model, generator round-trip, and comparison entry, excluded from identity and matching.

## 2. Preview, reason mapping, and safe writes

- [x] 2.1 Return lifecycle-classified entries and the proposed document from generate/update/prune outcomes.
- [x] 2.2 Resolve per-contract and per-family reason mapping for newly added entries only, with fail-closed argument parsing.
- [x] 2.3 Preserve the leading comment header on update/prune and refuse with an actionable diagnostic when interior comments cannot be round-tripped.
- [x] 2.4 Add `--dry-run`, stdout output, `--force` overwrite gating, and atomic temp-then-rename writes to the baseline subcommands.

## 3. Reporting

- [x] 3.1 Emit lifecycle status, counts, and canonical identity in the human and JSON output of every baseline subcommand.
- [x] 3.2 Fail `baseline verify` on stale, ambiguous, and configuration entries; keep `diff` a zero-exit report.

## 4. Verification and documentation

- [x] 4.1 Add Core tests for lifecycle classification, ambiguity counting, issue preservation, reason mapping, and comment handling.
- [x] 4.2 Add CLI tests for dry-run, stdout, force gating, failed-write preservation, commented fixtures, and JSON lifecycle output.
- [x] 4.3 Update the baseline JSON schema, migration-baselines guide, and CI-integration guidance that CI never updates baselines.
- [x] 4.4 Run focused tests, `make fmt`, `make acceptance`, and OpenSpec validation; archive the change.
