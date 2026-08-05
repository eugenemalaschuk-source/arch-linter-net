# Adopt or Upgrade to 0.5.1

ArchLinterNet 0.5.1 is the single public stabilization release for the 0.5.0
adoption workflow. Checkpoint A is internal integration evidence; it is not a
package release or a support promise. Use this guide to start a new policy or
to upgrade a supported 0.5.0 policy deliberately.

All names in this guide are synthetic. Replace `Example.Product` and file paths
with your own reviewed architecture; do not copy a policy merely because its
shape looks familiar.

## Choose a path

| You have | Start here |
| --- | --- |
| A new .NET repository | [Greenfield adoption](#greenfield-adoption) |
| A 0.5.0 root policy, fragments, or baseline | [Upgrade from 0.5.0](#upgrade-from-050) |
| Several projects or hosts | [Solution shapes](#solution-shapes) |
| A CI, shell, Make, Task, or Tilt integration | [Reference entrypoints](reference-entrypoints.md) |

The minimal path needs neither source sets, a baseline, API snapshots, cache,
profiling, nor parallelism settings. Add each only when its problem exists.

## Greenfield adoption

### 1. Pin the tool

Use a local tool manifest for a repository or CI. Pin the same package version
that you have approved for the repository; `0.5.1` is shown here as the release
contract this guide describes.

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli --version 0.5.1
dotnet tool restore
dotnet arch-linter-net --version
```

A global installation is convenient for an interactive workstation, but is not
the usual CI choice:

```bash
dotnet tool install --global ArchLinterNet.Cli --version 0.5.1
arch-linter-net --version
```

### 2. Create the smallest root policy

Create `architecture/arch.yml`. A root policy must declare `version`, `name`,
`layers`, `analysis`, and `contracts` even when the first contract set is
small.

```yaml
# yaml-language-server: $schema=https://archlinternet.dev/schema/0.5.1/dependencies.arch.schema.json
version: 1
name: Example Product architecture

layers:
  domain:
    namespace: Example.Product.Domain
  application:
    namespace: Example.Product.Application
  infrastructure:
    namespace: Example.Product.Infrastructure

analysis:
  solution: Example.Product.slnx
  target_assemblies:
    - Example.Product.Domain
    - Example.Product.Application
    - Example.Product.Infrastructure

contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application code depends on abstractions rather than concrete infrastructure.
```

Set `analysis.solution` to the actual solution file. It lets the CLI discover
the selected projects and their output directories; `target_assemblies` alone
does not recursively search every project's `bin/<Configuration>/<TFM>` path.
For a single-project repository, use its `.csproj` in `analysis.projects`
instead. The release-qualified schema ID is useful editor feedback. Installed package
bytes, not an unversioned web URL, are the release source of truth; see
[Offline schemas](#offline-schemas).

### 3. Check the policy without assemblies

Run an assembly-free check before configuring build discovery. It validates the
root, imports, identifiers, and static configuration but does not claim the
architecture is clean.

```bash
dotnet arch-linter-net policy check --policy architecture/arch.yml --format json
```

Exit `0` means the static policy is valid. Fact-dependent checks are reported
as deferred; invalid configuration exits `2`.

### 4. Restore, build, and run the first strict gate

For an ordinary checkout, prepare the inputs yourself and run strict
validation:

```bash
dotnet restore
dotnet build Example.Product.slnx --no-restore
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

For a clean checkout where the CLI owns preparation, make it explicit. It
builds and verifies the selected project graph once; it is never implicit.

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

In a prepared restricted environment, preserve the no-network boundary with
`--no-restore`. A missing restore state fails closed rather than restoring:

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --ensure-built --no-restore
```

### 5. Add only the features you need

- [Baselines](migration-baselines.md) record reviewed current debt. They never
  approve a future violation automatically.
- [Public API snapshots](../contracts/public-api-surface.md) give exported APIs
  a reviewed, exact file contract.
- [`--report`](../usage/output-formats.md) routes one validation result to
  human, JSON, and SARIF sinks without repeating analysis.
- [`--cache`](#cache-profile-and-concurrency) is an opt-in performance feature;
  the default is disabled.
- [`--profile`](#cache-profile-and-concurrency) writes a machine-readable
  `analysis-profile/v1` artifact only when requested.
- [`--max-parallelism`](#cache-profile-and-concurrency) can keep execution
  sequential on a resource-constrained runner.

## Upgrade from 0.5.0

The compatibility promise preserves valid existing policy meaning unless you
opt into an additive 0.5.1 capability or a documented correctness fix makes an
old identity too broad. Treat every generated artifact as a review candidate.

### 1. Compose roots and fragments deliberately

Keep the selected root as the root document and use fragments only for
mergeable sections. The selected root retains `version` and `name`; an imported
fragment does not. Imports have deterministic provenance and path safety, not
override precedence.

```yaml
# architecture/arch.yml (root)
version: 1
name: Example Product architecture
imports:
  - policy/layers.arch.yml
  - policy/contracts.arch.yml
layers: {}
analysis:
  target_assemblies: [Example.Product.Host]
contracts: {}
```

```yaml
# architecture/policy/layers.arch.yml (fragment)
layers:
  application:
    namespace: Example.Product.Application
```

Run `policy check` after each composition change. See [Policy imports](../policy-format/imports.md)
for root/fragment roles, canonical paths, and typed provenance diagnostics.

### 2. Adopt selectors, source sets, and planned-empty inputs only where useful

Compatible selectors use deterministic include-minus-exclude behavior. Source
sets expand only within already-declared analysis projects or assemblies; they
never discover additional analysis scope. A zero match fails closed unless the
exact input is declared optional with a non-empty reason.

```yaml
source_sets:
  product-hosts:
    kind: assembly
    members: [Example.Product.Host, Example.Product.Worker]

  future-adapters:
    kind: layer
    members: [adapters]
    optional: true
    reason: The adapter layer is planned but has no production code yet.
```

Use the [policy reference](../reference/yaml-schema.md#source_sets) for the
complete supported family inventory. Do not use an optional input to hide an
unexpected empty selector; it is visible as `optional-empty` and becomes an
ordinary populated input when code appears.

### 3. Migrate a legacy baseline and review identity requalification

`version: 1` baselines retain legacy matching behavior. They are never silently
treated as structured version 2 baselines. Preview the explicit migration and
review the proposed result before writing it:

```bash
dotnet arch-linter-net baseline migrate \
  --config architecture/arch.yml \
  --baseline architecture/baseline-v1.yml \
  --output architecture/baseline.yml \
  --dry-run
```

Write only after review, using explicit overwrite intent when the destination
already exists. Then verify the result in CI:

```bash
dotnet arch-linter-net baseline migrate \
  --config architecture/arch.yml \
  --baseline architecture/baseline-v1.yml \
  --output architecture/baseline.yml --force

dotnet arch-linter-net baseline verify \
  --config architecture/arch.yml --baseline architecture/baseline.yml
```

If `diff` or `verify` reports `changed`, `stale`, or `ambiguous`, do not treat
it as a match. Review the typed evidence, update or recapture the affected
identity explicitly, then prune only resolved entries:

```bash
dotnet arch-linter-net baseline update \
  --config architecture/arch.yml --baseline architecture/baseline.yml \
  --output architecture/baseline.yml --dry-run

dotnet arch-linter-net baseline prune \
  --config architecture/arch.yml --baseline architecture/baseline.yml \
  --output architecture/baseline.yml --dry-run
```

`generate`, `migrate`, `update`, and `prune` are local review operations. CI
uses read-only `baseline verify`; it must never regenerate, update, or commit
accepted debt. The [baseline lifecycle guide](migration-baselines.md) covers
reason preservation, preview, SARIF, and Testing API comparison results.

### 4. Move public API contracts to reviewed snapshots

Capture, compare, and update snapshots deliberately. `diff` is read-only;
`update` requires an explicit write and uses atomic replacement. Do not put a
capture or update command in an unattended CI job.

```bash
# Produce a candidate for review.
dotnet arch-linter-net public-api capture \
  --policy architecture/arch.yml --contract product-api \
  --output architecture/api/product-api.txt

# Gate an already reviewed snapshot.
dotnet arch-linter-net public-api diff \
  --policy architecture/arch.yml --contract product-api \
  --snapshot architecture/api/product-api.txt

# Inspect a proposed update before asking for an explicit write.
dotnet arch-linter-net public-api update \
  --policy architecture/arch.yml --contract product-api \
  --snapshot architecture/api/product-api.txt --dry-run
```

See [Public API surface contracts](../contracts/public-api-surface.md) for
inline-list migration and exact snapshot ownership.

### 5. Update machine consumers

JSON, SARIF, and `ArchLinterNet.Testing` expose equivalent normalized finding
semantics. New integrations consume `finding/v1`, its canonical identity, and
the typed `details` discriminator; legacy projection fields remain compatibility
fields for 0.5.1. Readers reject unsupported schema versions rather than
guessing their meaning. See [YAML Schema Reference](../reference/yaml-schema.md#normalized-finding-v1-compatibility)
and [Test Adapter](../usage/test-adapter.md).

### 6. Requalify build and project assumptions

For a solution with project discovery, package, `FrameworkReference`, or
composition contracts, use build-state preflight deliberately. A clean checkout
can use `--ensure-built`; `--no-restore` makes an unavailable restore input a
typed failure. Same-named global or top-level entry types stay distinct by
assembly/project identity, so multi-host policies must retain their real
project and assembly inputs.

## Reports, artifacts, and completion status

`--report <format>=<destination>` is repeatable and routes validation reports:

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --report json=artifacts/architecture.json \
  --report sarif=artifacts/architecture.sarif
```

The legacy one-sink `--format`/`--json` forms remain supported, but cannot be
combined with `--report` because that would be ambiguous. Report files are
staged and individually atomically replaced when the filesystem permits. A
later destination failure reports `partial-output`, identifies committed and
uncommitted paths, exits `2`, and does not rerun validation; it is not a
cross-file transaction.

Command `--output` options belong to their commands: baseline and public-API
operations use them for candidate artifacts, not report routing. These writes
are explicit and reviewable.

The numeric categories are stable:

| Exit code | Meaning |
| --- | --- |
| `0` | Command completed and its requested gate passed. |
| `1` | Command completed and a validation, baseline, diff, or verification gate failed. |
| `2` | The command could not complete: invalid input, preparation/build failure, output failure or `partial-output`, or cancellation. |

Human output is complete without color or a TTY. JSON, SARIF, and Testing use
typed locations and provenance; no consumer needs to parse display prose.
Cancellation is a typed `cancelled` completion and exit `2`; it wins before a
result is fully published and never creates reusable partial cache state.

## Cache, profile, and concurrency

These are opt-in execution controls. They do not change canonical findings,
identity, order, or exit categories.

```bash
# Default: no persistent cache. This is a supported normal run.
dotnet arch-linter-net --policy architecture/arch.yml --mode strict

# Opt into the platform user-cache namespace and record a profile file.
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --cache auto --profile artifacts/architecture-profile.json

# Use a reviewed caller-owned cache directory and force sequential scanning.
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --cache .architecture-cache --max-parallelism 1

# Inspect or clear a cache only with explicit intent.
dotnet arch-linter-net cache inspect --cache .architecture-cache
dotnet arch-linter-net cache clear --cache .architecture-cache
```

`--cache` is disabled by default. It persists verified `analysis-cache/v1`
entries only when explicitly selected. `auto` uses the platform user-cache namespace;
an explicit path must be safe and caller-owned. A cache hit is used only after
integrity and input eligibility checks; a miss, rejection, corruption, changed
input, or cancellation recomputes or fails safely and never becomes success.

`--profile` writes `analysis-profile/v1` to `stdout`, `stderr`, or a file path
and is independent of reports and `--timings`. Profile values are evidence for
the observed run, not hardware-independent performance promises.

The default bounded parallelism is `max(1, min(processor count, 4))`.
`--max-parallelism 1` is a fully supported sequential mode for constrained
runners; a positive higher value bounds only assembly and fact scanning.

## Offline schemas

An installed 0.5.1 package contains the immutable release registry. In an
offline directory — without a repository checkout, restore, build, or target
assemblies — discover it from the installed tool:

```bash
dotnet arch-linter-net schema list
dotnet arch-linter-net schema print policy-root > policy-root.schema.json
dotnet arch-linter-net schema print policy-fragment > policy-fragment.schema.json
dotnet arch-linter-net schema print baseline > baseline.schema.json
dotnet arch-linter-net schema print api-snapshot > api-snapshot.schema.json
dotnet arch-linter-net schema print normalized-finding > finding.schema.json
dotnet arch-linter-net schema print analysis-build-state > build-state.schema.json
dotnet arch-linter-net schema print analysis-cache > cache.schema.json
dotnet arch-linter-net schema print analysis-profile > profile.schema.json
```

`schema list` reports the logical ID, document version, immutable `$id`, and
packaged path. `schema print` writes the exact installed bytes. This registry,
not a mutable default-branch URL, is the compatibility authority for an
installed release.

## Solution shapes

For an ordinary multi-project solution, declare the actual analysis projects or
target assemblies and let build-state preflight verify their configuration and
target framework. Add reusable source sets only where multiple reviewed
contracts deliberately share a source universe.

For a multi-host solution, retain each host's assembly/project identity. The
same global or top-level `Program` type in two assemblies is intentionally not
the same composition finding or baseline identity.

For a Testing API consumer, load the same policy instead of duplicating its
rules in test helpers:

```csharp
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Testing;

ArchitectureAssertions
    .FromPolicy("architecture/arch.yml")
    .WithProfile()
    .WithCache(AnalysisCacheOptions.Auto())
    .WithMaxParallelism(1)
    .ValidateStrict()
    .ShouldPass();
```

The Testing API returns the same typed finding and baseline semantics as the
CLI. Its snapshot ownership is explicit: use `CreateSnapshot()` only when one
caller deliberately wants strict and audit evaluation to share one requested
session. See [Test Adapter](../usage/test-adapter.md) for the core pattern.

## Next steps

- Use [Reference entrypoints](reference-entrypoints.md) to adapt the canonical
  commands to POSIX, PowerShell, Make, Task, Tilt, or CI.
- Use [Output formats](../usage/output-formats.md) for human/JSON/SARIF details.
- Use [Exit codes](../usage/exit-codes.md) for CI routing.
- Use [Troubleshooting](troubleshooting.md) when preflight, import, output, or
  cancellation behavior needs diagnosis.
