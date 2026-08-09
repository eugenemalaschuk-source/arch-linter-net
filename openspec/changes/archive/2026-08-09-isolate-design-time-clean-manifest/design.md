## Context

`CoreClean` reads `$(IntermediateOutputPath)$(CleanFile)` to discover outputs
to delete. The prior corrective change redirected all of
`IntermediateOutputPath`, which protected primary outputs but made Buildalyzer
return reference assemblies below a temporary `obj` directory. Those paths are
consumed after `Resolve()` returns, so disposing the directory broke
cross-project symbol resolution.

## Decision

Pass a unique `CleanFile` name as an MSBuild global property for each
Buildalyzer evaluation. `CoreClean` therefore cannot read the project's normal
file-list manifest and has no prior shared outputs to delete. The real
intermediate path remains unchanged, so returned project-reference paths remain
stable and consumable.

After evaluation, remove only files bearing the generated, exact clean-file
name under the project's `obj` directory. This cleanup never scans or restores
the output directory and cannot overwrite unrelated artifacts.

## Verification

The resolver test asserts that its returned project-reference assembly exists
after `Resolve()` returns. The end-to-end method-body test resolves
`Widgets.Build` from that reference, while the concurrent-reader regression
continues to verify that primary outputs stay continuously available.
