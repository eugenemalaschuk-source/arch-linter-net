using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal interface IArchitectureIlMethodBodyScanner
{
    IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureLayer? sourceLayer = null,
        CancellationToken cancellationToken = default);
}
