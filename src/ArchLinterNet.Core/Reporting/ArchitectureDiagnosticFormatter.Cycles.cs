using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        var findings = cycles
            .Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName: string.Empty, contractId: null))
            .Select(ArchitectureFindingMapper.FromDiagnostic);
        return string.Join(
            Environment.NewLine,
            findings
                .OrderBy(finding => ((CycleDiagnostic)finding.Details).Path)
                .Select(finding => $"- {((CycleDiagnostic)finding.Details).Path}"));
    }

    public static string FormatCyclesForHumans(IReadOnlyCollection<ArchitectureCycleFinding> cycles)
    {
        var findings = cycles
            .Select(ArchitectureDiagnosticMapper.FromCycle)
            .Select(ArchitectureFindingMapper.FromDiagnostic);
        return string.Join(
            Environment.NewLine,
            findings
                .OrderBy(finding => ((CycleDiagnostic)finding.Details).Path, StringComparer.Ordinal)
                .ThenBy(finding => finding.ContractId, StringComparer.Ordinal)
                .Select(finding =>
                {
                    var d = (CycleDiagnostic)finding.Details;
                    string idPrefix = d.ContractId != null ? $"[{d.ContractId}] " : string.Empty;
                    return $"- {idPrefix}{d.Path}{FormatPolicyLocationSuffix(d)}";
                }));
    }

    public string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles)
    {
        var diagnostics = cycles.Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName, contractId));

        var payload = new
        {
            kind = "architecture_cycles",
            contract = contractName,
            contract_id = contractId,
            cycles = diagnostics.Select(d => d.Path).ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string FormatCyclesForCiArtifacts(
        string contractName,
        string? contractId,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles)
    {
        CycleDiagnostic[] diagnostics = cycles.Select(ArchitectureDiagnosticMapper.FromCycle).ToArray();

        var payload = new
        {
            kind = "architecture_cycles",
            contract = contractName,
            contract_id = contractId,
            cycles = diagnostics.Select(d => d.Path).ToArray(),
            cycle_diagnostics = diagnostics.Select(cycle => ToCycleJsonObject(cycle, mode: null)).ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, object?> ToCycleJsonObject(ArchitectureCycleFinding cycle, string? mode) =>
        ToCycleJsonObject(ArchitectureDiagnosticMapper.FromCycle(cycle), mode);

    private static Dictionary<string, object?> ToCycleJsonObject(CycleDiagnostic diagnostic, string? mode)
    {
        ArchitectureFinding finding = ArchitectureFindingMapper.FromDiagnostic(diagnostic, mode);
        Dictionary<string, object?> obj = ToCiJsonObject(finding, includeContract: true);
        obj["path"] = diagnostic.Path;
        return obj;
    }
}
