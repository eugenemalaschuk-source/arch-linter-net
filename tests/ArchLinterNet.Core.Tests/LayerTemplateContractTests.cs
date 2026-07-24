using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerTemplateContractTests
{
    private static Assembly CoreAssembly => typeof(ArchitectureContractDocument).Assembly;

    private static string CoreAssemblyName => CoreAssembly.GetName().Name!;

    [Test]
    public void CheckLayerContract_MissingRequiredLayer_ProducesViolation()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "template-test (Does.Not.Exist)",
            Id = "template-test/does-not-exist",
            TemplateName = "template-test",
            ContainerNamespace = "Does.Not.Exist",
            Layers = new List<string> { "Does.Not.Exist" },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo("empty layer namespace"));
    }

    [Test]
    public void CheckLayerContract_MissingOptionalLayer_Silent()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "template-test (Does.Not.Exist)",
            Id = "template-test/does-not-exist",
            TemplateName = "template-test",
            ContainerNamespace = "Does.Not.Exist",
            Layers = new List<string> { "Does.Not.Exist" },
            OptionalLayers = new HashSet<string> { "Does.Not.Exist" }
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckLayerContract_TemplateExpandedContract_DetectsDirectionViolation()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        // Layers ordered outermost to innermost:
        //   [0] ArchLinterNet.Core          — outermost (higher-level)
        //   [1] ArchLinterNet.Core.Contracts — innermost (lower-level)
        // Core types reference Contract types → violation (was at index 1 referencing index 0)
        var contract = new ArchitectureLayerContract
        {
            Name = "layering-test",
            TemplateName = "layering-test",
            Layers = new List<string>
            {
                "ArchLinterNet.Core",
                "ArchLinterNet.Core.Contracts"
            }
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        // Contracts (index 1) must not reference Core (index 0)
        Assert.That(violations, Is.Not.Empty);
    }

    [Test]
    public void CheckLayerContract_OptionalLayerPresent_EnforcesDirection()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        // Execution references Contracts → violation when Contracts is at lower index
        var contract = new ArchitectureLayerContract
        {
            Name = "layering-test",
            TemplateName = "layering-test",
            Layers = new List<string>
            {
                "ArchLinterNet.Core",
                "ArchLinterNet.Core.Contracts",
                "ArchLinterNet.Core.Execution"
            },
            OptionalLayers = new HashSet<string> { "ArchLinterNet.Core.Contracts" }
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        // Even though Contracts is optional, it IS present, so direction must be enforced.
        // Execution (index 2) references Contracts (index 1) → violation
        Assert.That(violations, Is.Not.Empty);
    }

    [Test]
    public void CheckLayerContract_TemplateMetadata_PropagatedToViolations()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "feature-clean-architecture (MyApp.Features.Fishing)",
            Id = "fca/myapp-features-fishing",
            TemplateName = "feature-clean-architecture",
            ContainerNamespace = "MyApp.Features.Fishing",
            Layers = new List<string>
            {
                "ArchLinterNet.Core",
                "ArchLinterNet.Core.Contracts"
            }
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That((violations[0].Payload as ConfigurationPayload)?.TemplateName, Is.EqualTo("feature-clean-architecture"));
        Assert.That((violations[0].Payload as ConfigurationPayload)?.ContainerNamespace, Is.EqualTo("MyApp.Features.Fishing"));
    }

    [Test]
    public void CheckLayerContract_RequiredLayerMissingWithMetadata_IncludesMetadata()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "fca (MyApp.Features.Fishing)",
            TemplateName = "fca",
            ContainerNamespace = "MyApp.Features.Fishing",
            Layers = new List<string> { "MyApp.Features.Fishing.Presentation", "MyApp.Features.Fishing.Domain" },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.Any(v => (v.Payload as ConfigurationPayload)?.TemplateName == "fca"), Is.True);
        Assert.That(violations.Any(v => (v.Payload as ConfigurationPayload)?.ContainerNamespace == "MyApp.Features.Fishing"), Is.True);
    }

    [Test]
    public void Validator_WithTemplateContracts_ExpandsAndValidates()
    {
        // Integration test through the validator — template layers are all optional
        // and point to absent namespaces, so no violations from the template.
        string dir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            string archDir = Path.Combine(dir, "architecture");
            Directory.CreateDirectory(archDir);
            string policyPath = Path.Combine(archDir, "dependencies.arch.yml");

            string yaml = $@"
version: 1
name: Template Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - {CoreAssemblyName}
contracts:
  strict_layer_templates:
    - name: test-template
      containers:
        - Does.Not.Exist
      layers:
        - name: LayerA
          optional: true
        - name: LayerB
          optional: true
      reason: All-optional template with absent layers — no violations expected
  strict_layers:
    - name: direct-layers
      layers: [core]
      reason: Direct contract still works
";
            File.WriteAllText(policyPath, yaml);

            bool result = ArchitectureValidator.Validate(policyPath, out _, out _);
            Assert.That(result, Is.True);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Test]
    public void Validator_TemplateContractDirectionViolation_Fails()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            string archDir = Path.Combine(dir, "architecture");
            Directory.CreateDirectory(archDir);
            string policyPath = Path.Combine(archDir, "dependencies.arch.yml");

            string yaml = $@"
version: 1
name: Template Violation Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - {CoreAssemblyName}
contracts:
  strict_layer_templates:
    - name: reversed-layers
      containers:
        - ArchLinterNet
      layers:
        - name: Core.Execution
        - name: Core
      reason: This should violate because types in Core reference types in Core.Execution
