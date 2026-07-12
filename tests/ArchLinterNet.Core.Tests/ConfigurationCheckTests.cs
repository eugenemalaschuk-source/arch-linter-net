using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

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
            new[] { "Nonexistent.Assembly.That.Does.Not.Exist" },
            Array.Empty<string>());

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
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.True);
    }

    [Test]
    public void CheckConfiguration_EmptySelectorLayer_ReturnsSelectorViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["empty"] = new()
                {
                    Selector = new ArchitectureLayerSelector { Role = "MissingRole" }
                }
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
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer selector"), Is.True);
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
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckConfiguration_ExternalLayerWithNoTypes_NoViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["ext"] = new() { Namespace = "External.Namespace.With.No.Types", External = true }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "test", Layers = new List<string> { "ext" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.False);
    }

    [Test]
    public void CheckConfiguration_NonExternalEmptyLayer_StillProducesViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["empty"] = new() { Namespace = "Empty.Namespace.With.No.Types", External = false }
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
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.True);
    }

    [Test]
    public void CheckConfiguration_ExternalLayerWithTypes_NoViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core", External = true }
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
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.False);
    }

    [Test]
    public void CheckConfiguration_ExternalLayer_StrictAndAuditModes()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["ext"] = new() { Namespace = "External.Empty", External = true },
                ["normal"] = new() { Namespace = "Another.Empty" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "strict-test", Layers = new List<string> { "ext" } }
                },
                AuditLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "audit-test", Layers = new List<string> { "normal" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var strictViolations = runner.CheckConfiguration(strict: true);
        var auditViolations = runner.CheckConfiguration(strict: false);

        // External layer should not produce violations in either mode
        Assert.That(strictViolations.Any(v =>
            v.ForbiddenNamespace == "empty layer namespace" &&
            v.SourceType.Contains("External.Empty")), Is.False);

        // Non-external audit layer should produce violations in audit mode
        Assert.That(auditViolations.Any(v =>
            v.ForbiddenNamespace == "empty layer namespace" &&
            v.SourceType.Contains("Another.Empty")), Is.True);
    }
}
