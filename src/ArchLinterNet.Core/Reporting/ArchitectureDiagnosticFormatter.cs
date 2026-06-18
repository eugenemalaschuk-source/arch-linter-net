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
                    $"- {violation.SourceType} -> {violation.ForbiddenNamespace}: {string.Join(", ", violation.ForbiddenReferences)}"));
    }

    public static string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        return string.Join(Environment.NewLine, cycles.Select(cycle => $"- {cycle}"));
    }

    public static string FormatViolationsForCiArtifacts(string contractName,
        IReadOnlyCollection<ArchitectureViolation> violations)
    {
        var payload = new
        {
            kind = "architecture_violations",
            contract = contractName,
            violations = violations.Select(v => new
            {
                source = v.SourceType,
                forbidden_namespace = v.ForbiddenNamespace,
                forbidden_references = v.ForbiddenReferences.ToArray()
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string FormatCyclesForCiArtifacts(string contractName, IReadOnlyCollection<string> cycles)
    {
        var payload = new
        {
            kind = "architecture_cycles",
            contract = contractName,
            cycles = cycles.ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }
}
