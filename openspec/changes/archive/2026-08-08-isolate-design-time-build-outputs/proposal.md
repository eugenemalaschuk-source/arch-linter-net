## Why

Eliminate the cross-process primary-output race left by post-hoc restoration of
Buildalyzer design-time evaluation.

## What Changes

- Run each Buildalyzer design-time evaluation with a unique, isolated
  intermediate output path so `Clean` cannot read or update the project's real
  clean manifest.
- Apply that isolation to project-aware Roslyn resolution and framework
  reference evaluation.
- Add a concurrent-reader regression and prove that an unrelated output changed
  by evaluation is not rolled back.

## Context

Buildalyzer invokes `Clean;Build` for design-time evaluation. Reading the
project's real intermediate clean manifest permits `Clean` to delete shared
primary outputs while another process is loading them. Snapshot/restore only
repairs the state after the unsafe interval and can overwrite a concurrent real
build's newer output.

## Work type

Corrective implementation for review findings on #436.

## Non-goals

- Changing policy syntax, receipt behavior, or ordinary preflight strictness.
- Isolating arbitrary user-authored MSBuild targets that explicitly use absolute
  paths outside MSBuild output properties.
