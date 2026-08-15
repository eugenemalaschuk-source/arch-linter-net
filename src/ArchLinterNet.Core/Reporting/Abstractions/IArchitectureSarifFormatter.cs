using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting.Abstractions;

public partial interface IArchitectureSarifFormatter
{
    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        string toolVersion);
}
