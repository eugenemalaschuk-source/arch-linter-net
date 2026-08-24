## Context

The validation service has two setup paths. Cache-enabled validation already selects project outputs
and captures metadata without loading CLR assemblies, then invokes build-state preparation and
materializes an isolated post-build load scope. The uncached path instead creates a runner before
preflight; resolving a project target can load its output from disk. When the output belongs to the
repository currently being built (notably `ArchLinterNet.Testing.dll` while the CLI is running), the
Windows file handle prevents the temporary solution build from replacing it.

## Goals / Non-Goals

**Goals:**

- Ensure every `--ensure-built` validation follows a metadata-only preparation phase before its
  graph build, independently of cache configuration.
- Retain the post-build isolated loading and ordinary receipt verification currently used for
  cache-backed preparation.
- Prove the installed-tool/self-analysis case and preserve explicit offline `--no-restore` behavior.

**Non-Goals:**

- Change policy syntax, artifact receipt contents, snapshot identity, strict/audit results, or
  diagnostic projections.
- Redesign cache persistence, project discovery, or build-state verification.
- Change how ordinary validation without `--ensure-built` resolves existing assemblies.

## Decisions

### Use the metadata preparation path whenever explicit preparation is requested

The validation service will use `PrepareRunner` / build-state preparation / post-build
`MaterializePreparedRunner` for cache-backed requests and for uncached `EnsureBuilt` requests.
`PrepareRunner` retains only paths and digests; it creates no `AssemblyLoadContext`, so it cannot
hold a selected output while the temporary build runs. After a successful build, the service repeats
metadata preparation, runs ordinary receipt verification, and then materializes the isolated loader
for analysis.

This reuses the already-proven cache path rather than creating a parallel no-lock preflight flow.
Keeping the ordinary uncached path unchanged avoids changing its established no-build semantics.

### Test the consumer boundary at two levels

A focused Core integration test will run the CLI against a disposable self-analysis-style fixture
whose target graph includes `ArchLinterNet.Testing`, proving the pre-build phase does not hold a
target DLL. The existing packaged candidate feed will add an installed-tool scenario with an
isolated public-safe fixture and validate the same command path. Both tests assert successful
receipt-backed preparation; the focused coverage also exercises `--no-restore` after restore.

### Preserve the prepared output identity during receipt refresh

Without explicit output constraints, post-build resolution historically selected the newest
`bin/**/<AssemblyName>.dll` candidate. That can differ from the output selected during metadata
preparation: a newer Release output could receive a current receipt while the policy-selected Debug
output remains stale. For an unconstrained request, receipt refresh therefore reuses the prepared
physical path when it is still the project's matching output below `bin/`. Explicit configuration,
framework, or runtime-identifier requests retain their existing exact-output lookup.

### Alternatives considered

- **Unload the pre-build runner:** rejected because normal assembly loads can be in the default
  load context and therefore cannot be deterministically unloaded before MSBuild replaces outputs.
- **Use a special build output location:** rejected because receipts and subsequent analysis must
  verify and consume the requested project outputs, and this would alter the build-state contract.
- **Disable `--ensure-built` for self-analysis:** rejected because it breaks the intended consumer
  workflow and weakens preparation rather than fixing its sequencing.

## Risks / Trade-offs

- [A metadata-only plan is incomplete for a configuration] → retain existing fail-closed preflight
  handling and materialize only when root selection is complete.
- [The shared path changes uncached timing/counters] → retain current state transitions and add
  focused assertions for successful receipt-backed output and unchanged findings.
- [Packaged smoke tests are slower] → add the scenario to the existing release-gate candidate feed,
  while keeping the fast disposable integration test as the normal regression proof.

## Migration Plan

No user migration is needed. Existing commands retain their flags and result schemas. The fixed
sequencing is available in the next package build; rollback is a normal revert of the implementation
and its OpenSpec change.

## Open Questions

None. The existing cache-backed metadata/preflight/materialization flow establishes the required
boundary and can be generalized without a new public abstraction.
