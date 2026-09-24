# Timings and performance diagnosis

Measure the architecture commands, not just the duration of the containing CI
job. Record the CLI version, exact revision, runner, build selectors and cache
state so a later comparison measures the same work.

## Capture timings and a profile

Use a policy that already passes its normal input checks:

```bash
mkdir -p artifacts
arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built \
  --timings --profile artifacts/architecture-profile.json \
  --report json=artifacts/architecture.json \
  --report sarif=artifacts/architecture.sarif \
  2> artifacts/architecture-stderr.txt
```

`--timings` writes a human phase breakdown to stderr. That stream may also
contain diagnostics; it is not a timing-only machine format. `--profile` writes
`analysis-profile/v1` JSON with counters and completion state. Neither option
is an optimization. Keep the command's exit code and inspect incomplete or
cancelled profiles separately from successful ones.

## JSON plus timings

For a simpler measurement, without the profile:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict --json --timings \
  > architecture.json 2> architecture-stderr.txt
```

## Sample output

Phase names and available counters depend on the installed version. Read the
actual profile rather than treating a historical sample's milliseconds as a
performance target. Use [CLI help](../cli/index.md) to confirm profiling support
for each subcommand you measure.

## Interpretation

Start with the largest source of avoidable work. Repeated process startup,
restore/build, base-worktree preparation, source scanning and output rendering
are different costs. When jobs overlap, keep elapsed pipeline time separate
from the sum of individual command times.

First remove duplicate work: request several validation formats with repeatable
`--report`; combine strict/audit only when both are deliberately required;
produce Health and the current snapshot together where supported. See the
[review workflow](../guides/single-tool-workflow.md). Do not repeat analysis just
to refresh a badge or to produce a second output format.

Compare repeated runs with the same inputs and runner. Report cold runs,
cache population and verified hits separately; a single faster run is not
proof that a cache or code change caused the improvement.

## Prepared receipts

These options are present in `0.9.0-preview.1`; do not assume they exist in
`0.8.2`. Check the exact installed command before using them:

```bash
arch-linter-net --help
arch-linter-net health --help
```

A producer can build the selected graph and publish its verified receipts.
Subsequent consumers verify that state without building or restoring:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict \
  --configuration Release --publish-prepared-receipts

arch-linter-net health --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --mode strict \
  --configuration Release --use-prepared-receipts \
  --change-snapshot artifacts/current.json --format json > artifacts/health.json
```

This is a multi-command build-reuse example, not a replacement for the
base/current report recipe. The explicit baseline and artifact directory must
already exist. If Health is the only analysis required, prefer its own
`--ensure-built` preparation rather than adding a preliminary validation.

The producer command may build when no authoritative completed-build handoff is
provided. A preceding plain `dotnet build` or copied assembly directory is not
such a handoff. Internal proof-directory/nonce options are for an integrated
producer; do not invent their values or treat them as trust-bypass switches.

Use the same revision, CLI, policy and build selectors. Missing or stale receipts
must fail; do not hide that failure by silently switching to a different revision
or unverified outputs. This reuses prepared build evidence, not the previous
process's in-memory analysis or a persisted `prepared-analysis/v1` service.

## Cache eligibility and misses

`--cache` enables the existing exact-request cache; it does not guarantee that
the selected project can populate it or obtain a hit:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built \
  --cache .architecture-cache --profile artifacts/cache-profile.json
arch-linter-net cache inspect --cache .architecture-cache
```

Inspect eligibility reasons and lookup/hit/miss/reject/write counters in the
profile before adding cache storage to CI. `CacheIneligible`, including
`framework-reference-identity-unverified` for unsupported real-MSBuild inputs,
is a deliberate refusal to reuse insufficiently proven state. A second run
with zero writes and zero hits is not a warm-hit benchmark.

A correct miss or refusal still uses ordinary analysis; never relax evidence
checks merely to obtain a hit. `cache clear --cache .architecture-cache`
removes the selected cache; it does not repair project inputs or receipts.

| Mechanism | What can be reused |
| --- | --- |
| Several `--report` sinks | One completed validation result, rendered several ways. |
| `health --change-snapshot` | Current Health and snapshot from one analysis session. |
| Prepared receipts | Verified build artifacts across compatible consumers. |
| `analysis-cache/v1` | An eligible equivalent request's persisted analysis data. |
| Trusted base evidence | Artifacts for one exact reviewed base revision, with compatible inputs and verified provenance. |

They are not interchangeable. No option is a general promise that every .NET
solution will get faster, or that a planned optimization has shipped.
