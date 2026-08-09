## Why

Reflecting over an assembly that references the ASP.NET Core shared framework
(`Microsoft.AspNetCore.App`) fails when analyzed through the documented
`dotnet arch-linter-net ... --ensure-built` entrypoint, because the CLI host process
never loads that shared framework and its assemblies are absent from the process's
trusted platform assembly list. Today a real adopter must hand-author a
runtimeconfig.json and invoke the CLI assembly through `dotnet exec` to work around
this. Unconditionally adding a `Microsoft.AspNetCore.App` `FrameworkReference` to the
CLI itself is not appropriate: it would require every consumer machine — including
non-ASP.NET adopters — to have the ASP.NET Core runtime installed just to run the
tool.

## What Changes

- Add an explicit, opt-in `analysis.shared_frameworks` policy option naming shared
  frameworks (for example `Microsoft.AspNetCore.App`) whose assemblies should be
  resolvable during analysis.
- Resolve each named shared framework to its installed directory on the host machine
  (honoring `DOTNET_ROOT`/`DOTNET_ROOT(X86)`, falling back to the currently running
  runtime's own shared-framework store) and add it to the assembly probing paths used
  by the post-build (`--ensure-built`) isolated load scope.
- Fail with an actionable `InvalidOperationException` when a named shared framework
  cannot be located, instead of a downstream `ReflectionTypeLoadException`.
- Add a representative ASP.NET Core host fixture and packaged-CLI acceptance coverage
  proving `--ensure-built` analysis succeeds against it with no hand-authored
  runtimeconfig or `dotnet exec` wrapper.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `assembly-resolution`: add opt-in shared-framework probing-path resolution for the
  post-build isolated load scope.

## Impact

Changes the public policy YAML schema (additive `analysis.shared_frameworks` field),
`ArchitectureAnalysisConfiguration`, `ArchitectureAssemblyResolutionService` probing
logic, packaged JSON schema, and documentation. No architecture layer or CLI option
surface changes; non-ASP.NET consumers are unaffected because the field is optional
and empty by default.
