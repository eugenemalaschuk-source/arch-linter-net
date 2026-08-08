## Context

MSBuild `Clean` obtains prior writes from
`$(IntermediateOutputPath)$(CleanFile)`, then removes matching files under the
real output directory. Buildalyzer's design-time `Clean;Build` therefore made
an otherwise read-oriented analysis destructive to consumers sharing `bin`.

## Decision

Create one temporary directory per Buildalyzer invocation and pass its unique
`IntermediateOutputPath` as an MSBuild global property. The isolated path has
no prior clean manifest, so `Clean` has no list of the project's existing
primary outputs to delete. Any design-time clean bookkeeping is contained in
the temporary directory, which is deleted only after evaluation has completed.

The real output path remains the source of resolved project/reference
artifacts. This retains the existing Roslyn and FrameworkReference discovery
semantics while preventing the standard MSBuild clean path from touching shared
primary outputs.

## Alternatives considered

- **Snapshot and restore:** rejected because consumers can observe the missing
  interval and a restore can overwrite a concurrent normal build.
- **Redirect the complete output path:** rejected because it changes the
  project-reference artifact paths that project-aware analysis must resolve.

## Verification

A real project fixture runs an MSBuild target that changes an unrelated output
during the design-time build. A concurrent reader continuously reads the
selected assembly and PDB. The test asserts no transient missing/changed
primary artifact and that the unrelated output retains its concurrent change.
