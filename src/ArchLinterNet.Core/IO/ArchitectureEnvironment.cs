using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.IO;

public sealed class ArchitectureEnvironment : IArchitectureEnvironment
{
    public static readonly ArchitectureEnvironment Real = new();

    public string? GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }

    public string BaseDirectory => AppContext.BaseDirectory;
}
