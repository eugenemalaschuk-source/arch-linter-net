namespace ArchLinterNet.Core.Resolution.Abstractions;

public interface IArchitectureRepositoryRootResolver
{
    string Resolve();

    string ResolveFrom(string policyPath);
}
