## Context

The history reader's Git object database owns byte-level parsing of loose
objects, version-2 pack indexes, pack entries, and delta instructions. Its
normal fail-closed boundary already converts malformed repository bytes to a
`HistoryDiagnostic`, but the existing NUnit fixtures are finite. The completed
#267 assessment selected precisely these seams for coverage-guided fuzzing and
deferred all other input surfaces.

The harness must be executable, reproducible on Linux, and isolated from live
repositories. It cannot become a normal PR dependency. The repository's
architecture policy treats Core's friend assemblies and solution project scope
as explicit reviewed contracts.

## Goals / Non-Goals

**Goals:**

- Exercise the five selected parser routes using one synthetic input format and a
  canonical-success-or-fail-closed oracle.
- Run each pack-index, pack-entry, OFS-delta, and REF-delta route in both 20-byte SHA-1
  and 32-byte SHA-256 modes.
- Make the 1 MiB input cap, 100 ms case timeout, and 512 MiB process limit
  mechanically visible in replay and campaign commands.
- Version only synthetic, reviewable seeds; keep raw campaign findings
  ephemeral and out of ordinary public GitHub Actions artifacts.

**Non-Goals:**

- Fuzz a live checkout, `.git/config`, refs, commit metadata, YAML, JSON,
  MSBuild/Roslyn, or third-party parsers.
- Change shipping history semantics or publish a new Core API.
- Run an open-ended campaign from normal pull-request CI.

## Decisions

### An internal Core seam plus a non-shipping friend executable

`GitParserFuzzingSeams` will be an internal Core adapter that dispatches a
single byte array to the selected routes. It will reuse the production parser
logic; thin extraction is permitted only where a private method currently
prevents byte-array input. The console executable in
`tools/ArchLinterNet.GitFuzz` receives access through a specifically reviewed
`InternalsVisibleTo` entry rather than making parser types public or using
reflection. The project will be in the solution so restore/build/format are
deterministic, while its `tools/**` path stays outside the shipped-project
architecture universe.

### One selector byte and a stable oracle

The first input byte selects loose object, pack index, pack entry, REF-delta, or
OFS-delta. The harness applies every pack/delta route twice, once per supported
digest length. It accepts only a bounded canonical parser result or
`HistoryFailureException`; every other exception remains observable as an
AFL++ crash. Input acquisition stops after 1 MiB plus one byte, before the
selected parser is invoked. The user-facing `--replay` mode launches a worker
and enforces a 100 ms post-warm-up watchdog plus a 512 MiB process-memory
limit through a Windows Job Object, Linux `prlimit`, or macOS `ulimit -v`; the
worker also receives the hexadecimal .NET `DOTNET_GCHeapHardLimit=0x20000000`.
The worker-only argument is not accepted without the launcher marker. This
keeps deterministic replay outside SharpFuzz's out-of-process execution path
without leaving triage uncontained.

This is preferred to fuzzing temporary Git directories because byte-array
routes are faster, reproducible, and cannot accidentally use a developer's
repository.

### Pin SharpFuzz and an immutable Linux AFL++ image

The harness references SharpFuzz 2.3.0 and the workflow installs
SharpFuzz.CommandLine 2.3.0. SharpFuzz documents .NET 8 or later as sufficient
for instrumentation, which covers this repository's .NET 10 SDK. The campaign
uses the official AFL++ linux/amd64 image by immutable digest
`sha256:2627e783e460c18ccc205587800a32cc92971795c80440171dc93d7121c5b6fa`.
The workflow publishes a self-contained `linux-x64` target so the AFL++ image
does not need to contain an SDK or receive network access.

The immutable digest is chosen over a mutable image tag. AFL++'s `-t 100`
provides the per-case timeout and `AFL_HANG_TMOUT=100` makes the hang retention
threshold match it, while Docker's `--memory=512m` is the campaign's memory
envelope. The workflow uses `afl-fuzz -m none` because SharpFuzz's .NET
fork-server model reserves virtual address space above AFL++'s process-memory
limit even when Docker still enforces the 512 MiB container cap. The campaign
also has an explicit finite duration.

SharpFuzz starts child executions through a `dotnet <harness>.dll` command. The
pinned AFL++ image intentionally does not contain a .NET runtime, so the
self-contained publish writes a harness-local `dotnet` wrapper and prepends
`/harness` to `PATH`; that wrapper maps the child `.dll` command back to the
self-contained native apphost.

### Textual source corpus materialized deterministically

The committed corpus stores hex-encoded synthetic inputs, which keeps every
byte reviewable in ordinary diffs without relying on an unreviewable binary
patch format. The harness materializes those files to an ignored temporary
binary corpus before AFL++ runs. Seeds include valid and malformed examples for
all five routes; tests replay every materialized seed and a deliberately
oversized input in both applicable digest modes.

### Scheduled/manual workflow with conservative artifact handling

The new workflow has only `schedule` and `workflow_dispatch` triggers. It
passes the host runner UID/GID to the container so AFL++'s 0700/0600 findings
remain readable, reports only the candidate count in the workflow summary, and
removes raw findings from the ephemeral runner. It never uploads raw crash or
hang inputs as ordinary GitHub Actions artifacts; triage reruns a pinned
campaign in an access-controlled private environment. The campaign runs with
Docker networking disabled; ordinary PR CI retains only the deterministic
NUnit regressions.

## Risks / Trade-offs

- [The instrumentation tool stops supporting .NET 10] → the fixed-version
  workflow verifies build and instrumentation before starting AFL++; a failure
  blocks the campaign rather than silently falling back.
- [Fuzzer-generated data consumes host resources] → capped input acquisition,
  AFL++ time/memory flags, finite duration, Docker memory/cpu limits, and a
  no-network container are all mandatory.
- [An externally readable corpus leaks repository information] → committed
  seeds are authored format fragments only; raw campaign outputs never enter
  public workflow artifacts and are removed from the ephemeral runner.
- [A friend assembly widens Core's effective API] → the exact friend and
  non-shipping project exclusion are recorded in the self-policy, with no
  exported Core member added.

## Migration Plan

1. Extract narrow internal byte-array parser adapters and prove the oracle with
   focused Core tests.
2. Add the console target, synthetic source corpus, deterministic materializer,
   replay command, and regression tests.
3. Add the pinned scheduled/manual workflow and documentation, then smoke its
   fixed-limit path.
4. Archive the OpenSpec change after all deterministic checks pass.

Rollback removes the tool, its reviewed Core friend entry, its workflow, and
the corpus without changing any shipped history command or public API.
