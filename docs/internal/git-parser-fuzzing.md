# Git parser fuzzing

This is an internal runbook for the synthetic Git binary-parser campaign. It
is not a product feature and it is not part of ordinary pull-request CI.

## Scope and safety contract

The harness exercises only the selected byte-array parser seams:

- loose-object header and payload validation;
- version-2 pack-index layout and offset lookup;
- pack-entry header decoding;
- `OBJ_OFS_DELTA` reconstruction; and
- `OBJ_REF_DELTA` reconstruction.

Pack-index, pack-entry, OFS-delta, and REF-delta inputs run in both 20-byte
SHA-1 and 32-byte SHA-256 modes. The harness does not locate, open, mutate, or otherwise
consume a live Git repository, and it does not make network requests.

Never use a private checkout, adopter repository, live-repository object, path,
secret, credential, or other sensitive data as a seed or replay input. The
committed corpus is limited to authored format fragments and test-derived
synthetic data. Materialized binary inputs are campaign outputs and must stay
in an ignored scratch directory.

## Pinned toolchain and limits

The workflow and local investigations use the following fixed values:

| Item | Contract |
| --- | --- |
| Target | .NET 10, published self-contained for `linux-x64` |
| Instrumentation | `SharpFuzz.CommandLine` 2.3.0, command `sharpfuzz` |
| AFL++ image | `aflplusplus/aflplusplus@sha256:2627e783e460c18ccc205587800a32cc92971795c80440171dc93d7121c5b6fa` |
| Input cap | 1 MiB, rejected before parser dispatch |
| Per-case timeout | 100 ms (`afl-fuzz -t 100`) |
| Process/container memory | 512 MiB Docker cgroup cap (`--memory=512m`); AFL virtual-memory cap disabled with `-m none` for the .NET/SharpFuzz target |
| Replay memory/timeout | `--replay` launches a worker with a 100 ms post-warm-up watchdog and a hard 512 MiB allocation envelope (Windows Job Object, Linux `prlimit --data`, or macOS `ulimit -d`) |
| CPU | One CPU (`docker --cpus=1`) |
| Campaign duration | 300 seconds (`afl-fuzz -V 300`) |
| Network and filesystem | `--network none`, read-only root, only the findings mount writable; the container runs as the host runner UID/GID |
| AFL++ managed-target check | `AFL_SKIP_BIN_CHECK=1`, because SharpFuzz instruments the managed DLL while AFL++ validates the native apphost |
| AFL++ hang threshold | `AFL_HANG_TMOUT=100`, so a 100 ms stall is retained as a candidate instead of waiting for AFL++'s default minimum hang timeout |
| SharpFuzz child launcher | Harness-local `dotnet` wrapper that maps the harness `.dll` child command to its self-contained apphost |
| Managed heap guard | `DOTNET_GCHeapHardLimit=0x20000000` (hex, 512 MiB) in the replay worker |
| Candidate retention | Ephemeral runner only; raw crash/hang inputs are never uploaded as public GitHub artifacts |

The Unix launcher uses the data/heap limit intentionally: CoreCLR reserves more
than 512 MiB of virtual address space during startup, so an `--as`/`ulimit -v`
cap would fail before the worker becomes ready. `prlimit --data` and
`ulimit -d` bound the anonymous data/heap allocation while the hexadecimal
managed-heap guard remains exactly 512 MiB; Windows uses a Job Object for the
process-memory cap.

The scheduled workflow is intentionally separate from pull-request validation.
It has only `schedule` and `workflow_dispatch` triggers. A normal PR must run
deterministic builds and NUnit regressions; it must not start AFL++.

## Verify a local toolchain

Run these checks from the repository root on a Linux amd64 host (or a Docker
environment that provides the same architecture):

```bash
dotnet --info
docker version
docker info --format '{{.OSType}}/{{.Architecture}}'
```

The SDK must support .NET 10 and Docker must report `linux/amd64`. Restore the
harness before using `--no-restore` commands:

```bash
dotnet restore tools/ArchLinterNet.GitFuzz/ArchLinterNet.GitFuzz.csproj \
  --runtime linux-x64
dotnet build tools/ArchLinterNet.GitFuzz/ArchLinterNet.GitFuzz.csproj \
  --configuration Release --no-restore
```

Install and verify the exact instrumentation tool in a disposable directory:

```bash
dotnet tool install \
  --tool-path .tmp/sharpfuzz \
  SharpFuzz.CommandLine --version 2.3.0
test -x .tmp/sharpfuzz/sharpfuzz
dotnet tool list --tool-path .tmp/sharpfuzz \
  | grep -F "sharpfuzz.commandline" \
  | grep -F "2.3.0"
```

Do not replace the package/tool version or AFL++ digest when a check fails.
Stop and investigate the toolchain failure instead.

## Materialize and replay synthetic inputs

The harness has two stable deterministic commands. Use a fresh ignored output
directory for materialization:

```bash
dotnet run --project tools/ArchLinterNet.GitFuzz -- --materialize-corpus artifacts/git-parser-corpus
```

Replay one materialized input by path. The user-facing command is already
bounded; it starts a worker, waits for its readiness marker, and only then
installs the 512 MiB process limit. The worker then warms the built-in
public-safe corpus, reports that the candidate case is ready, and only then
starts the 100 ms case watchdog before reading the candidate:

