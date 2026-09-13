using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Shared context and policy-file helpers for the CEL selector integration fixtures.
/// It is deliberately test-free so contextual contract fixtures do not inherit layer tests.
/// </summary>
public abstract class CelSelectorContextualIntegrationTestSupport
{
    private string _tempDir = null!;

    protected static string AssemblyName => typeof(CelSelectorContextualIntegrationTests).Assembly.GetName().Name!;

    [SetUp]
    public void SetUpCelSelectorFixture()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-cel-selector-contextual-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDownCelSelectorFixture()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    protected static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp", new[] { typeof(CelSelectorContextualIntegrationTests).Assembly }, Array.Empty<string>(), Array.Empty<string>());
    }

    protected ArchitectureContractDocument Load(string yaml) =>
        new ArchitecturePolicyDocumentLoader().Load(WritePolicy(yaml));

    protected static Type[] TypesMatchingLayer(ArchitectureAnalysisSession session, ArchitectureLayer layer)
    {
        return typeof(CelSelectorContextualIntegrationTests).Assembly.GetTypes()
            .Where(t => ArchitectureLayerMatchesForTest(session, layer, t))
            .ToArray();
    }

    protected static bool ArchitectureLayerMatchesForTest(ArchitectureAnalysisSession session, ArchitectureLayer layer, Type type)
    {
        return ArchitectureLayerTypeMatcher.Matches(
            layer, type, session.RoleIndex, session.ExpressionFacts);
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
