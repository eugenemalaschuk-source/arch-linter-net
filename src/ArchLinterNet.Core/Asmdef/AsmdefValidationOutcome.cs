using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Asmdef;

public sealed class AsmdefValidationOutcome
{
    public required IReadOnlyCollection<ArchitectureViolation> Violations { get; init; }

    public bool Passed => Violations.Count == 0;
}
