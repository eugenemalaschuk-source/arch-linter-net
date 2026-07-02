using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Tests;

internal sealed class FakeArchitectureEnvironment : IArchitectureEnvironment
{
    private readonly Dictionary<string, string?> _variables = new(StringComparer.Ordinal);

    public string BaseDirectory { get; set; } = "/fake/base";

    public void SetEnvironmentVariable(string name, string? value)
    {
        _variables[name] = value;
    }

    public string? GetEnvironmentVariable(string name)
    {
        return _variables.TryGetValue(name, out string? value) ? value : null;
    }
}
