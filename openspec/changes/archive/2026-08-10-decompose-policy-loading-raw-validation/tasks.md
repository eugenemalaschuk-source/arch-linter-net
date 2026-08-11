## 1. Raw validation seam

- [x] 1.1 Add the internal raw-document context (`ArchitecturePolicyRawDocument`), the
  `IArchitecturePolicyRawDocumentValidator` interface and the ordered
  `ArchitecturePolicyRawDocumentValidatorPipeline` under `Contracts/RawValidators/`.
- [x] 1.2 Extract shared raw-node vocabulary (`RawYamlNodes`, `RawContextualSelectorKeys`) with
  verbatim message text and provenance path construction.

## 2. Capability-specific raw validators

- [x] 2.1 Move layer raw validation into `RawLayerNodeValidator`.
- [x] 2.2 Move contextual-contract and port-boundary raw validation into
  `RawContextualContractNodeValidator` and `RawPortBoundaryNodeValidator`, preserving order.
- [x] 2.3 Move semantic-coverage, layout-convention and layer-template raw validation into their own
  validators.
- [x] 2.4 Move `when` placement validation into `RawWhenFieldLocationValidator` and delete the loader
  raw-validation partial files.

## 3. Loader orchestration

- [x] 3.1 Reduce `LoadCore` to stage sequencing over the raw pipeline, deserialization, preparation
  helpers and the document-validator pipeline.
- [x] 3.2 Move deferred classification-path detection and fallback-ID assignment into focused helpers
  while keeping `NormalizeToContractId` on the loader's existing public surface.

## 4. Tests and verification

- [x] 4.1 Add architecture regression tests for the loader boundary, pipeline registration and
  pipeline order.
- [x] 4.2 Add raw-validation ordering and authored/imported provenance regression tests covering
  layers, contextual selectors, semantic coverage, layout conventions, layer templates and `when`
  placement.
- [x] 4.3 Run focused loader/import/provenance/raw-validation tests, `make fmt`, and `make
  acceptance`.
- [x] 4.4 Synchronize specs with the implementation, run `openspec validate --all`, and archive the
  change.
