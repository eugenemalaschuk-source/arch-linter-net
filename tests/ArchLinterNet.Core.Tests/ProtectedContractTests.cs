using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ProtectedContractTests
{
    private static Assembly CoreAssembly => typeof(ArchitectureContractLoader).Assembly;
    private static Assembly TestAssembly => typeof(ProtectedContractTests).Assembly;

    [Test]
    public void CheckProtectedContract_AllowedImporter_NoViolations()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["test_area"] = new() { Namespace = "ArchLinterNet.Core.Tests" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string> { "test_area" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckProtectedContract_NonAllowedLayer_ProducesViolations()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string>()
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Not.Empty);
    }

    [Test]
    public void CheckProtectedContract_SelfReferenceWithinProtectedLayer_NoSelfViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string>()
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations.All(v => !v.SourceType.StartsWith("ArchLinterNet.Core.Execution")), Is.True);
    }

    [Test]
    public void CheckProtectedContract_AllowedTypesOverride_SuppressesViolation()
    {
        string thisTestType = "ArchLinterNet.Core.Tests.ProtectedContractTests+PermittedConsumer";

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string>(),
                        AllowedTypes = new List<string> { thisTestType }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        var permitsViolations = violations.Where(v => v.SourceType == thisTestType).ToList();
        Assert.That(permitsViolations, Is.Empty, "PermittedConsumer should be exempted by allowed_types");
        Assert.That(violations.Where(v => v.SourceType != thisTestType), Is.Not.Empty,
            "Other non-exempt types should still produce violations");
    }

    [Test]
    public void CheckProtectedContract_IgnoredViolations_BaselinesExisting()
    {
        string sourceType = "ArchLinterNet.Core.Tests.ProtectedContractTests+ExecutionUser";

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string>(),
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = sourceType,
                                ForbiddenReference = "ArchLinterNet.Core.Execution.*",
                                Reason = "Baseline for test"
                            }
                        }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        var baselinedViolations = violations.Where(v => v.SourceType == sourceType).ToList();
        Assert.That(baselinedViolations, Is.Empty, "Baselined violations should be suppressed");
        Assert.That(violations.Where(v => v.SourceType != sourceType), Is.Not.Empty,
            "Non-baselined violations should still appear");
    }

    [Test]
    public void CheckProtectedContract_MultipleProtectedLayers_IndependentlyEnforced()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["test_area"] = new() { Namespace = "ArchLinterNet.Core.Tests" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string>()
                    },
                    new()
                    {
                        Name = "execution-allowed-for-test",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string> { "test_area" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract1Violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);
        var contract2Violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[1]);

        Assert.That(contract1Violations, Is.Not.Empty, "Contract with no allowed importers should have violations");
        Assert.That(contract2Violations, Is.Empty,
            "Contract with test_area as allowed importer should have no violations");
    }

    [Test]
    public void CheckProtectedContract_UnknownLayer_ThrowsInvalidOperation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "bad-ref",
                        Protected = new List<string> { "nonexistent_layer" },
                        AllowedImporters = new List<string>()
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        Assert.That(
            () => runner.CheckProtectedContract(document.Contracts.StrictProtected[0]),
            Throws.InvalidOperationException);
    }

    [Test]
    public void CheckProtectedContract_ViolationIncludesSourceLayerContext()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Id = "exec-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string>(),
                        Reason = "Testing layer context"
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Not.Empty);
        var violation = violations[0];

        Assert.That(violation.SourceLayer, Is.Null, "Source with no matching layer should have null SourceLayer");
        Assert.That(violation.TargetLayer, Is.EqualTo("execution"));
        Assert.That(violation.AllowedImporters, Is.Not.Null);
        Assert.That(violation.AllowedImporters, Is.Empty);
        Assert.That(violation.ForbiddenNamespace, Does.Contain("protected layer 'execution'"));
    }

    [Test]
    public void CheckProtectedContract_JsonOutput_IncludesProtectedContext()
    {
        var violation = new ArchitectureViolation(
            "execution-is-protected",
            "exec-protected",
            "ArchLinterNet.Core.Tests.SomeUser",
            "protected layer 'execution' (allowed importers: [core])",
            new[] { "ArchLinterNet.Core.Execution.ArchitectureContractRunner" })
        {
            SourceLayer = "test_area",
            TargetLayer = "execution",
            AllowedImporters = new[] { "core" }
        };

        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict", false,
            new[] { violation },
            Array.Empty<string>());

        Assert.That(json, Does.Contain("\"source_layer\":\"test_area\""));
        Assert.That(json, Does.Contain("\"target_layer\":\"execution\""));
        Assert.That(json, Does.Contain("\"allowed_importers\":[\"core\"]"));
    }

    [Test]
    public void CheckProtectedContract_StandardViolation_NoProtectedContextInJson()
    {
        var violation = new ArchitectureViolation(
            "my-contract",
            null,
            "App.Web.Controller",
            "Infrastructure",
            new[] { "Infrastructure.DbContext" });

        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict", false,
            new[] { violation },
            Array.Empty<string>());

        Assert.That(json, Does.Not.Contain("source_layer"));
        Assert.That(json, Does.Not.Contain("target_layer"));
        Assert.That(json, Does.Not.Contain("allowed_importers"));
    }

    [Test]
    public void CheckProtectedContract_UnknownAllowedImporter_ThrowsInvalidOperation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "bad-allowed-importer",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string> { "nonexistent_importer" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        Assert.That(
            () => runner.CheckProtectedContract(document.Contracts.StrictProtected[0]),
            Throws.InvalidOperationException);
    }

    [Test]
    public void CheckProtectedContract_AllowedImporterParentLayer_CoversChildNamespaces()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["core_tests"] = new() { Namespace = "ArchLinterNet.Core.Tests" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "execution" },
                        AllowedImporters = new List<string> { "core" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Empty,
            "Types in child namespaces of allowed importer parent layer should be permitted");
    }

    [Test]
    public void CheckProtectedContract_ViolationTargetLayerIsProtectedLayerName()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core_execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["execution_internal"] = new() { Namespace = "ArchLinterNet.Core.Execution.Internal" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "execution-is-protected",
                        Protected = new List<string> { "core_execution" },
                        AllowedImporters = new List<string>()
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly, TestAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Not.Empty);
        foreach (var violation in violations)
        {
            Assert.That(violation.TargetLayer, Is.EqualTo("core_execution"),
                "TargetLayer must be the protected layer name, not a nested child layer");
        }
    }

    public sealed class ExecutionUser
    {
        private readonly ArchitectureContractRunner _runner;

        public ExecutionUser(ArchitectureContractRunner runner)
        {
            _runner = runner;
        }

        public ArchitectureContractRunner Runner => _runner;
    }

    public sealed class PermittedConsumer
    {
        private readonly ArchitectureContractRunner _runner;

        public PermittedConsumer(ArchitectureContractRunner runner)
        {
            _runner = runner;
        }

        public ArchitectureContractRunner Runner => _runner;
    }
}
