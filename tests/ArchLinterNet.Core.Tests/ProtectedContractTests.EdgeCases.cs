using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ProtectedContractTests
{
    private static readonly Assembly[] TestAssemblyArray = { TestAssembly };
    private static readonly Assembly[] CoreAssemblyTestAssembly = { CoreAssembly, TestAssembly };

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
            Payload = new DependencyPayload(
                SourceLayer: "test_area",
                TargetLayer: "execution",
                AllowedImporters: new[] { "core" })
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false,
            new[] { violation },
            Array.Empty<string>());

        Assert.That(json, Does.Contain("\"source_layer\":\"test_area\""));
        Assert.That(json, Does.Contain("\"target_layer\":\"execution\""));
        Assert.That(json, Does.Contain("\"allowed_importers\":[\"core\"]"));
    }

    [Test]
    public void CheckProtectedContract_GlobProtectedLayer_IncludesMatchedNamespacePrefixes()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["glob_target"] = new() { Namespace = "ProtectedGlob.Target.*" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "glob-target-is-protected",
                        Protected = new List<string> { "glob_target" },
                        AllowedImporters = new List<string>()
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            TestAssemblyArray,
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.Any(v =>
            v.MatchedNamespacePrefixes != null
            && v.MatchedNamespacePrefixes.Contains("ProtectedGlob.Target.Execution")), Is.True);
    }

    [Test]
    public void CheckProtectedContract_SourceTypeDirectlyInGlobParentNamespace_DoesNotResolveToGlobLayer()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["glob_target"] = new() { Namespace = "ProtectedGlob.Target.*" },
                ["glob_importers"] = new() { Namespace = "ProtectedGlob.Importers.*" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new()
                    {
                        Name = "glob-target-is-protected",
                        Protected = new List<string> { "glob_target" },
                        AllowedImporters = new List<string>()
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            TestAssemblyArray,
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        ArchitectureViolation? violation = violations.SingleOrDefault(
            v => v.SourceType == "ProtectedGlob.Importers.DirectImporter");

        Assert.That(violation, Is.Not.Null);
        Assert.That((violation!.Payload as DependencyPayload)?.SourceLayer, Is.Null,
            "A type declared directly in 'ProtectedGlob.Importers' (no descendant segment) must not be " +
            "classified as belonging to the glob layer 'ProtectedGlob.Importers.*', which requires one " +
            "additional namespace segment.");
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

        string json = _formatter.FormatResultForCiArtifacts(
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
            CoreAssemblyTestAssembly,
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
            CoreAssemblyTestAssembly,
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        Assert.That(violations, Is.Not.Empty);
        foreach (var violation in violations)
        {
            Assert.That((violation.Payload as DependencyPayload)?.TargetLayer, Is.EqualTo("core_execution"),
                "TargetLayer must be the protected layer name, not a nested child layer");
        }
    }

    [Test]
    public void CheckProtectedContract_GlobalNamespaceSourceType_ProducesViolation()
    {
        AssemblyName assemblyName = new("GlobalNsTestAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder typeBuilder = moduleBuilder.DefineType("GlobalNsRunnerUser", TypeAttributes.Public);
        typeBuilder.DefineField("_runner", typeof(ArchitectureContractRunner), FieldAttributes.Private);
        Type globalType = typeBuilder.CreateType();

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
            new[] { CoreAssembly, TestAssembly, assemblyBuilder },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckProtectedContract(document.Contracts.StrictProtected[0]);

        var globalViolations = violations
            .Where(v => v.SourceType == "GlobalNsRunnerUser")
            .ToList();

        Assert.That(globalViolations, Is.Not.Empty,
            "Global-namespace types referencing protected layers must produce violations");
        Assert.That((globalViolations[0].Payload as DependencyPayload)?.SourceLayer, Is.Null,
            "Global-namespace types should have null SourceLayer");
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
