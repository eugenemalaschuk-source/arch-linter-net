using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class InheritanceContractTests
{
    private const string FrameworkBaseName = "InheritanceContractTestFixtures.Framework.FrameworkBase";
    private const string GenericRepositoryName = "InheritanceContractTestFixtures.Framework.GenericRepository`1";
    private const string FrameworkLikeInterfaceName = "InheritanceContractTestFixtures.Framework.IFrameworkLike";
    private const string PrefixedFrameworkNamespace = "InheritanceContractTestFixtures.Framework.Prefixed.";
    private const string DomainNamespace = "InheritanceContractTestFixtures.Domain";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-inheritance-test-{Guid.NewGuid():N}");
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

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string AssemblyName => typeof(InheritanceContractTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(InheritanceContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            null);
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitectureInheritanceContract contract,
        Dictionary<string, ArchitectureLayer>? layers = null,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditInheritance = new List<ArchitectureInheritanceContract> { contract };
        }
        else
        {
            groups.StrictInheritance = new List<ArchitectureInheritanceContract> { contract };
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = layers ?? new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName }
            },
            Contracts = groups
        };
    }

    [Test]
    public void CheckInheritanceContract_DirectInheritance_ReturnsViolation()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-framework-base",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.DirectViolation"
            && v.ForbiddenBaseType == FrameworkBaseName), Is.True);
    }

    [Test]
    public void CheckInheritanceContract_TransitiveInheritance_ReturnsViolation()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-framework-base",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.TransitiveViolation"
            && v.ForbiddenBaseType == FrameworkBaseName), Is.True);
    }

    [Test]
    public void CheckInheritanceContract_GenericBaseType_IsMatchedByGenericTypeDefinitionName()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-generic-repository",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { GenericRepositoryName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.GenericBaseViolation"
            && v.ForbiddenBaseType == GenericRepositoryName), Is.True);
    }

    [Test]
    public void CheckInheritanceContract_BaseTypePrefix_MatchesByPrefix()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-prefixed-framework",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypePrefixes = new List<string> { PrefixedFrameworkNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.PrefixViolation"
            && v.ForbiddenBaseType!.StartsWith(PrefixedFrameworkNamespace, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckInheritanceContract_NestedType_IsChecked()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-framework-base",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.Outer+NestedViolation"
            && v.ForbiddenBaseType == FrameworkBaseName), Is.True);
    }

    [Test]
    public void CheckInheritanceContract_TypeOutsideSourceSurface_IsNotReported()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-framework-base",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Infrastructure.OutsideSourceSurface"), Is.False);
    }

    [Test]
    public void CheckInheritanceContract_InterfaceImplementation_IsNotTreatedAsInheritance()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-framework-like",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkLikeInterfaceName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckInheritanceContract_CleanType_ProducesNoViolation()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-no-framework-base",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.CleanDomainType"), Is.False);
    }

    [Test]
    public void CheckInheritanceContract_SourceLayers_ResolvesViaDeclaredLayerNamespace()
    {
        var layers = new Dictionary<string, ArchitectureLayer>
        {
            ["domain"] = new() { Namespace = DomainNamespace }
        };
        var contract = new ArchitectureInheritanceContract
        {
            Name = "domain-layer-no-framework-base",
            SourceLayers = new List<string> { "domain" },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(contract, layers);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.DirectViolation"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Infrastructure.OutsideSourceSurface"), Is.False);
    }

    [Test]
    public void CheckInheritanceContract_ViolationOrder_IsDeterministicBySourceThenBaseType()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "deterministic-order",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName, GenericRepositoryName },
            ForbiddenBaseTypePrefixes = new List<string> { PrefixedFrameworkNamespace }
        };
        var document = CreateDocument(contract);
        var runnerOne = new ArchitectureContractRunner(CreateContext(), document);
        var runnerTwo = new ArchitectureContractRunner(CreateContext(), document);

        var violationsOne = runnerOne.Session.CheckInheritanceContract(contract);
        var violationsTwo = runnerTwo.Session.CheckInheritanceContract(contract);

        string[] orderOne = violationsOne.Select(v => $"{v.SourceType}|{v.ForbiddenBaseType}").ToArray();
        string[] orderTwo = violationsTwo.Select(v => $"{v.SourceType}|{v.ForbiddenBaseType}").ToArray();

        Assert.That(orderOne, Is.Not.Empty);
        Assert.That(orderOne, Is.EqualTo(orderTwo));
        Assert.That(orderOne, Is.EqualTo(orderOne.Distinct().ToArray()),
            "At most one violation per (type, matched base type) pair.");

        string[] sortedByOrdinal = orderOne
            .OrderBy(key => key.Split('|')[0], StringComparer.Ordinal)
            .ThenBy(key => key.Split('|')[1], StringComparer.Ordinal)
            .ToArray();
        Assert.That(orderOne, Is.EqualTo(sortedByOrdinal));
    }

    [Test]
    public void CheckInheritanceContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitectureInheritanceContract
        {
            Name = "audit-inheritance",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName }
        };
        var document = CreateDocument(auditContract, audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictInheritance, Is.Empty);
    }

    [Test]
    public void CheckInheritanceContract_IgnoredViolation_SuppressesViolation()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "ignored-inheritance",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "InheritanceContractTestFixtures.Domain.DirectViolation",
                    ForbiddenReference = FrameworkBaseName,
                    Reason = "test ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInheritanceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.DirectViolation"), Is.False);
        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.TransitiveViolation"), Is.True);
    }

    [Test]
    public void CheckInheritanceContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitectureInheritanceContract
        {
            Name = "unmatched-ignore",
            Id = "unmatched-ignore",
            SourceNamespaces = new List<string> { DomainNamespace },
            ForbiddenBaseTypes = new List<string> { FrameworkBaseName },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "Nonexistent.Base",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckInheritanceContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Nonexistent.Type"), Is.True);
    }

    [Test]
    public void Inheritance_NoSourceSurface_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_inheritance:
                - name: no-source-surface
                  forbidden_base_types: [Some.Base]
                  reason: Missing source_layers/source_namespaces.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no 'source_layers' or 'source_namespaces'"));
        Assert.That(ex.Message, Does.Contain("no-source-surface"));
    }

    [Test]
    public void Inheritance_NoBaseTypeSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_inheritance:
                - name: no-base-type-selector
                  source_namespaces: [Some.Namespace]
                  reason: Missing forbidden_base_types/forbidden_base_type_prefixes.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no 'forbidden_base_types' or 'forbidden_base_type_prefixes'"));
        Assert.That(ex.Message, Does.Contain("no-base-type-selector"));
    }

    [Test]
    public void ValidateStrict_InheritanceViolation_EndToEndThroughValidationService()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_inheritance:
                - id: domain-no-framework-base-types
                  name: domain-must-not-inherit-framework-types
                  source_namespaces: [{DomainNamespace}]
                  forbidden_base_types: [{FrameworkBaseName}]
                  reason: Domain types must stay framework-independent.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.DirectViolation"
            && v.ForbiddenBaseType == FrameworkBaseName), Is.True);
    }

    [Test]
    public void ValidateAudit_InheritanceViolation_ReportsWithoutFailingStrictValidation()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              audit_inheritance:
                - id: domain-no-framework-base-types-audit
                  name: domain-no-framework-base-types-audit
                  source_namespaces: [{DomainNamespace}]
                  forbidden_base_types: [{FrameworkBaseName}]
                  reason: Discoverable in audit mode without blocking strict.
            """);

        ValidationOutcome strictOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        ValidationOutcome auditOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        });

        Assert.That(strictOutcome.Passed, Is.True,
            "An audit_inheritance contract must not be evaluated (and therefore cannot fail) under strict mode.");
        Assert.That(strictOutcome.Violations, Is.Empty);

        Assert.That(auditOutcome.Violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.DirectViolation"), Is.True);
    }
}
