namespace ArchLinterNet.Core.Scanning;

// Small internal entry facade for one caller-selected visible contract root. The traversal and
// metadata scanners all share the one state created here for the duration of this scan.
internal static class ArchitectureContractSurfaceExposureScanner
{
    internal static ArchitectureContractSurfaceExposureResult Scan(Type root)
    {
        return Scan(root, ArchitectureContractSurfaceShape.Exported);
    }

    internal static ArchitectureContractSurfaceExposureResult Scan(
        Type root,
        ArchitectureContractSurfaceShape surfaceShape)
    {
        ArgumentNullException.ThrowIfNull(root);
        surfaceShape.EnsureValid();

        ArchitectureContractSurfaceExposureScanState state =
            new(root);
        ArchitectureContractSurfaceExposureTraversal traversal =
            new(state, surfaceShape);
        traversal.ScanDeclaredType(root, state.RootPath);
        return state.CreateResult();
    }
}