```bash
dotnet run --project tools/ArchLinterNet.GitFuzz -- --replay artifacts/git-parser-corpus/<input-file>
```

The materializer decodes the reviewable text/hex source corpus into binary
files. Do not edit or commit those generated files. A replay must use one of
those synthetic files (or a private scratch copy of a candidate under review),
and must retain the recorded input, route selector, digest mode, and result.

For a candidate from AFL++, use that same `--replay` command first. It enforces
the replay envelope mechanically; do not invoke the internal worker argument
directly. A replay still must use a private scratch path and a public-safe
candidate, and it must not be given sensitive inputs.

## Run and inspect a campaign

Use **Actions → Git parser fuzzing → Run workflow** for an intentional run. The
scheduled run has the same inputs and limits. The workflow performs these
operations in order:

1. restores and materializes the synthetic corpus;
1. publishes the harness self-contained for `linux-x64`;
1. instruments the published `ArchLinterNet.Core.dll` with SharpFuzz 2.3.0;
1. runs the pinned AFL++ image with `-t 100 -m none -V 300` and
    `AFL_HANG_TMOUT=100` inside the Docker 512 MiB memory envelope as the host
    runner UID/GID; and
1. reports only a candidate count in the workflow summary, then removes the
    raw findings from the ephemeral runner.

The container mounts the corpus and published harness read-only. It mounts
only the temporary findings directory read-write, runs as the host runner UID/GID
so AFL++'s 0700/0600 outputs remain readable to the host step, uses
`--network none`, one CPU, a 512 MiB memory envelope, a read-only root filesystem, and temporary
filesystems for runtime scratch space. It instruments `ArchLinterNet.Core.dll`,
not the thin harness assembly, because the parser code under test lives in
Core. It sets `AFL_SKIP_BIN_CHECK=1` for the SharpFuzz-managed target: the
instrumented managed assembly is loaded through the self-contained native
apphost, which AFL++'s native binary preflight cannot recognize directly. The
target command is `/harness/ArchLinterNet.GitFuzz` without an `@@` placeholder;
SharpFuzz supplies each mutated input through the stream passed to
`Fuzzer.OutOfProcess.Run`. The published harness directory also contains a
local executable named `dotnet` that maps SharpFuzz's child command
(`/harness/ArchLinterNet.GitFuzz.dll`) to the sibling self-contained apphost
(`/harness/ArchLinterNet.GitFuzz`), and Docker's `PATH` puts `/harness` first.
This satisfies SharpFuzz's child-process launcher inside the pinned AFL++ image
without installing a runtime or allowing network access in the campaign
container. The AFL++ output directory is not a general build or repository
workspace.

When candidates exist, the workflow writes only the count and a no-upload
notice to the job summary. Raw AFL++ crash/hang inputs remain on the ephemeral
runner and are deleted in an `always()` cleanup step; they are never placed in
ordinary GitHub Actions artifacts, which are readable to users with access to a
public repository. A run with no candidate crash or hang reports zero and also
produces no findings artifact.

## Minimize and triage a candidate

Every crash, hang, timeout, resource-limit breach, and unexpected successful
parse is a candidate finding. The scheduled workflow does not expose raw
inputs; rerun the pinned campaign in an access-controlled private scratch
environment to obtain and triage a candidate. Treat every candidate as
untrusted input.

For each candidate:

1. Record the workflow run, commit, image digest, SharpFuzz version, route, and
   digest mode.

1. Replay the exact bytes with the stable `--replay` command and the campaign's
   100 ms/512 MiB limits. Confirm whether the behavior is deterministic.

1. Minimize it with `afl-tmin` from the same pinned AFL++ image, with the same
    no-network, one-CPU, read-only-root, Docker 512 MiB memory envelope, `-t 100`,
    `AFL_HANG_TMOUT=100`, and `-m none` .NET virtual-memory handling. For a file mounted at
   `/findings/default/crashes/id:...`, the target shape is:

   ```bash
   afl-tmin -i /findings/default/crashes/id:... \
     -o /findings/minimized.bin -t 100 -m none -- \
     /harness/ArchLinterNet.GitFuzz
   ```

   The same command shape applies to `default/hangs`.

1. Replay the minimized bytes again, inspect the bytes and diagnostic, and
   verify that they still contain no private or adopter data.

1. If the behavior is a confirmed parser defect, add a focused NUnit
   regression for it (including the applicable digest mode) before closing the
   finding. Promote a minimized input into the committed synthetic corpus only
   after parser-owner review confirms that it is public-safe.

Do not close a confirmed finding on the basis of an AFL++ status screen alone.
The deterministic NUnit regression is the durable evidence that prevents a
future change from reintroducing the defect.

## Corpus ownership and retention

The harness maintainers own the source corpus and its review. A corpus change
must remain small, textual, deterministic, and explain which parser route and
digest mode it exercises. Reviewers should reject inputs that contain repository
content, adopter data, credentials, secrets, machine-specific paths, or any
other non-synthetic material.

Generated binary materializations, AFL++ queues, and candidate artifacts do
not belong in source control. Workflow candidates exist only on the ephemeral
runner until cleanup. Private triage copies must be deleted after replay,
minimization, and review; never mirror raw findings into a public artifact or
another long-lived location.
