using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    public string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations) =>
        FormatViolationsForCiArtifacts(contractName, contractId, violations, CancellationToken.None);

    public string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = new
        {
            kind = "architecture_violations",
            contract = contractName,
            contract_id = contractId,
            violations = ArchitectureFindingMapper.Order(
                    ArchitectureFindingMapper.FromViolations(violations, cancellationToken: cancellationToken), cancellationToken)
                .Select(finding => ToCiJsonObject(finding, includeContract: false))
        };

        return JsonSerializer.Serialize(payload);
    }
}
