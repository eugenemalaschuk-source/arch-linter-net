# Input Robustness Testing Assessment

Issue: #267\
Related delivered surface: #236\
Decision date: 2026-08-22

## Decision method

This assessment ranks externally supplied or restored bytes by both realistic
failure impact and the amount of parser or interpreter code owned by
ArchLinterNet. It does not use the OpenSSF Scorecard signal as an adoption
criterion. A selected technique must expose a bounded seam and a meaningful
oracle; otherwise existing deterministic tests are retained.

| Priority | Surface | Custom-code exposure | Decision | Rationale |
| --- | --- | --- | --- | --- |
| 1 | Loose Git objects; pack index, entry header, and delta bytes | High | A | Custom binary layout, size arithmetic, decompression, offset handling, and delta instruction reconstruction consume repository-controlled bytes. The fail-closed/no-crash/no-hang/no-unbounded-allocation oracle is clear. |
| 2 | Git refs, commit headers, paths, tree objects, and TaskKey spans | Medium | C | They are small, grammar-led layers with extensive deterministic corrupt-object, malformed-metadata, ref-cycle, UTF-8, path, and span fixtures. Mutating repository-wide structures would add setup cost without increasing the selected parser seam's coverage. |
| 3 | Policy root/import YAML, raw schema validation, source selectors, CEL fields | Medium | C | YAML syntax is owned by YamlDotNet and custom rules have focused schema/import/cycle/depth and CEL-profile fixtures. No current generator has a stated invariant whose shrinking value exceeds that suite. Reassess if a new custom language or stateful import evaluator is added. |
| 4 | Baselines, canonical reports, cache envelopes, packaged schemas, release evidence | Low to medium | C | The current boundaries use explicit version/schema checks and deterministic serialization/deserialization tests; most parsing is System.Text.Json or packaged-resource handling. A raw-byte fuzzer would predominantly exercise framework behavior rather than unique ArchLinterNet parsing. |
| 5 | Solution/project selection, PE/assembly metadata, PDB/manifest mapping | Low | C | MSBuild, Roslyn, and metadata libraries own the underlying formats. ArchLinterNet's value is in post-parse interpretation, already covered by project-selection and assembly fixtures; fuzzing opaque framework parsers is out of scope. |
| 6 | TaskKey normalization, canonical ordering/identity, policy composition, JSON round trips, graph/scoring thresholds | Medium semantic / low byte-parser | C | The candidate invariants are valuable, but current bounded example, golden, and boundary tests exhaust the small supported domains. No property-testing dependency is selected without an observed gap requiring generation and shrinking. |

## Selected A target: Git binary parser harness

The follow-up is limited to byte-level seams below `Core.History.Git`:

- loose-object decompression plus `<kind> SP <size> NUL payload` validation;
- version-2 pack-index fanout/name/offset layout and lookup;
- pack-entry header, `OBJ_OFS_DELTA`, and `OBJ_REF_DELTA` decoding;
- delta size varints and copy/insert reconstruction.

The oracle accepts either a canonical bounded parse result or the existing
fail-closed `HistoryDiagnostic` route. It rejects unhandled exceptions,
unexpected successful partial evidence, hangs, and memory/process-limit
breaches. It does not fuzz a live repository.

### Resource and operational contract

- Input: one synthetic byte fixture capped at 1 MiB; no network or external
  repository access.
- Per case: 100 ms timeout; campaign process memory cap: 512 MiB.
- Corpus: versioned, public-safe synthetic seeds under the future fuzz project;
  generated from test fixtures or format fragments only, never from an adopter
  or developer checkout.
- Tooling: version-pinned SharpFuzz instrumentation and a version/digest-pinned
  AFL++ Linux container, both verified against .NET 10 by the follow-up before
  adoption. A maintained equivalent may replace SharpFuzz only with the same
  replay and containment guarantees.
- Replay: documented one-input command with the identical input/time/memory
  limits and tool versions.
- Triage: preserve a failing input only in restricted CI artifacts while
  reviewing it; minimize with the fuzzer tooling; commit only a public-safe
  minimized reproduction; add a deterministic NUnit regression before closing.
- Cadence: scheduled/manual campaign. Normal PR CI runs the deterministic
  regressions, not a long-running campaign. A bounded smoke run needs a
  separate decision backed by stable timing evidence.

## Deferred property testing

FsCheck supports NUnit-integrated generated and shrinking properties, but no
current candidate meets the maintenance/value threshold. The repository will
reconsider it only with a concrete invariant that has a broader valid-input
space than the present deterministic suite and an independently useful shrink
result.

## Evidence

- `GitObjectDatabase`, `GitPackIndex`, `GitPackFile`, and `GitDeltaDecoder`
  directly process object, index, pack, and delta bytes.
- Existing `HistoryCorruptObjectTests` and `GitDeltaDecoderTests` provide
  deterministic malformed-input fixtures but are intentionally finite.
- SharpFuzz documents .NET assembly instrumentation for AFL-style fuzzing;
  its documented environment is Linux/macOS for AFL. AFL++ documents explicit
  corpus minimization and time/memory limits. FsCheck documents NUnit
  integration and shrinking. These sources informed the harness contract, not
  the selection by themselves.
