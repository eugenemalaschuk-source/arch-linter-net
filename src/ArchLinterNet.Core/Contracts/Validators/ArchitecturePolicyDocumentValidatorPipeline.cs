namespace ArchLinterNet.Core.Contracts.Validators;

// Distinct from Execution.ArchitectureContractFamilyRegistry: that registry drives contract
// cataloguing/execution and lives in Execution (which may depend on Contracts). This pipeline
// drives ArchitecturePolicyDocumentLoader.Load's post-deserialization validation and must stay
// inside Contracts, which per docs/internal/core-architecture-blueprint.md depends on nothing
// else in Core. The two orderings are independent and are not expected to match.
internal static class ArchitecturePolicyDocumentValidatorPipeline
{
    // Order matches ArchitecturePolicyDocumentLoader.Load's validation call sequence prior to
    // this pipeline's introduction. Exceptions are thrown eagerly (first-match-wins), so this
    // order is load-bearing behavior and must not be reordered without reviewing every affected
    // invalid-policy test.
    public static IReadOnlyList<IArchitecturePolicyDocumentValidator> All { get; } =
    [
        new DuplicateIdValidator(),
        new AcyclicSiblingValidator(),
        new LayerNamespacesValidator(),
        new CoverageValidator(),
        new AssemblyIndependenceValidator(),
        new AssemblyDependencyValidator(),
        new AssemblyAllowOnlyValidator(),
        new PackageDependencyValidator(),
        new PackageAllowOnlyValidator(),
        new ProjectMetadataValidator(),
        new TypePlacementValidator(),
        new PublicApiSurfaceValidator(),
        new AttributeUsageValidator(),
        new InheritanceValidator(),
        new InterfaceImplementationValidator(),
        new CompositionValidator(),
        new ContextualContractValidator(),
    ];
}
