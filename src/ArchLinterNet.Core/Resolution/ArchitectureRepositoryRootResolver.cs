namespace ArchLinterNet.Core.Resolution;

public interface IArchitectureRepositoryRootResolver
{
    string ResolveFrom(string policyPath);
}

public sealed class ArchitectureRepositoryRootResolver : IArchitectureRepositoryRootResolver
{
    public string ResolveFrom(string policyPath)
    {
        return ArchitectureRepositoryRootLocator.ResolveFrom(policyPath);
    }
}
