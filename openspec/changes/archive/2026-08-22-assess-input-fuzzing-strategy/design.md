## Context

The release-forensics history reader introduced by #236 reads loose Git objects,
pack indexes, pack entry headers, and delta instructions directly. Its custom
parsers are reachable from repository-controlled bytes and must fail closed.
The repository also accepts policy YAML, derived JSON artifacts, and project or
assembly metadata, but most parsing at those boundaries is delegated to mature
libraries and already has bounded deterministic contract fixtures.

## Goals / Non-Goals

**Goals:**

- Make a reproducible, evidence-led testing-technique decision for every
  candidate input surface in #267.
- Identify one narrowly scoped coverage-guided follow-up where custom binary
  parsing and malformed bytes make the technique worthwhile.
- Set non-negotiable safety requirements before a fuzzer is introduced.

**Non-Goals:**

- Add a fuzzer, property-testing dependency, or CI workflow in this change.
- Fuzz third-party parsers or mutate live developer repositories.
- Treat a Scorecard result as proof that a fuzzer is valuable.

## Decisions

1. The assessment selects coverage-guided fuzzing only for synthetic Git object
   and pack-parser seams: loose-object header/payload validation, pack-index
   layout/offset lookup, pack-entry header decoding, and delta reconstruction.
   These routines contain custom byte-level state machines, allocation sizing,
   and recursive object resolution. They are therefore a materially better
   target than the higher-level history orchestration.

2. The implementation follow-up will use a small executable harness that reads
   an explicit byte array or synthetic object-store fixture, imposes a 1 MiB
   input cap, and treats only expected fail-closed parser failures as success.
   Unhandled exceptions, timeouts, non-termination, or allocations exceeding
   the process limit are failures. It will run under a pinned SharpFuzz tool and
   pinned AFL++ container image on Linux; ordinary PR CI will run only normal
   deterministic regression tests. SharpFuzz is retained as the .NET IL
   instrumentation candidate because its documented .NET/AFL integration fits
   this seam, while the follow-up must verify the pinned versions against .NET
   10 before adoption.

3. The versioned, public-safe seed corpus will contain only synthetic object,
   index, pack, and delta bytes derived from tests or generated locally. A
   scheduled/manual campaign will use fixed time and memory limits, publish no
   raw developer/adopter repository content, minimize a failure before filing
   it, and promote every confirmed failure to a deterministic NUnit regression.

4. No property-based library is selected. The possible invariants—TaskKey
   normalization, ordering, policy composition, and JSON serialization—have
   constrained state spaces and established example/golden tests. Adding a
   generator and a new dependency now would not deliver comparable value; a
   future semantic defect may reopen the decision with a concrete invariant.

## Risks / Trade-offs

- [SharpFuzz compatibility or maintenance does not meet .NET 10 needs] → The
  follow-up validates the exact pinned toolchain before adding it; it must
  choose an equivalent maintained mechanism or close without implementation.
- [Fuzzing consumes disproportionate CI time or becomes flaky] → Campaigns are
  scheduled/manual and only minimized regressions run in normal tests.
- [A corpus leaks repository information] → Seeds are synthetic/test-derived
  only and corpus review is required before publication.
- [Parser allocation exposes the host to resource exhaustion] → Harness input,
  process memory, and per-case time limits are part of its acceptance contract.
