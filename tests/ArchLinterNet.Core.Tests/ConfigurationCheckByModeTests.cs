using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ConfigurationCheckByModeTests
{
    [Test]
    public void CheckConfiguration_StrictMode_ChecksOnlyStrictLayers()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["empty-strict"] = new() { Namespace = "Test.Empty.Strict.Namespace" },
                ["empty-audit"] = new() { Namespace = "Test.Empty.Audit.Namespace" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "strict-test", Layers = new List<string> { "empty-strict" } }
                },
                AuditLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "audit-test", Layers = new List<string> { "empty-audit" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureContractLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration(strict: true);

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "empty layer namespace" &&
            v.SourceType.Contains("Test.Empty.Strict")), Is.True);
        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "empty layer namespace" &&
            v.SourceType.Contains("Test.Empty.Audit")), Is.False);
    }

    [Test]
    public void CheckConfiguration_AuditMode_ChecksOnlyAuditLayers()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["empty-strict"] = new() { Namespace = "Test.Empty.Strict.Namespace" },
                ["empty-audit"] = new() { Namespace = "Test.Empty.Audit.Namespace" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "strict-test", Layers = new List<string> { "empty-strict" } }
                },
                AuditLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "audit-test", Layers = new List<string> { "empty-audit" } }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureContractLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckConfiguration(strict: false);

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "empty layer namespace" &&
            v.SourceType.Contains("Test.Empty.Audit")), Is.True);
        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "empty layer namespace" &&
            v.SourceType.Contains("Test.Empty.Strict")), Is.False);
    }

    [Test]
    public void CheckConfiguration_MissingAssemblies_AlwaysIncluded()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "Missing.Assembly" }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            Array.Empty<System.Reflection.Assembly>(),
            new[] { "Missing.Assembly" },
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var strictViolations = runner.CheckConfiguration(strict: true);
        var auditViolations = runner.CheckConfiguration(strict: false);

        Assert.That(strictViolations.Any(v => v.ForbiddenNamespace == "missing target assembly"), Is.True);
        Assert.That(auditViolations.Any(v => v.ForbiddenNamespace == "missing target assembly"), Is.True);
    }
}
