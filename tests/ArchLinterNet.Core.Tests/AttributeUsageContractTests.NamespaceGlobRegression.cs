using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for issue #443 (allowed_only_in_namespaces/forbidden_in_namespaces
// glob-grammar semantics): glob-pattern matching, invalid-pattern/blank-entry rejection at load
// time, and composed (imported-fragment) policy paths. Split out of AttributeUsageContractTests.cs
// to stay under the file-size lint gate.
public sealed partial class AttributeUsageContractTests
{
    [Test]
    public void CheckAttributeUsageContract_AllowedOnlyInNamespacesGlobPattern_MatchesMiddleSegment()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-allowed-namespace-glob",
            Attributes = new List<string> { ModuleMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Modules.*.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Modules.Orders.Allowed.OrdersAllowedHolder"), Is.False);
        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Modules.Orders.Other.OrdersOtherHolder"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_ForbiddenInNamespacesGlobPattern_MatchesMiddleSegment()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-forbidden-namespace-glob",
            Attributes = new List<string> { ModuleMarkerAttributeName },
            ForbiddenInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Modules.*.Forbidden" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Modules.Orders.Forbidden.OrdersForbiddenHolder"
            && (v.Payload as AttributeUsagePayload)?.AttributeUsageKind == "forbidden"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Modules.Orders.Other.OrdersOtherHolder"), Is.False);
    }

    [Test]
    public void AttributeUsage_AllowedOnlyInNamespacesLeadingWildcard_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_attribute_usage:
                - name: invalid-namespace-glob
                  attributes: [{TestMarkerAttributeName}]
                  allowed_only_in_namespaces: ["*.Allowed"]
                  reason: Leading wildcard is unsupported.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("invalid-namespace-glob"));
        Assert.That(ex.Message, Does.Contain("allowed_only_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("Leading wildcard"));
    }

    [Test]
    public void AttributeUsage_ForbiddenInNamespacesBareWildcard_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_attribute_usage:
                - name: invalid-forbidden-namespace-glob
                  attributes: [{TestMarkerAttributeName}]
                  forbidden_in_namespaces: ["*"]
                  reason: Bare wildcard is unsupported.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("invalid-forbidden-namespace-glob"));
        Assert.That(ex.Message, Does.Contain("forbidden_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("Bare wildcard"));
    }

    [Test]
    public void AttributeUsage_ForbiddenInNamespacesBlankEntry_ThrowsActionableErrorInsteadOfSilentNoMatch()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_attribute_usage:
                - name: blank-forbidden-namespace-entry
                  attributes: [{TestMarkerAttributeName}]
                  forbidden_in_namespaces: ["AttributeUsageContractTestFixtures.Forbidden", ""]
                  reason: A blank entry must fail load, not silently no-match.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("blank-forbidden-namespace-entry"));
        Assert.That(ex.Message, Does.Contain("forbidden_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("blank"));
    }

    [Test]
    public void AttributeUsage_ComposedPolicy_InvalidNamespaceGlobInImportedFragment_ThrowsActionableErrorIdentifyingFragment()
    {
        string root = Path.Combine(_tempDir, "root.yml");
        File.WriteAllText(root, $$"""
            version: 1
            name: Test
            imports:
              - fragment.yml
            layers: {}
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_attribute_usage: []
            """);
        File.WriteAllText(Path.Combine(_tempDir, "fragment.yml"), $$"""
            contracts:
              strict_attribute_usage:
                - name: composed-invalid-glob
                  attributes: [{{TestMarkerAttributeName}}]
                  allowed_only_in_namespaces: ["*.Allowed"]
                  reason: Composed (imported) policy path regression for issue #443.
            """);

        // Composed (imported) policies wrap the InvalidOperationException in
        // ArchitecturePolicyValidationException - Assert.Catch matches by assignability, unlike
        // Assert.Throws's exact-type check (see ExpressionCompilationValidatorLocationRegressionTests).
        InvalidOperationException ex = Assert.Catch<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("composed-invalid-glob"));
            Assert.That(ex.Message, Does.Contain("Leading wildcard"));
            Assert.That(ex.Message, Does.Contain("fragment.yml"));
        });
    }
}
