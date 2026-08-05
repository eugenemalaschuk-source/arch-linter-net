# 0.5.1 Reference Entrypoints

These are thin consumer-owned templates for ArchLinterNet 0.5.1. They pin or
restore the tool, pass structured arguments, invoke one validation command per
requested session, and preserve stdout/stderr. POSIX, PowerShell, Task with
`--exit-code`, and direct CI invocations propagate the exact product exit code.
GNU Make cannot do so for a failing recipe, so its template writes that exact
code to an artifact for the outer shell/CI caller to propagate. The templates
do not generate a policy, create a baseline, approve debt, or write an API
snapshot automatically.

Examples use the synthetic `Example.Product` policy path. Replace values with
your own reviewed paths; do not interpolate untrusted values into shell command
strings or use `eval`.

## Direct pinned .NET tool

Commit `.config/dotnet-tools.json` after pinning the package. CI restores it,
then invokes the tool exactly once:

```bash
dotnet tool install ArchLinterNet.Cli --version 0.5.1
dotnet tool restore
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

For JSON and SARIF artifacts, use product report routing rather than shell
redirection where multiple files are needed:

```bash
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --report json=artifacts/architecture.json \
  --report sarif=artifacts/architecture.sarif
```

Exit code `0` is a passed gate, `1` is a completed failing gate, and `2` means
the command could not complete. A pipeline must preserve all three values.

## POSIX shell

Use an argument array. It keeps a policy path containing spaces as one argument
and neither evaluates arguments nor merges the tool's standard streams.

```bash
#!/usr/bin/env bash
set -u

tool=(dotnet arch-linter-net)
args=(--policy "architecture/Example Product/arch.yml" --mode strict)

"${tool[@]}" "${args[@]}"
exit "$?"
```

The script deliberately does not use `set -e`: the caller must receive the
tool's exact `1` or `2` status rather than a shell-specific substitute. Do not
replace the array invocation with `eval`, `sh -c`, or a concatenated command
string.

## PowerShell

Pass native arguments as an array, invoke the native executable directly, and
return `$LASTEXITCODE`. This preserves native stdout/stderr and works when the
output is redirected or no terminal is attached.

```powershell
$ErrorActionPreference = 'Stop'

$arguments = @(
    'arch-linter-net',
    '--policy', 'architecture/Example Product/arch.yml',
    '--mode', 'strict'
)

& dotnet @arguments
$exitCode = $LASTEXITCODE
exit $exitCode
```

Do not use `Invoke-Expression`, concatenate `$arguments` into one string, or
replace `$LASTEXITCODE` with PowerShell's success preference. A validation
failure must remain exit `1`; cancellation, malformed input, and output failure
must remain exit `2`.

## Make

GNU Make does not preserve the product exit code when a recipe fails: its own
process exits `2` for both product status `1` and `2`. Keep the exact product
status in a machine-readable artifact, then make the *outer* shell or CI caller
return that saved value. Do not use a plain failing recipe as a three-state CI
interface.

```make
.PHONY: architecture

architecture:
	@mkdir -p artifacts
	@set +e; \
	  dotnet arch-linter-net --policy architecture/arch.yml --mode strict; \
	  status=$$?; \
	  printf '%s\n' $$status > artifacts/architecture.exit-code; \
	  exit 0
```

Restore the pinned tool before the target, then call Make and re-emit its saved
product status from the surrounding POSIX shell or CI step:

```bash
dotnet tool restore
make architecture
status=$(cat artifacts/architecture.exit-code)
exit "$status"
```

The product is still invoked exactly once. The artifact is a status channel,
not a replacement for JSON/SARIF reports.

## Taskfile

Task's normal failure exit code is Task's own code, not necessarily the product
status. Invoke the task with `--exit-code` to preserve the ArchLinterNet `0`/`1`/`2`
contract. Use a fixed command rather than a shell-composed string:

```yaml
version: '3'

tasks:
  architecture:
    cmds:
      - dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

The command is a fixed literal: do not append untrusted values through Task's
template interpolation. Keep tool restore as a distinct bootstrap task when it
is not already performed by the environment. The `architecture` task invokes
ArchLinterNet once, and its caller must use:

```bash
task --exit-code architecture
```

## Tilt

Use a fixed argv list and let Tilt display the tool output. Do not construct a
shell string from repository or environment values:

```python
local([
    "dotnet",
    "arch-linter-net",
    "--policy", "architecture/arch.yml",
    "--mode", "strict",
])
```

Tilt remains consumer orchestration: it does not change policy, baseline, API
snapshot, cache, or report semantics.

## Generic CI contract

Every CI provider needs the same sequence: restore the pinned tool, restore and
build the solution as appropriate, invoke strict validation once, preserve its
exit code, and retain configured report files as artifacts. The provider does
not parse display prose to decide success.

```text
dotnet tool restore
dotnet restore
dotnet build --no-restore
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --report json=artifacts/architecture.json \
  --report sarif=artifacts/architecture.sarif
status=$?
upload artifacts/architecture.json and artifacts/architecture.sarif when present
return status unchanged
```

For a resource-constrained runner, choose the supported sequential mode:

```text
dotnet arch-linter-net --policy architecture/arch.yml --mode strict --max-parallelism 1
```

For a prepared offline runner, use the installed tool's schema registry and
preserve the no-restore boundary:

```text
dotnet arch-linter-net schema list
dotnet arch-linter-net schema print policy-root > artifacts/policy-root.schema.json
dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built --no-restore
```

Cache is disabled unless explicitly selected. If the CI trust boundary permits
a caller-owned cache, opt in deliberately and retain profile evidence
separately from reports:

```text
dotnet arch-linter-net --policy architecture/arch.yml --mode strict \
  --cache .architecture-cache \
  --profile artifacts/architecture-profile.json
```

A cancelled run or failed report publication exits `2`. If a multi-report run
reports `partial-output`, preserve both the command status and its committed /
uncommitted destination evidence; do not rerun it automatically.

## GitHub Actions example

GitHub Actions is one provider of the generic contract, not a semantic product
dependency:

```yaml
name: Architecture validation

on: [pull_request]

jobs:
  architecture:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet tool restore
      - run: dotnet restore
      - run: dotnet build --no-restore
      - name: Strict validation
        run: >-
          dotnet arch-linter-net --policy architecture/arch.yml --mode strict
          --report json=artifacts/architecture.json
          --report sarif=artifacts/architecture.sarif
      - name: Upload diagnostics
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-diagnostics
          path: artifacts/
```

An audit run is a visibility decision, not a replacement for strict validation.
If it is non-blocking, configure that policy in the CI provider while retaining
the tool's real exit code in logs and artifacts.

## Testing API

The Testing API loads the same policy and has the same load-bearing semantics
as the CLI. Snapshot ownership is explicit; normal tests can validate one mode
directly:

```csharp
using ArchLinterNet.Testing;
using NUnit.Framework;

[TestFixture]
public sealed class ArchitectureTests
{
    [Test]
    public void StrictArchitectureContractsMustPass()
    {
        ArchitectureAssertions
            .FromPolicy("architecture/arch.yml")
            .ValidateStrict()
            .ShouldPass();
    }
}
```

Use `.WithBaseline(path).VerifyBaseline()` for a read-only baseline gate and
keep capture/update/migrate ownership in a reviewed local workflow. See
[Test Adapter](../usage/test-adapter.md) and the [migration guide](migration-to-0-5-1.md).
