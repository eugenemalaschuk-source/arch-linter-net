# Design

## Context

See `proposal.md` for motivation. `ArchitectureAnalysisSession` creates one `ArchitectureContractExecutionContext` for each selected contract, and that context reuses one ignore list while its checker evaluates findings. The current matcher scans that list for every finding; with a usage tracker it must collect all matches, and without one the first match determines loaded-baseline provenance. The matcher also supports versioned baseline identity, structured manual waivers, and legacy glob fallback.

The parent story recommends implementing the earlier baseline merge task #1126 first but explicitly does not make it a semantic dependency. This index consumes the finalized ignore list after baseline loading and merging, so its scope remains independent of the merge implementation.

## Goals / Non-Goals

**Goals:**

- Prepare lookup data once for each contract execution and reuse it for every finding and every alias of a finding.
- Preserve exact equality, text normalization, fail-closed waiver handling, missing-identity fallback, original entry order, duplicate usage indices, and loaded-baseline provenance.
- Bound indexed exact-path work by index preparation, finding/alias lookup count, and required match count; measure wildcard fallback separately.
- Keep performance evidence deterministic for work counts and environment-labelled for time and memory.

**Non-Goals:**

- Changing the glob language, timeout, cancellation contract, waiver validity/expiry rules, or public diagnostic identity.
- Reusing matcher state across contracts, policies, or analysis sessions.
- Adding a public profiler or a new benchmark framework.

## Decisions

### Build a per-contract immutable index

Create an immutable snapshot when `ArchitectureContractExecutionContext` is constructed. Classify entries into exact versioned-identity lookup, structured-waiver fingerprint lookup, normalized literal-pair lookup, or wildcard fallback. Retain each original list position and the loaded-baseline provenance bit. The versioned-identity key contains exactly the fields compared by the current matcher; it does not add `ContractId` or change the current equality contract. Versioned baseline entries are also available to the text/glob fallback when a live identity is absent, while structured manual waivers never enter that fallback.

Index construction is O(I) for I ignore entries. Its contract-scoped lifetime ends with the execution context; it is not retained in global or cross-policy state.

### Merge candidate streams by original position

The exact identity, fingerprint, literal-pair, and wildcard paths each produce matches in ascending original list position. Merge those bounded streams by their next position instead of sorting a per-finding result collection. Without a tracker, stop at the first merged match and return that entry's loaded-baseline provenance. With a tracker, visit every merged match and record every original index, including duplicate declarations.

Wildcard entries remain on the existing matcher path, retaining normalization, prefix handling, regex timeout, and cancellation checks. The exact-path complexity claim excludes wildcard fallback; wildcard-heavy cost is reported independently.

### Build the waiver fingerprint once per finding

Only create a live waiver fingerprint when the contract index contains a structured manual waiver and the finding has a live identity. Compute it once before matching aliases and pass the same value to each alias lookup. Versioned baseline identity matching continues to compare the same individual identity fields as today. A versioned baseline entry without a live identity uses its existing text/glob fallback; an invalid or incomplete manual structured waiver remains fail-closed.

### Reuse the existing evidence style

Add a focused NUnit benchmark fixture alongside the existing #502/#1126 evidence fixtures. Retain a test-only reference implementation of the current ordered matcher for differential checks and baseline work counts. Scale ignore entries, findings, required matches, and wildcard count independently; report index build and lookup timing, allocations, retained-memory measurements when stable, canonical result checksums, and the execution environment. Deterministic work assertions gate tests; timings do not.

## Observed measurements

On .NET 10.0.15 / Windows 10.0.19045 / x64 (6 processors), the five-sample exact-heavy matrix improved at I=1,024/F=64/K=64 for versioned identities (4.7136 ms to 2.0572 ms), structured waivers (102.5309 ms to 0.6903 ms), and literal pairs (1.4991 ms to 0.7224 ms). The structured-waiver case reduced measured allocations from 116,316,288 B to 467,808 B and built 64 live fingerprints instead of 65,536.

Index construction and retained memory are visible trade-offs. The small I=8/F=8 case increased from 0.0093 ms / 1,024 B to 0.0197 ms / 10,672 B, and the I=1,024/F=16 case increased from 0.6533 ms to 1.7772 ms. At I=1,024/F=64 the independent finding-scale run improved slightly (1.9346 ms before, 1.7657 ms after); F=256 improved from 7.9570 ms to 1.8156 ms. At I=1,024/F=32, K=32/128/512 remained slower overall because the shared index build dominates those short runs, despite query work tracking K rather than I×F.

The W=256 unmatched double-star case increased from 90.6649 ms to 106.2235 ms while evaluating 16,384 wildcard candidates. Regex work remains dominant and arbitrary-glob runtime has no linear guarantee. These are matcher-only measurements: product-validation phase share and a whole-validation Amdahl bound remain unmeasured. Full rows, allocations, retained-index estimates, and canonical hashes are in `docs/internal/large-solution-benchmarking/evidence/issue-1127/`.

## Risks / Trade-offs

- **[Risk]** Per-contract lookup structures increase setup allocation and retained memory, especially for small contracts or few findings. → Include index construction in the reported cost, expose small and low-finding regressions, and keep the index contract-scoped so its retained state is released with the execution context.
- **[Risk]** Merging multiple match sources could reorder overlaps or omit duplicate entries. → Differentially compare tracker results, boolean suppression, and loaded-baseline provenance against the original ordered matcher using deterministic randomized cases.
- **[Risk]** A new index-build loop could delay cancellation for large policies. → Pass the existing cancellation token into index preparation and check it while consuming entries; check it during wildcard fallback enumeration as well.

## Migration Plan

No persisted data or public contract changes. Reverting the implementation restores the previous ordered scan; no migration is required.
