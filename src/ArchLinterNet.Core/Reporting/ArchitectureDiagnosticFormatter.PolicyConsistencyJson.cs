using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static Dictionary<string, object?> ToPolicyConsistencyJsonObject(
        PolicyConsistencyDiagnostic finding,
        string? mode)
    {
        return ToCiJsonObject(ArchitectureFindingMapper.FromDiagnostic(finding, mode), includeContract: true);
    }
}
