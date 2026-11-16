# Design

## Context

See `proposal.md` for the motivation. `LineChurnCalculator` already trims common line prefixes and suffixes, interns exact byte lines, and computes LCS length with two rows over the shorter reduced sequence. The shared #502 catalog already provides three sparse and three dense History workloads for this issue.

## Goals / Non-Goals

**Goals:**

- Make exact LCS work scale with input length for small edit distance.
- Bound fast-path search and memory, and retain the existing exact two-row fallback for dense differences.
- Prove parity and capture repeatable algorithmic and environment-specific performance evidence.

**Non-Goals:**

- Change raw-byte line semantics, reports, rename/binary routing, or public APIs.
- Replace line interning or reduce copied-line/hex-symbol allocations without separate measurement evidence.
- Claim sub-quadratic worst-case exact LCS; dense fallback remains quadratic in time.

## Decisions

### Use a bounded Myers frontier to find only the edit distance

The calculator needs the LCS length, not an edit script. A forward Myers frontier can find the shortest insertion/deletion distance `D` without storing a trace; then `LCS = (n + m - D) / 2`. Use a fixed maximum distance `K = 32`, which covers the existing 10,000-line, 16-replacement sparse workload (`D = 32`) while routing the 50%-replacement dense workloads to fallback. The initial `K = 64` measurement exceeded the dense timing gate at 1.326x baseline, so the cutoff was reduced to the largest catalogued sparse distance. If `abs(n - m) > K`, skip the search because no path within the bound can exist.

For a completed search, frontier work is `O((n + m)(D + 1))` and auxiliary frontier memory is `O(K)`. If the search exhausts `K`, discard the bounded frontier and run the existing exact two-row DP from the beginning. Fallback time remains `O(nm)` and DP memory remains `O(min(n,m))`; the fast path never stores a quadratic edit trace.

Alternatives rejected: a full Myers trace can consume `O(D²)` memory on dense inputs; Hunt–Szymanski can expand to a large matching-pair set on repeated lines; bit-parallel LCS adds alphabet-indexed state and is not needed to satisfy the small-distance contract. The existing two-row DP remains the simplest exact dense fallback.

### Preserve current symbolization and line normalization

Keep prefix/suffix trimming, byte equality, NUL and exact-rename routing, and the existing string-based line symbol table. This isolates the algorithm change and makes before/after allocations comparable. Any later line-copy or hex-string optimization needs its own materiality evidence.

### Use the shared workload catalog and keep timing evidence informative

Run the existing `line_churn_sparse_edits` and `line_churn_dense_edits` 1k/5k/10k catalog cases. Capture deterministic baseline DP cell counts, exact result parity, direct calculator time/allocated bytes, and complete `HistoryIngestionService.Ingest` call timings. Keep deterministic work and result assertions in NUnit tests; timing and allocation observations are informational and belong in the existing #502 evidence area. Do not fail tests on elapsed-time comparisons against saved results because CI hosts and local machines are not guaranteed to match.

Success requires exhaustive small-input parity, the large sparse case to reduce deterministic work below 5% of its baseline DP cells, and dense cases to use the exact fallback with the expected DP-cell count. Compare timing medians only from explicitly paired captures on the same host, and report the environment and sample spread so readers can judge the evidence. Report peak temporary/retained memory as not measured unless the runner provides a reliable per-workload measurement; report the frontier and DP array bounds from the implementation separately.

## Risks / Trade-offs

- **Risk:** Diagonal parity or repeated-line tie cases can produce an incorrect distance. → **Mitigation:** Compare all short sequences over a repeated-symbol alphabet against a test-only quadratic LCS oracle, plus seeded random and named edge cases.
- **Risk:** Dense inputs pay for a fast-path attempt before fallback. → **Mitigation:** Stop at `K = 32`, test dense scale cases, and record environment-labelled timings without making them hardware-sensitive test assertions.
- **Risk:** An input with small but greater-than-32 distance gets no fast-path benefit. → **Mitigation:** Keep the cutoff explicit and bounded; only raise it with measured sparse/dense evidence.
- **Risk:** Extra line-symbol allocations dominate the sparse run and hide the DP improvement. → **Mitigation:** Keep symbolization stable for this change and report allocation results independently from operation counts.

## Migration Plan

No migration is needed. After implementation and focused validation, synchronize the existing `release-architecture-forensics` spec by archiving this OpenSpec change, then run `openspec validate --all`.