";
            File.WriteAllText(policyPath, yaml);

            bool result = ArchitectureValidator.Validate(policyPath, out _, out _);
            Assert.That(result, Is.False);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Test]
    public void CheckLayerContract_Exhaustive_AllChildrenMapped_NoViolation()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "exhaustive-test (ArchLinterNet.Core)",
            Id = "exhaustive-test/archlinternet-core",
            TemplateName = "exhaustive-test",
            ContainerNamespace = "ArchLinterNet.Core",
            Exhaustive = true,
            Layers = new List<string>
            {
                "ArchLinterNet.Core.Asmdef",
                "ArchLinterNet.Core.BuildState",
                "ArchLinterNet.Core.Composition",
                "ArchLinterNet.Core.Contracts",
                "ArchLinterNet.Core.Discovery",
                "ArchLinterNet.Core.Execution",
                "ArchLinterNet.Core.Graph",
                "ArchLinterNet.Core.IO",
                "ArchLinterNet.Core.Model",
                "ArchLinterNet.Core.Reporting",
                "ArchLinterNet.Core.Resolution",
                "ArchLinterNet.Core.Scanning",
                "ArchLinterNet.Core.Validation"
            },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        Assert.That(violations.Where(v => v.ForbiddenNamespace == "unmapped sibling namespace"), Is.Empty);
    }

    [Test]
    public void CheckLayerContract_Exhaustive_UnmappedSibling_ProducesViolation()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "exhaustive-test (ArchLinterNet.Core)",
            Id = "exhaustive-test/archlinternet-core",
            TemplateName = "exhaustive-test",
            ContainerNamespace = "ArchLinterNet.Core",
            Exhaustive = true,
            Layers = new List<string>
            {
                "ArchLinterNet.Core.Contracts",
                "ArchLinterNet.Core.Execution"
            },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        var unmapped = violations.Where(v => v.ForbiddenNamespace == "unmapped sibling namespace").ToList();
        Assert.That(unmapped, Is.Not.Empty);
        Assert.That(unmapped.Any(v =>
            v.ForbiddenReferences.Any(r => r.Contains("ArchLinterNet.Core.Model"))), Is.True);
    }

    [Test]
    public void CheckLayerContract_Exhaustive_UnmappedSiblingWithoutTypes_Silent()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "exhaustive-test (Does.Not.Exist)",
            Id = "exhaustive-test/does-not-exist",
            TemplateName = "exhaustive-test",
            ContainerNamespace = "Does.Not.Exist",
            Exhaustive = true,
            Layers = new List<string> { "Does.Not.Exist.Mapped" },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        var unmapped = violations.Where(v => v.ForbiddenNamespace == "unmapped sibling namespace").ToList();
        Assert.That(unmapped, Is.Empty);
    }

    [Test]
    public void Expand_Exhaustive_DottedLayerName_ThrowsArgumentException()
    {
        var template = new ArchitectureLayerTemplateContract
        {
            Name = "test-template",
            Containers = new List<string> { "ArchLinterNet" },
            Layers = new List<ArchitectureTemplateLayer>
            {
                new() { Name = "Core.Execution" }
            },
            Exhaustive = true
        };

        Assert.Throws<ArgumentException>(() =>
            LayerTemplateExpander.Expand(new[] { template }));
    }

    [Test]
    public void Expand_NonExhaustive_DottedLayerName_Succeeds()
    {
        var template = new ArchitectureLayerTemplateContract
        {
            Name = "test-template",
            Containers = new List<string> { "ArchLinterNet" },
            Layers = new List<ArchitectureTemplateLayer>
            {
                new() { Name = "Core.Execution" }
            },
            Exhaustive = false
        };

        List<ArchitectureLayerContract> contracts =
            LayerTemplateExpander.Expand(new[] { template });

        Assert.That(contracts, Has.Count.EqualTo(1));
        Assert.That(contracts[0].Layers, Contains.Item("ArchLinterNet.Core.Execution"));
    }

    [Test]
    public void CheckLayerContract_Exhaustive_MultipleUnmappedSiblings_DeterministicOrder()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "exhaustive-test (ArchLinterNet.Core)",
            Id = "exhaustive-test/archlinternet-core",
            TemplateName = "exhaustive-test",
            ContainerNamespace = "ArchLinterNet.Core",
            Exhaustive = true,
            Layers = new List<string>
            {
                "ArchLinterNet.Core.Contracts"
            },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        var unmapped = violations
            .Where(v => v.ForbiddenNamespace == "unmapped sibling namespace")
            .ToList();

        var actual = unmapped
            .SelectMany(v => v.ForbiddenReferences)
            .ToList();

        var expected = actual
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CheckLayerContract_NonExhaustive_UnmappedSibling_Silent()
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
                TargetAssemblies = new List<string> { CoreAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { CoreAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        var contract = new ArchitectureLayerContract
        {
            Name = "non-exhaustive-test (ArchLinterNet.Core)",
            Id = "non-exhaustive-test/archlinternet-core",
            TemplateName = "non-exhaustive-test",
            ContainerNamespace = "ArchLinterNet.Core",
            Exhaustive = false,
            Layers = new List<string>
            {
                "ArchLinterNet.Core.Contracts",
                "ArchLinterNet.Core.Execution"
            },
            OptionalLayers = new HashSet<string>()
        };

        List<ArchitectureViolation> violations = runner.CheckLayerContract(contract);

        var unmapped = violations.Where(v => v.ForbiddenNamespace == "unmapped sibling namespace").ToList();
        Assert.That(unmapped, Is.Empty);
    }
}
