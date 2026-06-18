using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ConfigurationCheckTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void CheckConfiguration_MissingAssembly_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "Nonexistent.Assembly.That.Does.Not.Exist" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "test", Layers = new List<string> { "core" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            Array.Empty<System.Reflection.Assembly>(),
            new[] { "Nonexistent.Assembly.That.Does.Not.Exist" });

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.Any(v => v.ForbiddenNamespace == "missing target assembly"), Is.True);
    }

    [Test]
    public void CheckConfiguration_EmptyLayer_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["empty"] = new() { Namespace = "Test.Empty.Namespace.That.Has.No.Types" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "test", Layers = new List<string> { "empty" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(ArchitectureContractLoader).Assembly },
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.True);
    }

    [Test]
    public void CheckConfiguration_AllAssembliesResolved_NoConfigurationViolations()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "test", Layers = new List<string> { "core" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(ArchitectureContractLoader).Assembly },
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Empty);
    }
}
