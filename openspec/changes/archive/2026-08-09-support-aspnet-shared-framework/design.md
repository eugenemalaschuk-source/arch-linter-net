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

### Highest installed version wins

When more than one version directory exists under a shared framework, the resolver
picks the highest by parsed version, matching how `dotnet` itself rolls forward
within a shared framework's compatible range. The policy does not pin the exact
runtime version — it names the framework, not a specific patch build.

### Fail closed on a missing framework

Unlike `analysis.assembly_search_paths` (which silently skips directories that don't
exist), a named `analysis.shared_frameworks` entry that cannot be located throws
`InvalidOperationException` naming the framework and the roots searched. Acceptance
criteria for #441 explicitly require "actionable diagnostics" for missing/unsupported
framework conditions, and a silent skip here would surface as a much less actionable
downstream `ReflectionTypeLoadException`.
