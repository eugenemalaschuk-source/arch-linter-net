using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        var diagnostics = cycles.Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName: string.Empty, contractId: null));
        return string.Join(Environment.NewLine, diagnostics.OrderBy(d => d.Path).Select(d => $"- {d.Path}"));
    }

    public string FormatCyclesForHumans(IReadOnlyCollection<ArchitectureCycleFinding> cycles)
    {
        var diagnostics = cycles.Select(ArchitectureDiagnosticMapper.FromCycle);
        return string.Join(
            Environment.NewLine,
            diagnostics
                .OrderBy(d => d.Path, StringComparer.Ordinal)
                .ThenBy(d => d.ContractId, StringComparer.Ordinal)
                .Select(d =>
                {
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

    public string FormatCyclesForCiArtifacts(
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
            cycle_diagnostics = diagnostics.Select(ToCycleJsonObject).ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, object?> ToCycleJsonObject(ArchitectureCycleFinding cycle)
    {
        return ToCycleJsonObject(ArchitectureDiagnosticMapper.FromCycle(cycle));
    }

    private static Dictionary<string, object?> ToCycleJsonObject(CycleDiagnostic diagnostic)
    {
        var obj = new Dictionary<string, object?>
        {
            ["contract"] = diagnostic.ContractName,
            ["contract_id"] = diagnostic.ContractId,
            ["path"] = diagnostic.Path
        };
        ApplyPolicyLocationFields(diagnostic, obj);
        return obj;
    }
}
