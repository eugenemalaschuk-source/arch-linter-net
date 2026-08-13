# Adopt or Upgrade ArchLinterNet

Use this guide to adopt ArchLinterNet in a new repository or to upgrade an
existing policy without coupling the documentation to one package release.

ArchLinterNet package releases and persisted document/schema versions have
separate lifecycles. Pin the package version your repository has reviewed, then
use the installed CLI to discover the exact schemas and machine contracts that
ship with that package.

All names in this guide are synthetic. Replace `Example.Product` and file paths
with your own reviewed architecture; do not copy a policy merely because its
shape looks familiar.

## Choose a path

| You have | Start here |
| --- | --- |
| A new .NET repository | [Greenfield adoption](#greenfield-adoption) |
| An existing ArchLinterNet policy | [Upgrade an existing policy](#upgrade-an-existing-policy) |
| Several projects or hosts | [Solution shapes](#solution-shapes) |
| A CI, shell, Make, Task, or Tilt integration | [Reference entrypoints](reference-entrypoints.md) |

The minimal path needs neither source sets, a baseline, API snapshots, cache,
profiling, nor parallelism settings. Add each only when its problem exists.

## Greenfield adoption

### 1. Pin the tool

For a repository or CI, prefer a local tool manifest. The first install resolves
a package release and records the exact selected version in
`.config/dotnet-tools.json`; review and commit that manifest.

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --version
```

When you intentionally upgrade, update the local tool, review the manifest diff,
and run the repository's architecture acceptance checks before merging it:

```bash
dotnet tool update ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --version
```

A global install is convenient for an interactive workstation but is not the
usual CI choice:

```bash
dotnet tool install --global ArchLinterNet.Cli
arch-linter-net --version
```

### 2. Create the smallest root policy

Create `architecture/arch.yml`. Keep the policy focused on real repository
boundaries:

```yaml
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

Set `analysis.solution` to the actual solution file. For a single-project
repository, use its `.csproj` in `analysis.projects` instead.

Do not copy a release-qualified schema URL from a web page into an evergreen
example. Discover the exact schema shipped by the installed package:

```bash
dotnet arch-linter-net schema list
dotnet arch-linter-net schema print policy-root > policy-root.schema.json
```

The installed package bytes are the compatibility authority for that selected
release.

### 3. Check the policy without assemblies

Run an assembly-free check before configuring build discovery. It validates the
root, imports, identifiers, and static configuration but does not claim the
architecture is clean.

```bash
dotnet arch-linter-net policy check --policy architecture/arch.yml --format json
```

Exit `0` means the static policy is valid. Fact-dependent checks are reported as
deferred; invalid configuration exits `2`.

### 4. Restore, build, and run the first strict gate

For an ordinary checkout, prepare inputs and run strict validation:

```bash
dotnet restore
dotnet build Example.Product.slnx --no-restore
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

For a clean checkout where the CLI owns preparation, make it explicit:

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

In a prepared restricted environment, preserve the no-network boundary with
`--no-restore`. Missing restore state fails closed rather than restoring:

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --ensure-built --no-restore
```

### 5. Add only the features you need

- [Baselines](migration-baselines.md) record reviewed current debt. They never
  approve a future violation automatically.
- [Public API snapshots](../contracts/public-api-surface.md) give exported APIs
  a reviewed file contract.
- [`--report`](../usage/output-formats.md) routes one validation result to human,
  JSON, and SARIF sinks without repeating analysis.
- `--cache` is an opt-in performance feature; the default is disabled.
- `--profile` writes a machine-readable `analysis-profile/v1` artifact only when
  requested.
- `--max-parallelism` can keep execution sequential on a resource-constrained
  runner.

## Upgrade an existing policy

Treat package upgrades as reviewed compatibility changes, not as a reason to
create a new version-named documentation path. Start from the policy and
artifacts you already own, inspect the installed release's capabilities, and
adopt changes deliberately.

### 1. Establish the upgrade boundary

Before editing the policy:

1. restore the currently pinned tool and run the existing strict gate;
1. update the pinned tool deliberately;
1. run `schema list` and `--version` to record the selected package/schema
   boundary;
1. run `policy check` before loading assemblies;
1. review changed diagnostics or compatibility errors before changing policy.

Do not weaken a rule merely because a newer tool exposes a problem that the old
configuration failed to make visible.

### 2. Compose roots and fragments deliberately

Keep the selected root as the root document and use fragments only for mergeable
sections. The selected root retains `version` and `name`; an imported fragment
does not. Imports have deterministic provenance and path safety, not override
precedence.

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

Run `policy check` after each composition change. See
[Policy imports](../policy-format/imports.md) for root/fragment roles, canonical
paths, and provenance diagnostics.

### 3. Adopt selectors and source sets only where useful

Source sets expand only within already-declared analysis projects, assemblies,
or layers; they never discover a broader analysis scope. A zero match fails
closed unless the exact input is deliberately optional with a non-empty reason.

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

Do not use an optional input to hide an unexpected empty selector.

### 4. Review baseline identity changes explicitly

Persisted baseline document versions are machine-contract versions, not package
release names. When migrating an older baseline format, preview the operation and
review the proposed identities before writing:

```bash
dotnet arch-linter-net baseline migrate \
  --config architecture/arch.yml \
  --baseline architecture/baseline-v1.yml \
  --output architecture/baseline.yml \
  --dry-run
```

Write only after review, then verify the result in CI:

```bash
dotnet arch-linter-net baseline migrate \
  --config architecture/arch.yml \
  --baseline architecture/baseline-v1.yml \
  --output architecture/baseline.yml --force

dotnet arch-linter-net baseline verify \
  --config architecture/arch.yml --baseline architecture/baseline.yml
```

If `diff` or `verify` reports `changed`, `stale`, or `ambiguous`, review and
recapture the affected identity explicitly. CI uses read-only
`baseline verify`; it must never regenerate, update, or commit accepted debt.

### 5. Move API contracts to reviewed snapshots when appropriate

Capture, compare, and update snapshots deliberately. `diff` is read-only;
`update` requires explicit write intent. Do not put capture/update commands in an
unattended CI job.

```bash
dotnet arch-linter-net public-api capture \
  --policy architecture/arch.yml --contract product-api \
  --output architecture/api/product-api.txt --ensure-built

dotnet arch-linter-net public-api diff \
  --policy architecture/arch.yml --contract product-api \
  --snapshot architecture/api/product-api.txt

dotnet arch-linter-net public-api update \
  --policy architecture/arch.yml --contract product-api \
  --snapshot architecture/api/product-api.txt --dry-run
```

See [Public API surface contracts](../contracts/public-api-surface.md) for the
reviewed snapshot lifecycle.

### 6. Update machine consumers by contract identity

JSON, SARIF, and `ArchLinterNet.Testing` expose normalized findings through
machine contract identities such as `finding/v1`. Treat those identifiers as
persisted protocol/schema versions; do not infer them from the package SemVer.
Readers should reject unsupported machine-contract versions rather than guessing
meaning.

### 7. Requalify build and project assumptions

For solution/project discovery, package, `FrameworkReference`, or composition
contracts, use build-state preflight deliberately. A clean checkout can use
`--ensure-built`; `--no-restore` makes unavailable restore input a typed failure.
Multi-host policies must retain their real project and assembly identities.

## Reports, artifacts, and completion status

`--report <format>=<destination>` is repeatable and routes validation reports:

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --report json=artifacts/architecture.json \
  --report sarif=artifacts/architecture.sarif
```

Command `--output` options belong to their commands: baseline and public-API
operations use them for candidate artifacts, not report routing. A later report
destination failure may produce typed `partial-output`; it does not turn the
operation into a cross-file transaction.

The numeric exit categories remain:

| Exit code | Meaning |
| --- | --- |
| `0` | Command completed and its requested gate passed. |
| `1` | Command completed and a validation/comparison gate failed. |
| `2` | The command could not complete normally. |

Human output is complete without color or a TTY. Cancellation is typed
`cancelled` completion and exits `2`; it never creates reusable partial cache
state.

## Cache, profile, and concurrency

These are opt-in execution controls. They do not change canonical findings,
identity, order, or exit categories.

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict

dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --cache auto --profile artifacts/architecture-profile.json

dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --cache .architecture-cache --max-parallelism 1
```

`analysis-cache/v1` and `analysis-profile/v1` are machine-format identities;
their numbering is independent from the package release.

## Offline schemas

In a prepared offline environment, discover every schema from the installed
tool rather than a mutable web page:

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
packaged path. `schema print` writes the exact installed bytes.

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
using ArchLinterNet.Testing;

ArchitectureAssertions
    .FromPolicy("architecture/arch.yml")
    .ValidateStrict()
    .ShouldPass();
```

## Next steps

- Use [Reference entrypoints](reference-entrypoints.md) for POSIX, PowerShell,
  Make, Task, Tilt, and CI wrappers.
- Use [Output formats](../usage/output-formats.md) for Human/JSON/SARIF details.
- Use [Exit codes](../usage/exit-codes.md) for CI routing.
- Use [Troubleshooting](troubleshooting.md) when preflight, import, output, or
  cancellation behavior needs diagnosis.
