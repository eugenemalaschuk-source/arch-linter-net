## Why

The v0.6.0 packed-artifact Checkpoint B gate proves that one immutable candidate installs and
behaves identically on every supported platform. It does not prove the thing v0.6.1 exists to
prove: that the F1–F11 correctness fixes plus source-set authoring (#465) actually let a real
external consumer delete its 0.6.0 adoption workarounds. Today that claim is only supported by
`ProjectReference` unit coverage, and the packed gate cannot fail when a workaround is still
required.

Two concrete defects block release authorization as it stands:

- The gate emits `public-api-snapshot-workflow` and `missing-shared-framework-diagnostic` scenario
  results that the aggregator's required inventory does not know about, so aggregation rejects
  every platform record with "incomplete scenario inventory" before it can authorize anything.
- The packed README, CLI docs, and release guidance still identify `0.6.0` as the public adoption
  package line, so a 0.6.1 candidate would ship self-contradicting release identity.

## What Changes

- Add a release-blocking consumer-cleanup matrix to the packed-artifact gate that runs the F1–F11
  and #465 regressions against the installed candidate tool and packages from the isolated feed,
  not against the source tree.
- Add one synthetic modular consumer fixture whose composed policy is the release's
  policy-shape evidence: 20 module assemblies governed through one authored directional assembly
  contract, project-metadata contracts reusing one solution-discovered project set, glob namespace
  allowances, declared layer overlaps, and reviewed public-API snapshots.
- Require every platform evidence record to carry typed policy-shape counters, and make the
  aggregator reject a candidate whose canonical consumer policy still needs a workaround shape.
- Reconcile the scenario inventory the gate actually produces with the inventory the aggregator
  requires, and emit an explicit PASS/FAIL publication statement for the candidate version.
- Advance the public release identity from `0.6.0` to `0.6.1` across the packaged README, CLI and
  release documentation, and the package-validation assertions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: add the release-blocking consumer-cleanup matrix, typed
  policy-shape evidence, and the reconciled scenario inventory with an explicit publication
  statement.
- `checkpoint-b-candidate-provenance`: require the candidate's own release identity (packaged
  README, CLI version, packaged schema registry product version) to agree with the manifested
  candidate version.
- `adoption-stabilization-compatibility`: record that the 0.6.1 package line is the public
  adoption line and that its adoption workarounds are removable through documented behavior.
- `docs-site`: identify `0.6.1` as the current public adoption package line.

## Impact

Affected areas are the packed-artifact release gate tests and the synthetic adoption corpus, the
release-evidence aggregation tool, the release and package-validation workflows, the root README,
CLI/release documentation, and the corresponding OpenSpec contracts. No product runtime code,
public API, or schema format changes.
