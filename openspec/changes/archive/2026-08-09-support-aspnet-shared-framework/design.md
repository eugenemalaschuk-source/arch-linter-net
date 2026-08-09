## Context

`ArchitectureAssemblyResolutionService.ResolveFromDocument` computes a list of
probing paths and, when running through the post-build path (`--ensure-built`),
hands them to an isolated `AssemblyLoadContext` (`IsolatedAssemblyLoadScope` in
`ArchitectureAssemblyLoader.cs`). That scope's `Load(AssemblyName)` override only
resolves an assembly name against exact post-build paths or the probing-path list;
anything else falls through to CLR default resolution, which only succeeds if the
assembly is already in the process's trusted platform assembly list. The CLI process
has no `FrameworkReference` to `Microsoft.AspNetCore.App`, so any deep reflection
(base types, custom attributes, interface lists) that touches an ASP.NET Core type
fails.

The non-isolated (no `--ensure-built`) path uses `Assembly.Load`/`Assembly.LoadFrom`
directly against `AssemblyLoadContext.Default` with no resolving hook at all, and
is not the documented entrypoint (every reference-entrypoints example uses
`--ensure-built`). Extending it would require a process-wide, static
`AssemblyLoadContext.Default.Resolving` handler — risky in-process for
`ArchLinterNet.Testing`, which reuses one static engine across sequential calls in a
single test process. This change is scoped to the isolated post-build path only.

## Goals / Non-Goals

**Goals:**

- Let `--ensure-built` analysis resolve types from an installed shared framework
  when the policy opts in.
- Give an actionable, fail-fast diagnostic when the named framework is not
  installed, instead of a downstream reflection exception.
- Leave every consumer who does not set `analysis.shared_frameworks` completely
  unaffected.

**Non-Goals:**

- Making the non-isolated (no `--ensure-built`) resolution path shared-framework
  aware.
- Adding a `FrameworkReference` to the CLI's own project (would force the ASP.NET
  Core runtime onto every consumer machine).
- Supporting shared frameworks other than by directory-of-assemblies probing (no
  runtimeconfig generation, no apphost changes).

## Decisions

### Opt-in YAML field, not automatic detection

`analysis.shared_frameworks: [Microsoft.AspNetCore.App]` is explicit. Automatically
probing for every installed shared framework on every run would silently change
behavior for consumers who happen to have the ASP.NET Core runtime installed for
unrelated reasons, and would make missing-framework failures nondeterministic across
machines.

### Directory discovery, not a `FrameworkReference`

Resolution locates `<shared-root>/<framework-name>/<version>/` on the host machine
(preferring `DOTNET_ROOT`/`DOTNET_ROOT(X86)`, falling back to the currently running
runtime's own shared-framework store derived from
`RuntimeEnvironment.GetRuntimeDirectory()`) and adds it as a probing-path directory.
This requires no change to the CLI's own `runtimeconfig.json` and does not require
the ASP.NET Core runtime for consumers who never opt in.

### Scoped to the isolated post-build load scope

Only `ArchitectureAssemblyResolutionService.ResolvePostBuild`'s probing paths (feeding
`IsolatedAssemblyLoadScope`) gain shared-framework directories. This is the
documented `--ensure-built` entrypoint every reference-entrypoints guide already
recommends, and the isolated scope already has correct, per-invocation (not
process-static) probing-path resolution semantics.

### Highest compatible version wins, anchored to a major version

When more than one version directory exists under a shared framework, the resolver
picks the highest version whose major version matches an anchor: `analysis.target_
framework` when the policy sets it, otherwise the currently running runtime's own
major version. This mirrors the .NET host's default roll-forward policy, which never
crosses a major version and never prefers a prerelease build over a release build at
the same or lower version. Without this anchor, a machine that also has a newer
major's prerelease installed (e.g. `Microsoft.AspNetCore.App 11.0.0-preview.*`
alongside a `net10` target) would have its numerically-higher prerelease silently
selected instead of the intended stable major — and because `AssemblyLoadContext`
can satisfy a requested assembly version with a higher loaded one, the failure mode
is not a load error but silent reflection against the wrong framework's metadata.
When no anchor major version is derivable at all, the resolver falls back to the
highest version across all installed majors (still preferring release over
prerelease) rather than refusing to resolve — the policy does not pin an exact
runtime patch build, only the framework name.

### Fail closed on a missing framework

Unlike `analysis.assembly_search_paths` (which silently skips directories that don't
exist), a named `analysis.shared_frameworks` entry that cannot be located throws
`InvalidOperationException` naming the framework and the roots searched. Acceptance
criteria for #441 explicitly require "actionable diagnostics" for missing/unsupported
framework conditions, and a silent skip here would surface as a much less actionable
downstream `ReflectionTypeLoadException`.
