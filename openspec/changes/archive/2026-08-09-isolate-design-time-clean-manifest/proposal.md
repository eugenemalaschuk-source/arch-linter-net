## Why

Redirecting the complete MSBuild `IntermediateOutputPath` avoids destructive
`Clean`, but it also redirects project-reference reference assemblies returned
by Buildalyzer. Deleting that temporary directory before project-aware Roslyn
compilation silently removes those returned references.

## What Changes

- Isolate only the per-invocation MSBuild `CleanFile` manifest, not the entire
  intermediate-output path.
- Remove only the uniquely named design-time clean manifests after evaluation.
- Assert that project-reference paths returned from `Resolve()` still exist and
  remain consumable by end-to-end method-body analysis.

## Non-goals

- Changing the no-transient-mutation guarantee for selected primary outputs.
- Retaining per-invocation temporary intermediate-output directories after
  project-aware resolution.
