using ArchLinterNet.Core;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalDependencyIlScannerTests
{
    [Test]
    public void FindMethodBodyViolations_MethodCallToForbiddenGroup_DetectsViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithMethodCall)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            "test-id",
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations[0].SourceType,
            Does.Contain("CoreTypeWithMethodCall"));
        Assert.That(violations[0].ForbiddenReferences.Any(r => r.Contains("VendorSdk")),
            Is.True);
    }

    [Test]
    public void FindMethodBodyViolations_ConstructorCallToForbiddenGroup_DetectsViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithConstructorCall)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations[0].SourceType,
            Does.Contain("CoreTypeWithConstructorCall"));
    }

    [Test]
    public void FindMethodBodyViolations_PropertyAccessToForbiddenGroup_DetectsViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithPropertyAccess)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations[0].SourceType,
            Does.Contain("CoreTypeWithPropertyAccess"));
    }

    [Test]
    public void FindMethodBodyViolations_AdapterLayerWithMethodBodyReference_DetectsViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Adapters.AdapterUsingVendorSdk)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations[0].SourceType, Does.Contain("AdapterUsingVendorSdk"));
    }

    [Test]
    public void FindMethodBodyViolations_NoForbiddenReferences_ReturnsNoViolations()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Core.PureCoreType)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void FindMethodBodyViolations_GenericOnlyInsideBody_DetectsViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithGenericOnlyInBody)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations[0].SourceType,
            Does.Contain("CoreTypeWithGenericOnlyInBody"));
        Assert.That(violations[0].ForbiddenReferences.Any(r =>
            r.Contains("ExternalDependencyContractTestsFixtures.VendorSdk.Client")),
            Is.True);
    }

    [Test]
    public void FindMethodBodyViolations_UnityStyleViolation_DetectsViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "UnityEngine" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.UnityStyle.CoreTypeWithUnityMethodBody)
        };

        var violations = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "unity_runtime",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations[0].ForbiddenReferences.Any(r => r.Contains("UnityEngine")),
            Is.True);
    }

    [Test]
    public void FindMethodBodyViolations_IgnoredViolation_SuppressesViolation()
    {
        var group = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };

        Type[] sourceTypes = new[]
        {
            typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithMethodCall)
        };

        var violationsWithoutIgnore = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            Array.Empty<ArchitectureIgnoredViolation>()).ToList();

        Assert.That(violationsWithoutIgnore, Is.Not.Empty);

        string sourceType = violationsWithoutIgnore[0].SourceType;
        string forbiddenReference = violationsWithoutIgnore[0].ForbiddenReferences.First();

        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = sourceType,
                ForbiddenReference = forbiddenReference,
                Reason = "test ignore"
            }
        };

        var violationsWithIgnore = ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
            "test-contract",
            null,
            sourceTypes,
            "vendor_sdk",
            group,
            ignoredViolations).ToList();

        Assert.That(violationsWithIgnore.Any(v =>
            v.SourceType == sourceType &&
            v.ForbiddenReferences.Contains(forbiddenReference)),
            Is.False);
    }

    [Test]
    public void CheckExternalContract_MethodBodyViolation_FailsStrictValidation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ExternalDependencyContractTestsFixtures.Core" }
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
                TargetAssemblies = new List<string> { typeof(ExternalDependencyIlScannerTests).Assembly.GetName().Name! }
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
            new[] { typeof(ExternalDependencyIlScannerTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.Any(v =>
            v.SourceType.Contains("CoreTypeWithMethodCall") ||
            v.SourceType.Contains("CoreTypeWithConstructorCall") ||
            v.SourceType.Contains("CoreTypeWithPropertyAccess") ||
            v.SourceType.Contains("CoreTypeWithGenericReference") ||
            v.SourceType.Contains("CoreTypeWithGenericOnlyInBody")),
            Is.True);
    }

    [Test]
    public void CheckExternalContract_AuditMethodBodyViolation_ReportsWithoutFailingStrict()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string contractDir = Path.Combine(tempDir, "architecture");
            Directory.CreateDirectory(contractDir);
            string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

            File.WriteAllText(contractPath, @"
version: 1
name: Audit Method Body External Test
layers:
  core:
    namespace: ExternalDependencyContractTestsFixtures.Core
external_dependencies:
  vendor_sdk:
    namespace_prefixes:
      - ExternalDependencyContractTestsFixtures.VendorSdk
analysis:
  target_assemblies:
    - ArchLinterNet.Core.Tests
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
  strict_layer_templates: []
  strict_acyclic_siblings: []
  audit_external:
    - name: core-audit-vendor-sdk
      source: core
      forbidden: [vendor_sdk]
");

            var strictResult = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();
            Assert.That(strictResult.Passed, Is.True);

            var auditResult = ArchitectureAssertions.FromPolicy(contractPath).ValidateAudit();
            Assert.That(auditResult.Passed, Is.False);
            Assert.That(auditResult.Violations.Any(v =>
                v.ForbiddenExternalGroup == "vendor_sdk" &&
                v.ForbiddenReferences.Any(r => r.Contains("VendorSdk.Client"))),
                Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
