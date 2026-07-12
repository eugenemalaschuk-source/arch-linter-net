namespace ArchLinterNet.Core.IO.Abstractions;

public interface IArchitectureEnvironment
{
    string? GetEnvironmentVariable(string name);

    string BaseDirectory { get; }
}
