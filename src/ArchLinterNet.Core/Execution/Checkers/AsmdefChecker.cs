using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class AsmdefChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureAsmdefContract contract,
        ArchitectureCheckerContext context)
    {
        return new ArchitectureAsmdefScanner()
            .FindAsmdefViolations(contract.Name, contract.Id, context.AnalysisContext.RepositoryRoot, contract)
            .ToList();
    }
}
