using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public static class ArchitectureDiagnosticFormatter
{
    public static string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return string.Join(
            Environment.NewLine,
            violations
                .OrderBy(violation => violation.SourceType)
                .ThenBy(violation => violation.ForbiddenNamespace)
                .Select(violation =>
                {
                    string idPrefix = violation.ContractId != null ? $"[{violation.ContractId}] " : string.Empty;
                    return $"- {idPrefix}[{violation.ContractName}] {violation.SourceType} -> {violation.ForbiddenNamespace}: {string.Join(", ", violation.ForbiddenReferences)}";
                }));
    }

    public static string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        return string.Join(Environment.NewLine, cycles.OrderBy(c => c).Select(cycle => $"- {cycle}"));
    }

    public static string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles)
    {
        var payload = new
        {
            passed,
            mode,
            violations = violations.Select(v => new
            {
                contract = v.ContractName,
                contract_id = v.ContractId,
                source = v.SourceType,
                forbidden_namespace = v.ForbiddenNamespace,
                forbidden_references = v.ForbiddenReferences.ToArray()
            }).ToArray(),
            cycles = cycles.ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations)
    {
        var payload = new
        {
            kind = "architecture_violations",
            contract = contractName,
            contract_id = contractId,
            violations = violations.Select(v => new
            {
                source = v.SourceType,
                forbidden_namespace = v.ForbiddenNamespace,
                forbidden_references = v.ForbiddenReferences.ToArray()
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles)
    {
        var payload = new
        {
            kind = "architecture_cycles",
            contract = contractName,
            contract_id = contractId,
            cycles = cycles.ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }
}
