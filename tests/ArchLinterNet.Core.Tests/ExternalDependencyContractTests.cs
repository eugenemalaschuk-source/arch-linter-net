using ArchLinterNet.Core;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalDependencyContractTests
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

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    [Test]
    public void LoadFromPath_ExternalDependenciesAndContracts_AreParsed()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: External Contract Test
layers:
  core:
    namespace: ArchLinterNet.Core
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes:
      - Stripe.StripeClient
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict_external:
    - name: core-no-unity
      source: core
      forbidden: [unity_runtime]
  audit_external:
    - name: core-audit-unity
      source: core
      forbidden: [unity_runtime]
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);

        Assert.That(document.ExternalDependencies.ContainsKey("unity_runtime"), Is.True);
        Assert.That(document.ExternalDependencies["unity_runtime"].NamespacePrefixes,
            Is.EqualTo(new[] { "UnityEngine" }));
        Assert.That(document.ExternalDependencies["unity_runtime"].TypePrefixes,
            Is.EqualTo(new[] { "Stripe.StripeClient" }));
        Assert.That(document.Contracts.StrictExternal, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.AuditExternal, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.StrictExternal[0].Id, Is.EqualTo("core-no-unity"));
    }

    [Test]
    public void MatchesGroup_NamespaceExactMatch_ReturnsTrue()
    {
        ArchitectureExternalDependencyGroup group = new()
        {
            NamespacePrefixes = new List<string> { "UnityEngine" }
        };

        bool matches = ArchitectureExternalDependencyResolver.MatchesGroup(
            group,
            "UnityEngine.Vector3",
            "UnityEngine");

        Assert.That(matches, Is.True);
    }

    [Test]
    public void MatchesGroup_NamespaceChildMatch_ReturnsTrue()
    {
        ArchitectureExternalDependencyGroup group = new()
        {
            NamespacePrefixes = new List<string> { "UnityEngine" }
        };

        bool matches = ArchitectureExternalDependencyResolver.MatchesGroup(
            group,
            "UnityEngine.Rendering.RenderPipeline",
            "UnityEngine.Rendering");

        Assert.That(matches, Is.True);
    }

    [Test]
    public void MatchesGroup_NamespaceSiblingMatch_ReturnsFalse()
    {
        ArchitectureExternalDependencyGroup group = new()
        {
            NamespacePrefixes = new List<string> { "UnityEngine" }
        };

        bool matches = ArchitectureExternalDependencyResolver.MatchesGroup(
            group,
            "UnityEngineering.Tools.Widget",
            "UnityEngineering.Tools");

        Assert.That(matches, Is.False);
    }

    [Test]
    public void MatchesGroup_TypeExactMatch_ReturnsTrue()
    {
        ArchitectureExternalDependencyGroup group = new()
        {
            TypePrefixes = new List<string> { "Stripe.StripeClient" }
        };

        bool matches = ArchitectureExternalDependencyResolver.MatchesGroup(
            group,
            "Stripe.StripeClient",
            "Stripe");

        Assert.That(matches, Is.True);
    }

    [Test]
    public void MatchesGroup_TypePrefixMatch_ReturnsTrue()
    {
        ArchitectureExternalDependencyGroup group = new()
        {
            TypePrefixes = new List<string> { "Amazon.S3" }
        };

        bool matches = ArchitectureExternalDependencyResolver.MatchesGroup(
            group,
            "Amazon.S3.AmazonS3Client",
            "Amazon.S3");

        Assert.That(matches, Is.True);
    }

    [Test]
    public void MatchesGroup_TypeRawPrefixMatch_ReturnsTrue()
    {
        ArchitectureExternalDependencyGroup group = new()
        {
            TypePrefixes = new List<string> { "Amazon.S3.AmazonS3" }
        };

        bool matches = ArchitectureExternalDependencyResolver.MatchesGroup(
            group,
            "Amazon.S3.AmazonS3Client",
            "Amazon.S3");

        Assert.That(matches, Is.True);
    }

    [Test]
    public void CheckExternalContract_StrictViolationFound_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new()
                {
                    NamespacePrefixes = new List<string> { "System" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-system",
                        Source = "core",
                        Forbidden = new List<string> { "system" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => (v.Payload as ExternalDependencyPayload)?.ForbiddenExternalGroup == "system"), Is.True);
        Assert.That(violations.SelectMany(v => v.ForbiddenReferences).Any(r => r.StartsWith("System", StringComparison.Ordinal)),
            Is.True);
    }

    [Test]
    public void CheckExternalContract_NoMatch_ReturnsNoViolations()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["unity_runtime"] = new()
                {
                    NamespacePrefixes = new List<string> { "UnityEngine" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "contracts-no-unity",
                        Source = "contracts",
                        Forbidden = new List<string> { "unity_runtime" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckExternalContract_AdapterLayerOutsideSourceLayer_NotScanned()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ExternalDependencyContractTestsFixtures.Core" },
                ["adapters"] = new() { Namespace = "ExternalDependencyContractTestsFixtures.Adapters" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["vendor_sdk"] = new()
                {
                    NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ExternalDependencyContractTests).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-vendor-sdk",
                        Source = "core",
                        Forbidden = new List<string> { "vendor_sdk" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ExternalDependencyContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(violations.Any(v => v.SourceType.Contains("AdapterUsingVendorSdk")), Is.False);
    }

    [Test]
    public void CheckExternalContract_IgnoredViolation_SuppressesViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new()
                {
                    NamespacePrefixes = new List<string> { "System" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-system",
                        Source = "core",
                        Forbidden = new List<string> { "system" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var baselineViolations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        string sourceType = baselineViolations[0].SourceType;
        string forbiddenReference = baselineViolations[0].ForbiddenReferences.First();

        document.Contracts.StrictExternal[0].IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = sourceType,
            ForbiddenReference = forbiddenReference,
            Reason = "test ignore"
        });

        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(baselineViolations.SelectMany(v => v.ForbiddenReferences), Does.Contain(forbiddenReference));
        Assert.That(violations.Any(v => v.SourceType == sourceType && v.ForbiddenReferences.Contains(forbiddenReference)),
            Is.False);
    }

    [Test]
    public void CheckConfiguration_UnknownExternalGroup_ReturnsViolation()
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
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-unknown",
                        Source = "core",
                        Forbidden = new List<string> { "unknown_group" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "unknown external dependency group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_AuditMode_UnknownExternalGroup_ReturnsViolation()
    {
        // Audit-mode counterpart of CheckConfiguration_UnknownExternalGroup_ReturnsViolation:
        // proves the single registry-driven contribution loop (introduced in #212) still validates
        // the audit branch identically to strict, now that both modes share the same code path.
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
                AuditExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-unknown-audit",
                        Source = "core",
                        Forbidden = new List<string> { "unknown_group" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration(strict: false);

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "unknown external dependency group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_ExternalGroupWithoutMatchers_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["empty_group"] = new()
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-empty-group",
                        Source = "core",
                        Forbidden = new List<string> { "empty_group" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "invalid external dependency group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_ExternalGroupWithWhitespaceMatchers_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["blank_group"] = new()
                {
                    NamespacePrefixes = new List<string> { "   " },
                    TypePrefixes = new List<string> { "\t" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-blank-group",
                        Source = "core",
                        Forbidden = new List<string> { "blank_group" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "invalid external dependency group"), Is.True);
    }

    [Test]
    public void ValidateStrict_UnknownExternalGroup_ReturnsConfigurationViolationInsteadOfThrowing()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Unknown External Group Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external:
    - name: core-no-missing-group
      source: core
      forbidden: [missing_group]
  audit_external: []
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Violations.Any(v => v.ForbiddenNamespace == "unknown external dependency group"), Is.True);
    }

    [Test]
    public void ValidateStrict_MalformedExternalGroupField_ReturnsConfigurationViolationInsteadOfSilentlyPassing()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Malformed External Group Test
layers:
  core:
    namespace: ArchLinterNet.Core
external_dependencies:
  broken_group:
    namespace_prefix:
      - System
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external:
    - name: core-no-broken-group
      source: core
      forbidden: [broken_group]
  audit_external: []
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Violations.Any(v => v.ForbiddenNamespace == "invalid external dependency group"), Is.True);
    }

    [Test]
    public void ArchitectureValidator_UnknownExternalGroup_ReturnsConfigurationViolationInsteadOfThrowing()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Unknown External Group Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external:
    - name: core-no-missing-group
      source: core
      forbidden: [missing_group]
  audit_external: []
");

        bool passed = ArchitectureValidator.Validate(contractPath, out var violations, out _);

        Assert.That(passed, Is.False);
        Assert.That(violations.Any(v => v.ForbiddenNamespace == "unknown external dependency group"), Is.True);
    }

    [Test]
    public void ValidateStrict_AuditExternalOnlyViolation_DoesNotFailStrictValidation()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Audit External Test
layers:
  core:
    namespace: ArchLinterNet.Core
external_dependencies:
  system:
    namespace_prefixes:
      - System
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external: []
  audit_external:
    - name: core-audit-system
      source: core
      forbidden: [system]
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.True);
        Assert.That(result.Violations, Is.Empty);
    }

    [Test]
    public void ValidateAudit_AuditExternalViolation_IsReported()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Audit External Test
layers:
  core:
    namespace: ArchLinterNet.Core
external_dependencies:
  system:
    namespace_prefixes:
      - System
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external: []
  audit_external:
    - name: core-audit-system
      source: core
      forbidden: [system]
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateAudit();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Violations.Any(v => (v.Payload as ExternalDependencyPayload)?.ForbiddenExternalGroup == "system"), Is.True);
    }

    [Test]
    public void ArchitectureValidator_AuditExternalOnlyViolation_DoesNotFailValidate()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Validator External Test
layers:
  core:
    namespace: ArchLinterNet.Core
external_dependencies:
  system:
    namespace_prefixes:
      - System
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external: []
  audit_external:
    - name: core-audit-system
      source: core
      forbidden: [system]
");

        bool passed = ArchitectureValidator.Validate(contractPath, out var violations, out _);

        Assert.That(passed, Is.True);
        Assert.That(violations, Is.Empty);
    }

}
