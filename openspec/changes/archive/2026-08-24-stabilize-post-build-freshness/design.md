## Context

`--ensure-built` prepares project and artifact metadata without loading the selected target DLL,
performs the graph build, then needs to authorize the resulting bytes before materialization. The
current second metadata preparation repeats project-output discovery. Discovery intentionally
rejects an output whose filesystem timestamp predates a source timestamp, but some macOS filesystems
can publish the completed output with an earlier timestamp than the source by a few milliseconds.
The receipt emitted by the build already binds the selected path, effective build context, build
input fingerprint, and output digest, and ordinary preflight verifies those properties.

## Goals / Non-Goals

**Goals:**

- Use receipt and digest evidence to authorize the exact selected post-build artifact closure.
- Retain the existing no-load-before-build order required for Windows file replacement.
- Keep timestamp-based stale-output detection for ordinary discovery.
- Make the Release-policy regression deterministic evidence for the successful post-build path.

**Non-Goals:**

- Adding a timestamp tolerance or changing ordinary discovery freshness rules.
- Redesigning project discovery, receipt schema, build inputs, cache identity, or build invocation.
- Re-resolving a different output configuration after the graph build.

## Decisions

### Refresh the existing selection rather than rediscovering outputs

After a non-blocking ensure-built preflight, snapshot construction will retain the originally
prepared project graph and selected metadata reference closure. It will recompute content digests
for each selected DLL, its PDB, and its receipt, then run the ordinary preflight against those
paths. This keeps selected-output provenance and effective build context fixed while ensuring that
neither a pre-build artifact digest nor a missing pre-build receipt authorizes materialization.

The ordinary preflight remains the authority for receipt identity, effective configuration/TFM/
platform/RID, build-input fingerprint, and the current DLL digest. The result is therefore stronger
than timestamp ordering and still fails closed for a changed source, wrong output, missing receipt,
or artifact replacement after build.

Alternative considered: relax `FinishResolve` by accepting a timestamp window. Rejected because it
would weaken ordinary stale artifact detection and couple correctness to filesystem clock behavior.

Alternative considered: run the second discovery but pass a special freshness mode. Rejected because
it would introduce a context-sensitive exception inside generic discovery and could accidentally be
used outside the successful build-to-receipt transition.

## Risks / Trade-offs

- [A project graph changes during the build] → Ordinary post-build receipt verification recomputes
  fingerprints and evaluated manifests and blocks on any mismatch before assembly loading.
- [An artifact, PDB, or receipt changes after refresh] → existing prepared-artifact digest checks
  reject materialization.
- [Reference metadata could change with the build] → the selected closure retains the initial
  project graph; a graph-affecting input change is caught by the receipt fingerprint before loading.

## Migration Plan

No migration is needed. The change is internal and applies to the existing ensure-built lifecycle.
Rollback is a normal code revert; receipts and cache entries remain compatible.

## Open Questions

None.
