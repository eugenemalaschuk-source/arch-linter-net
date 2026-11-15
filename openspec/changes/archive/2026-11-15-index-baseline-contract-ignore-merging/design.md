# Design

## Context

See `proposal.md` and the `baseline-generation` delta. `ArchitectureBaselineLoadingService.ContractGroupMerger` currently builds each group list, linearly finds the first contract whose ID matches with `OrdinalIgnoreCase`, then scans that contract's accumulated ignore list for every baseline ignore. The structured deduplicator is intentionally narrower than general finding identity equality.

## Goals / Non-Goals

**Goals:**

- Remove the per-entry contract scan and per-ignore accumulated-list scan.
- Preserve group-local, case-insensitive first-match contract lookup.
- Preserve v1 exact display-pair and v2/v3 structured merge-key behavior, list ordering, reason precedence, loaded-baseline provenance, filtering, and diagnostics.
- Retain deterministic work counts and reproducible, environment-labelled before/after measurements using the existing #502 evidence approach.

**Non-Goals:**

- Change public APIs, baseline formats, identity definitions, validation outcomes, or suppression scope.
- Index comparisons, waivers, lifecycle matching, or other baseline paths owned by sibling issues.
- Add a reusable benchmark framework or claim an end-to-end speedup without a measured phase share.

## Decisions

1. **Build one contract lookup for each non-empty baseline group.** Iterate the group's contracts in their existing order and add IDs to a `StringComparer.OrdinalIgnoreCase` dictionary with first-write-wins behavior. Resolve every regular baseline entry with one lookup. Keep imported external-diagnostic handling before contract resolution so those entries continue to resolve against external evidence IDs.

2. **Build membership indexes lazily for touched contracts.** When a valid, non-applicability baseline ignore first targets a contract, seed its version-specific set from that contract's existing inline ignores. Add each accepted loaded key to the set as well as to the existing ordered list. Applicability-only entries therefore allocate no ignore set.

3. **Represent the exact existing merge key.** For v1, use the ordinal `(SourceType, ForbiddenReference)` pair. For v2/v3, use the fields compared by the current `IdentityEquals`: contract family, source assembly/type/member, target assembly/type/member, occurrence defaulted to zero, and configuration, all with ordinal string equality. Only existing inline identities at `ArchitectureViolationIdentity.CurrentVersion` seed the structured set. As today, the key omits `Kind` and `ForbiddenReference`, and candidate `IdentityVersion` does not alter the comparison. Do not substitute the general full-identity comparer.

4. **Keep lists authoritative for output and add an opt-in internal counter.** `IgnoredViolations` remains the output source, preserving inline-before-loaded order and the first reason/provenance. A low-cardinality `ArchitectureBaselineMergeWorkCounter`, supplied only by Core tests/evidence code, records contract-index entries/lookups, seeded inline ignores, membership checks, and accepted loaded ignores. Production passes no counter, so instrumentation adds no allocation and only nullable counter guards.

5. **Use paired evidence, not a new framework.** Add an issue-specific explicit benchmark in the existing Core test project, following the #1125 legacy-control pattern and #502 methodology. It will vary C, B, and I independently at three sizes, hash canonical ordered outputs, report deterministic operation counts, and sample direct merge time and managed allocation. Report retained-memory/working-set observations only with their measurement limitations. `--timings` exposes the composite `baseline_loading` phase, so report its share as a conservative upper bound for merge impact: it also includes baseline decoding and content identity work. Do not present that share as merge-only or claim an end-to-end speedup beyond the corresponding Amdahl bound.

## Risks / Trade-offs

- **Hash indexes retain additional O(C+I) temporary memory.** Build only the group index for non-empty groups and create ignore membership sets only for contracts that receive eligible baseline ignores. Measure allocations and retained memory against the pre-change path.
- **A key drift could broaden or narrow suppression.** Keep key fields next to the parity tests and compare generated small-input outputs against a test-only copy of the prior linear merge semantics for v1/v2/v3.
- **Hash collisions can affect constants.** Use the default ordinal string hashing/equality for keys and preserve explicit work-count assertions; benchmark data remains environment-labelled.

## Migration Plan

No user migration is required. Existing baseline files and policy documents retain their formats. Archive the spec delta after implementation and tests, then run the required OpenSpec validation.
