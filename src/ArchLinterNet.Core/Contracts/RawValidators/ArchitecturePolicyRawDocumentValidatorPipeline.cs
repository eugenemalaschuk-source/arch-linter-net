namespace ArchLinterNet.Core.Contracts.RawValidators;

// Distinct from Validators/ArchitecturePolicyDocumentValidatorPipeline: this pipeline runs before
// deserialization over the YAML node tree, that one runs after provenance binding and source-set
// expansion over the deserialized ArchitectureContractDocument. The two inputs are not
// interchangeable, so the seams stay separate.
internal static class ArchitecturePolicyRawDocumentValidatorPipeline
{
    // Order reproduces ArchitecturePolicyDocumentLoader.LoadCore's raw-validation call sequence prior
    // to this pipeline's introduction (layers, contextual contracts, port boundaries, semantic
    // coverage, layout conventions, layer templates, `when` placement). Exceptions are thrown eagerly
    // (first-match-wins), so this order is load-bearing behavior and must not be reordered without
    // reviewing every affected invalid-policy test.
    public static IReadOnlyList<IArchitecturePolicyRawDocumentValidator> All { get; } =
    [
        new RawLayerNodeValidator(),
        new RawContextualContractNodeValidator(),
        new RawPortBoundaryNodeValidator(),
        new RawSemanticCoverageNodeValidator(),
        new RawLayoutConventionNodeValidator(),
        new RawLayerTemplateNodeValidator(),
        new RawWhenFieldLocationValidator(),
    ];
}
