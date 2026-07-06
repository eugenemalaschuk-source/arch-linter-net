using System.Linq;
using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class ProtectedContractTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();

    private static Assembly CoreAssembly => typeof(ArchitecturePolicyDocumentLoader).Assembly;
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
                // The Core self-policy's core-application-seam-layering rule intentionally allows
                // Validation to depend on Execution (e.g. the validation/baseline application
                // services are constructor-injected with execution-layer setup/runner services),
                // so this fixture's real-assembly scan must allow that importer too.
                ["validation"] = new() { Namespace = "ArchLinterNet.Core.Validation" },
                // ServiceCollectionExtensions.ResolveHandlerRegistry's declared return type
                // (ArchitectureContractHandlerRegistry) is itself an execution-layer reference —
                // Composition is the composition root, so wiring the concrete registry through a
                // typed factory method is expected, not a layering violation.
                ["composition"] = new() { Namespace = "ArchLinterNet.Core.Composition" },
                // Graph's application services (ArchitectureGraphApplicationService,
                // ArchitectureExplainApplicationService) orchestrate Core.Execution the same way
                // Validation does, so Graph is expected to import Execution too.
                ["graph"] = new() { Namespace = "ArchLinterNet.Core.Graph" },
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
                        AllowedImporters = new List<string> { "test_area", "validation", "composition", "graph" }
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
                // See CheckProtectedContract_AllowedImporter_NoViolations for why "validation",
                // "composition", and "graph" must be allowed importers of "execution" in this
                // real-assembly fixture.
                ["validation"] = new() { Namespace = "ArchLinterNet.Core.Validation" },
                ["composition"] = new() { Namespace = "ArchLinterNet.Core.Composition" },
                ["graph"] = new() { Namespace = "ArchLinterNet.Core.Graph" },
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
                        AllowedImporters = new List<string> { "test_area", "validation", "composition", "graph" }
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
}
