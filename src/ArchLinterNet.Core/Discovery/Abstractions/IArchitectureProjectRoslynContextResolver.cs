namespace ArchLinterNet.Core.Discovery.Abstractions;

internal interface IArchitectureProjectRoslynContextResolver
{
    ArchitectureProjectRoslynResolution Resolve(string projectAbsolutePath);
}
