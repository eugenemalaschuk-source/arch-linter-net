## Context

`ArchitecturePolicyDocumentLoader.LoadCore` currently sequences ten stages: root resolution, source
reading, import resolution/composition, effective-schema validation, raw YAML validation,
deserialization, fallback IDs, provenance binding, deferred-classification detection, API-snapshot
resolution, source-set expansion, and the document-validator pipeline.

Only one of those stages is capability-specific: raw YAML validation. It is implemented as six
private static methods (`ValidateRawLayerYaml`, `ValidateRawContextualContractYaml`,
`ValidateRawSemanticCoverageYaml`, `ValidateRawLayoutConventionYaml`, `ValidateRawLayerTemplateYaml`,
`ValidateRawWhenFieldLocations`) plus their helpers, spread over `ArchitecturePolicyDocumentLoader.cs`
and three partial files that exist only to stay under the repository file-size lint budget. Each
method independently constructs a `YamlStream`, loads the same effective YAML string, and walks that
capability's node shapes.

Raw validation cannot be folded into the existing post-deserialization
`IArchitecturePolicyDocumentValidator` pipeline: it exists precisely because
`IgnoreUnmatchedProperties()` erases unknown keys during deserialization, so these checks must see
the node tree *before* the model exists.

## Goals / Non-Goals

**Goals:**

- Give each capability's raw node validation a focused home, so adding a rule does not extend a
  central all-capabilities method on the loader.
- Keep `ArchitecturePolicyDocumentLoader` an orchestrator: stage sequencing, exception enrichment,
  cancellation, and provenance lifecycle only.
- Preserve exact ordering, diagnostics, provenance/validation-subject evolution and fail-closed
  behavior for both monolithic and imported/composed policies.
- Make reintroduction of loader-owned raw-node algorithms visible in tests.

**Non-Goals:**

- Any change to what constitutes a valid policy, to messages, or to failure ordering.
- A public or plugin-facing extension model for policy validation.
- Rewriting imports/composition, replacing YamlDotNet, or reorganizing the `Contracts` namespace
  beyond the new raw-validator folder.
- Fixing correctness defects tracked by the F1-F11 backlog.

## Decisions

### D1: Mirror the existing validator-pipeline shape rather than invent a new model

The repository already has a proven internal seam for "ordered, capability-specific policy
validation": `IArchitecturePolicyDocumentValidator` plus
`ArchitecturePolicyDocumentValidatorPipeline`. The raw stage gets the deliberately parallel
`IArchitecturePolicyRawDocumentValidator` plus `ArchitecturePolicyRawDocumentValidatorPipeline`, both
`internal` to `ArchLinterNet.Core.Contracts`.

Two pipelines rather than one: the raw pipeline consumes a YAML node tree and runs before
deserialization; the document pipeline consumes `ArchitectureContractDocument` and runs after
provenance binding and source-set expansion. They are not substitutable, and merging them would
require a union input type that every validator would have to narrow.

*Alternatives rejected:* a single `AdditionalValidation`-style delegate registry (already rejected by
`extract-family-validation-from-loader` for coupling `Contracts` to a family registry); a public
extension point (explicitly a non-goal — no public API is added for the refactor).

### D2: Parse the effective YAML once into a shared raw-document context

Each raw check currently re-parses the same string. The pipeline takes an
`ArchitecturePolicyRawDocument` carrying the parsed root mapping node plus the provenance index.

This is behavior-preserving: the same string parses to the same tree, and the parse stays inside the
enriched-exception block at the same semantic point, so a malformed-YAML `YamlException` still
surfaces from the same stage. The per-validator "no documents / root is not a mapping" early return
becomes a single `Root is null` guard expressed through `TryGetSection`.

The deferred-`classification.path` detector keeps its own parse: it runs *after* deserialization and
provenance binding, so sharing the raw pipeline's context would couple two stages that are separated
by design.

### D3: Split contextual and port-boundary raw validation into separate validators

`ValidateRawContextualContractYaml` today validates four contextual groups and then two
port-boundary groups in one method. They are separate contract families with separate node shapes,
so they become `RawContextualContractNodeValidator` and `RawPortBoundaryNodeValidator`, registered
adjacently in that order. The sequence of key checks, thrown exceptions and
`SetValidationSubject` calls is unchanged, so first-match-wins failure ordering is preserved.

Shared vocabulary (node navigation, `<unnamed>` contract naming, contract provenance paths,
known-key checks) moves to `RawYamlNodes`; the contextual-selector key rules shared by contextual and
port-boundary contracts move to `RawContextualSelectorKeys`. Message text, including the
`Contextual contract '<name>' declares an unknown property ...` wording used by several families, is
copied verbatim.

### D4: The validation subject is pipeline state, not per-validator state

`ArchitecturePolicyProvenanceIndex.SetValidationSubject` is sticky: the subject set by one raw check
remains current until the next call, and the loader resets it once in a `finally` after the whole raw
stage. Validators therefore must not reset the subject on entry or exit; they keep making exactly the
`SetValidationSubject` calls their originating method made, in the same order. This is what keeps
authored/imported location evidence identical for malformed root and imported-fragment cases.

### D5: Guard the boundary with reflection over the loader type

The self-architecture policy is namespace-scoped and cannot distinguish the loader from its
neighbours in `ArchLinterNet.Core.Contracts`. The boundary is instead guarded by focused NUnit
architecture tests asserting that:

- `ArchitecturePolicyDocumentLoader` references no `YamlDotNet.RepresentationModel` type at all.
  Signatures alone are not enough: the extracted checks looked like
  `ValidateRawLayerYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)` and built the node
  tree inside their own bodies, so a signature-only guard would let exactly that shape back in. The
  guard therefore checks the loader's source *and* its compiled members, method locals and
  compiler-generated captured state, and is itself verified against a probe reproducing the old shape;
- every `IArchitecturePolicyRawDocumentValidator` in the Core assembly is registered exactly once in
  the pipeline, so a new raw validator cannot be silently unreachable;
- the pipeline's order matches the documented pre-refactor call order.

## Risks / Trade-offs

- **Risk:** a moved check silently changes failure ordering. *Mitigation:* the pipeline order and the
  intra-validator call order are copied verbatim, and the existing raw-validation regression matrix
  (layers, contextual selectors, semantic coverage, layout conventions, layer templates, `when`
  placement) runs unchanged for monolithic and imported policies.
- **Risk:** provenance/location evidence drifts. *Mitigation:* D4 plus explicit
  authored-vs-imported location regression tests for representative malformed root and
  imported-fragment cases.
- **Trade-off:** two parallel validation pipelines in one namespace. Accepted: they have different
  inputs and run at different points, and naming (`Raw...`) keeps them distinguishable.
