using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting.Abstractions;

public interface IArchitectureSarifFormatter
{
    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        string toolVersion);

    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        string toolVersion);
}
