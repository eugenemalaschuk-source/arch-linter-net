using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal interface IArchitectureExternalDependencyIlScanner
{
    IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        Type[] sourceTypes,
        string externalGroupName,
        ArchitectureExternalDependencyGroup externalGroup,
        ArchitectureContractExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
