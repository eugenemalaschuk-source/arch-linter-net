using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Reporting.ArchitectureDiagnosticFormatter;

namespace ArchLinterNet.Core.Reporting;

internal static class ArchitecturePolicyConsistencyProjector
{
    internal static Dictionary<string, object?> ToPolicyConsistencyJsonObject(
        PolicyConsistencyDiagnostic finding,
        string? mode)
    {
        return ToCiJsonObject(ArchitectureFindingMapper.FromDiagnostic(finding, mode), includeContract: true);
    }
}
