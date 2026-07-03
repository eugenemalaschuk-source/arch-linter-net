namespace ArchLinterNet.Core.IO;

public interface IArchitectureEnvironment
{
    string? GetEnvironmentVariable(string name);

    string BaseDirectory { get; }
}
