# Adopt or Upgrade ArchLinterNet

For a new repository, use [Getting Started](../getting-started/index.md).
For an existing policy, follow the upgrade steps below. For a Health/PR report,
use the single [base/current recipe](single-tool-workflow.md).

## Choose a path

| Starting point | Guide |
| --- | --- |
| New .NET repository | [Greenfield adoption](#greenfield-adoption) |
| Existing policy and baseline | [Upgrade an existing policy](#upgrade-an-existing-policy) |
| Additional topology, budgets, waivers or SARIF | [Extended governance](extended-governance-adoption.md) |
| Slow CI | [Timings, prepared receipts and cache](../usage/timings.md) |
| Several projects or test hosts | [Solution shapes](#solution-shapes) |

## Greenfield adoption

Install a repository-local tool, commit its manifest, and author a small
`version: 2` policy for real boundaries. [Getting Started](../getting-started/index.md)
is the maintained installation/policy example. Policy `version: 1` is supported
for compatibility; it is not the recommended new-policy default.

The minimal gate needs no baseline, public badge, cache, API snapshot or external
service. Add those only when their purpose exists. A package version and a policy
schema version are different decisions.

## Upgrade an existing policy

### 1. Establish the upgrade boundary

Restore the old pin and record the existing gate result first. Select the new
package deliberately from its release notes. Then update the manifest:

```bash
: "${ARCHLINTERNET_VERSION:?Set the exact reviewed package version}"
dotnet tool update ArchLinterNet.Cli --version "$ARCHLINTERNET_VERSION"
dotnet tool restore
dotnet arch-linter-net --version
dotnet arch-linter-net schema list
dotnet arch-linter-net policy check --policy architecture/arch.yml
```

Run your existing architecture checks with their original policy, baseline and
build selectors. Compare new diagnostics before editing policy. A newly exposed
problem is not a reason to weaken its rule automatically.

When the CLI analyzes projects referencing `ArchLinterNet.Testing`/Core, keep the
consumed package set compatible with the CLI; review both tool and package pins.
Use one exact CLI for the review's base and current states, including when the
base has an older local tool manifest.

### 2. Compose roots and fragments deliberately

The root owns `version` and `name`. Imported fragments contain mergeable sections,
not another root. Imports do not establish override precedence. Keep existing
v1 roots while deliberately migrating legacy waivers; choose v2 for new roots.
See [imports](../policy-format/imports.md).

### 3. Adopt selectors and source sets only where useful

`source_sets` reuse selections within the declared analysis scope; they do not
discover unrelated projects. Mark an input optional only for a reviewed expected
absence, with a reason. See [policy format](../policy-format/index.md).

### 4. Review baseline identity changes explicitly

Use [baseline diff and migration](migration-baselines.md) before rewriting an
existing baseline. Review `changed`, `stale` and `ambiguous` identities; none is
permission to broaden a match. CI reads and verifies accepted debt rather than
regenerating it. Scalar metric baseline values are separate from finding debt.

### 5. Move API contracts to reviewed snapshots when appropriate

Capture the intended selected API, review it, and use diff for validation.
Update is an explicit review operation, not a fix for any failing API test.
See [public API contracts](../contracts/public-api-surface.md).

### 6. Update machine consumers by contract identity

Read the schema/format identity emitted by the installed package. Do not infer
it from package SemVer or silently treat an unknown field as an empty result.
Regenerate paired policy contexts with the same compatible tool after a schema
change. Keep the original artifacts for comparison.

### 7. Requalify build and project assumptions

Use the SDKs and build selectors needed by both revisions. A fresh checkout may
need `--ensure-built`; `--no-restore` forbids restoring missing prerequisites,
not building. Prepared receipts are a separate verified path, not a claim that
plain `dotnet build` produces every required receipt.

## Reports, artifacts, and completion status

Request multiple validation formats through `--report` rather than several
analytical runs. For a PR review, use [Health plus a compatible change report](single-tool-workflow.md).
The supported `health --change-snapshot` path avoids a second current analysis;
older packages require the documented fallback.

`0` is success, `1` a completed failing decision, and `2` an input/runtime or
unassessable result. Health can write a valid unassessable JSON document while
exiting 2. Rendering or uploading it must not clear that failure. See
[exit codes](../usage/exit-codes.md).

## Cache, profile, and concurrency

See [performance diagnosis](../usage/timings.md) before enabling caching or
increasing parallelism. Cache eligibility and actual hits must be observed;
prepared receipts and base-evidence reuse are different mechanisms.

## Offline schemas

```bash
dotnet arch-linter-net schema list
dotnet arch-linter-net schema print policy-root > policy-root.schema.json
dotnet arch-linter-net schema print policy-fragment > policy-fragment.schema.json
```

`schema list` supplies the remaining installed logical IDs. Select from that
list rather than deriving names or URLs from the package version.

## Solution shapes

For multi-project/host solutions, retain actual project and assembly identities.
Two same-named types in different assemblies are not the same finding. For
Unity, prepare compatible compiled inputs through the
[Unity workflow](unity-boundaries.md), not an ordinary .NET build assumption.

A test host uses the same policy rather than duplicating rules in C#:

```csharp
using ArchLinterNet.Testing;

ArchitectureAssertions
    .FromPolicy("architecture/arch.yml")
    .ValidateStrict()
    .ShouldPass();
```

See [Test adapter](../usage/test-adapter.md) for baseline/context composition.

## Next steps

Use [reference entrypoints](reference-entrypoints.md) for provider wrappers,
[output formats](../usage/output-formats.md) for machine consumers and
[troubleshooting](troubleshooting.md) for failed input checks.
