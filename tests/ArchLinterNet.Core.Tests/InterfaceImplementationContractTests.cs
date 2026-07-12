using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class InterfaceImplementationContractTests
{
    private const string PaymentPortName = "InterfaceImplementationContractTestFixtures.Ports.IPaymentPort";
    private const string GenericPortName = "InterfaceImplementationContractTestFixtures.Ports.IGenericPort`1";
    private const string PrefixedPortsNamespace = "InterfaceImplementationContractTestFixtures.Ports.Prefixed.";
    private const string AdaptersNamespace = "InterfaceImplementationContractTestFixtures.Adapters";
    private const string DomainNamespace = "InterfaceImplementationContractTestFixtures.Domain";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-interface-impl-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(InterfaceImplementationContractTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(InterfaceImplementationContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            null);
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitectureInterfaceImplementationContract contract,
        Dictionary<string, ArchitectureLayer>? layers = null,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditInterfaceImplementation = new List<ArchitectureInterfaceImplementationContract> { contract };
        }
        else
        {
            groups.StrictInterfaceImplementation = new List<ArchitectureInterfaceImplementationContract> { contract };
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
    public void CheckInterfaceImplementationContract_ImplementationInAllowedNamespace_ProducesNoViolation()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "port-implemented-only-by-adapters",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace, DomainNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckInterfaceImplementationContract_ImplementationOutsideAllowedNamespace_ReturnsMisplacedViolation()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "port-implemented-only-by-adapters",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => (v.Payload as InterfaceImplementationPayload)?.ImplementationKind == "misplaced"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation"
            && (v.Payload as InterfaceImplementationPayload)?.MatchedInterface == PaymentPortName), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType.StartsWith(AdaptersNamespace, StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckInterfaceImplementationContract_ImplementationInForbiddenNamespace_ReturnsForbiddenViolation()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "domain-must-not-implement-ports",
            Interfaces = new List<string> { PaymentPortName },
            ForbiddenInNamespaces = new List<string> { DomainNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => (v.Payload as InterfaceImplementationPayload)?.ImplementationKind == "forbidden"), Is.True);
        Assert.That(violations.All(v =>
            v.SourceType.StartsWith(DomainNamespace, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckInterfaceImplementationContract_FailingBothAllowAndDenyLists_ReportsSingleForbiddenViolation()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "port-allowed-and-forbidden",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace },
            ForbiddenInNamespaces = new List<string> { DomainNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        var domainViolations = violations
            .Where(v => v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation")
            .ToList();

        Assert.That(domainViolations, Has.Count.EqualTo(1));
        Assert.That((domainViolations[0].Payload as InterfaceImplementationPayload)?.ImplementationKind, Is.EqualTo("forbidden"));
    }

    [Test]
    public void CheckInterfaceImplementationContract_InheritedInterfaceImplementation_IsMatched()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "port-implemented-only-by-adapters",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.InheritedImplementation"
            && (v.Payload as InterfaceImplementationPayload)?.MatchedInterface == PaymentPortName), Is.True);
    }

    [Test]
    public void CheckInterfaceImplementationContract_GenericInterface_IsMatchedByGenericTypeDefinitionName()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "generic-port-implemented-only-by-adapters",
            Interfaces = new List<string> { GenericPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.GenericPortImplementation"
            && (v.Payload as InterfaceImplementationPayload)?.MatchedInterface == GenericPortName), Is.True);
    }

    [Test]
    public void CheckInterfaceImplementationContract_InterfacePrefixes_MatchesByPrefix()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "prefixed-ports-implemented-only-by-adapters",
            InterfacePrefixes = new List<string> { PrefixedPortsNamespace },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.PrefixedPortImplementation"
            && (v.Payload as InterfaceImplementationPayload)?.MatchedInterface?.StartsWith(PrefixedPortsNamespace, StringComparison.Ordinal) == true), Is.True);
    }

    [Test]
    public void CheckInterfaceImplementationContract_InterfaceExtendingSelectedInterface_IsNotReported()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "port-implemented-only-by-adapters",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Ports.IExtendedPort"), Is.False);
    }

    [Test]
    public void CheckInterfaceImplementationContract_AllowedOnlyInLayers_ResolvesViaDeclaredLayerNamespace()
    {
        var layers = new Dictionary<string, ArchitectureLayer>
        {
            ["adapters"] = new() { Namespace = AdaptersNamespace }
        };
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "port-implemented-only-in-adapters-layer",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInLayers = new List<string> { "adapters" }
        };
        var document = CreateDocument(contract, layers);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.StartsWith(AdaptersNamespace, StringComparison.Ordinal)), Is.False);
        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation"), Is.True);
    }

    [Test]
    public void CheckInterfaceImplementationContract_ViolationOrder_IsDeterministicBySourceThenInterface()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "deterministic-order",
            Interfaces = new List<string> { PaymentPortName, GenericPortName },
            InterfacePrefixes = new List<string> { PrefixedPortsNamespace },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(contract);
        var runnerOne = new ArchitectureContractRunner(CreateContext(), document);
        var runnerTwo = new ArchitectureContractRunner(CreateContext(), document);

        var violationsOne = runnerOne.Session.CheckInterfaceImplementationContract(contract);
        var violationsTwo = runnerTwo.Session.CheckInterfaceImplementationContract(contract);

        string[] orderOne = violationsOne.Select(v => $"{v.SourceType}|{(v.Payload as InterfaceImplementationPayload)?.MatchedInterface}").ToArray();
        string[] orderTwo = violationsTwo.Select(v => $"{v.SourceType}|{(v.Payload as InterfaceImplementationPayload)?.MatchedInterface}").ToArray();

        Assert.That(orderOne, Is.Not.Empty);
        Assert.That(orderOne, Is.EqualTo(orderTwo));
        Assert.That(orderOne, Is.EqualTo(orderOne.Distinct().ToArray()),
            "At most one violation per (type, matched interface) pair.");

        string[] sortedByOrdinal = orderOne
            .OrderBy(key => key.Split('|')[0], StringComparer.Ordinal)
            .ThenBy(key => key.Split('|')[1], StringComparer.Ordinal)
            .ToArray();
        Assert.That(orderOne, Is.EqualTo(sortedByOrdinal));
    }

    [Test]
    public void CheckInterfaceImplementationContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitectureInterfaceImplementationContract
        {
            Name = "audit-interface-implementation",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace }
        };
        var document = CreateDocument(auditContract, audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictInterfaceImplementation, Is.Empty);
    }

    [Test]
    public void CheckInterfaceImplementationContract_IgnoredViolation_SuppressesViolation()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "ignored-interface-implementation",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation",
                    ForbiddenReference = PaymentPortName,
                    Reason = "test ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation"), Is.False);
        Assert.That(violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.InheritedImplementation"), Is.True);
    }

    [Test]
    public void CheckInterfaceImplementationContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitectureInterfaceImplementationContract
        {
            Name = "unmatched-ignore",
            Id = "unmatched-ignore",
            Interfaces = new List<string> { PaymentPortName },
            AllowedOnlyInNamespaces = new List<string> { AdaptersNamespace },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "Nonexistent.Interface",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckInterfaceImplementationContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Nonexistent.Type"), Is.True);
    }

    [Test]
    public void InterfaceImplementation_NoInterfaceSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_interface_implementation:
                - name: no-interfaces
                  allowed_only_in_layers: [infrastructure]
                  reason: Missing interfaces/interface_prefixes.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no 'interfaces' or 'interface_prefixes'"));
        Assert.That(ex.Message, Does.Contain("no-interfaces"));
    }

    [Test]
    public void InterfaceImplementation_NoLocationExpectation_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_interface_implementation:
                - name: no-location-expectation
                  interfaces: [Some.IPort]
                  reason: Missing allowed_only_in_*/forbidden_in_*.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no-location-expectation"));
        Assert.That(ex.Message, Does.Contain("location expectation"));
    }

    [Test]
    public void ValidateStrict_InterfaceImplementationViolation_EndToEndThroughValidationService()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_interface_implementation:
                - id: ports-implemented-only-by-adapters
                  name: ports-implemented-only-by-adapters
                  interfaces: [{PaymentPortName}]
                  allowed_only_in_namespaces: [{AdaptersNamespace}]
                  reason: Application ports may be implemented only by adapters.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation"
            && (v.Payload as InterfaceImplementationPayload)?.MatchedInterface == PaymentPortName), Is.True);
    }

    [Test]
    public void ValidateAudit_InterfaceImplementationViolation_ReportsWithoutFailingStrictValidation()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              audit_interface_implementation:
                - id: ports-implemented-only-by-adapters-audit
                  name: ports-implemented-only-by-adapters-audit
                  interfaces: [{PaymentPortName}]
                  allowed_only_in_namespaces: [{AdaptersNamespace}]
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
            "An audit_interface_implementation contract must not be evaluated (and therefore cannot fail) under strict mode.");
        Assert.That(strictOutcome.Violations, Is.Empty);

        Assert.That(auditOutcome.Violations.Any(v =>
            v.SourceType == "InterfaceImplementationContractTestFixtures.Domain.DomainPaymentImplementation"), Is.True);
    }

    [Test]
    public void ValidateStrict_DanglingAllowedOnlyInLayerNotCoveredByRuleInputCoverage_ThrowsActionableError()
    {
        // Interface_implementation counterpart of the equivalent type_placement/attribute_usage
        // tests: proves GetInterfaceImplementationReferencedLayerNames (promoted to internal static
        // for #212) is still reached through this family's new ConfigurationContributor, so a
        // dangling allowed_only_in_layers entry with no rule_input coverage still throws instead of
        // silently passing.
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_interface_implementation:
                - id: port-dangling-layer
                  name: port-dangling-layer
                  interfaces: [{PaymentPortName}]
                  allowed_only_in_layers: [does_not_exist_layer]
                  reason: Placeholder with a dangling allowed_only_in_layers entry and no coverage deferral.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("unknown layer 'does_not_exist_layer'"));
    }

    [Test]
    public void ValidateStrict_DanglingForbiddenInLayerCoveredByRuleInputCoverage_ReportsUnresolvedWithoutThrowing()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_interface_implementation:
                - id: port-dangling-forbidden-layer
                  name: port-dangling-forbidden-layer
                  interfaces: [{PaymentPortName}]
                  forbidden_in_layers: [does_not_exist_layer]
                  reason: Placeholder with a dangling forbidden_in_layers entry.
              strict_coverage:
                - id: rule-input-coverage
                  name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [port-dangling-forbidden-layer]
                  reason: Flag dangling layer references.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("unresolved"));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenReferences, Is.EqualTo(new[] { "does_not_exist_layer" }));
    }
}
