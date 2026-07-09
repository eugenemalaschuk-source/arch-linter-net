using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AttributeUsageContractTests
{
    private const string TestMarkerAttributeName = "AttributeUsageContractTestFixtures.Markers.TestMarkerAttribute";
    private const string SecondMarkerAttributeName = "AttributeUsageContractTestFixtures.Markers.SecondMarkerAttribute";
    private const string PrefixedNamespace = "AttributeUsageContractTestFixtures.Markers.Prefixed.";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-attribute-usage-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(AttributeUsageContractTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext(ProjectDiscoveryResult? projectDiscovery = null)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(AttributeUsageContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            projectDiscovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitectureAttributeUsageContract contract,
        Dictionary<string, ArchitectureLayer>? layers = null,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditAttributeUsage = new List<ArchitectureAttributeUsageContract> { contract };
        }
        else
        {
            groups.StrictAttributeUsage = new List<ArchitectureAttributeUsageContract> { contract };
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
    public void CheckAttributeUsageContract_AttributeOnlyInAllowedNamespace_ProducesNoViolations()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-allowed-namespace",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string>
            {
                "AttributeUsageContractTestFixtures.Allowed",
                "AttributeUsageContractTestFixtures.Wrong",
                "AttributeUsageContractTestFixtures.Forbidden"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAttributeUsageContract_AttributeOutsideAllowedNamespace_ReturnsMisplacedViolation()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-allowed-namespace-only",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => (v.Payload as AttributeUsagePayload)?.AttributeUsageKind == "misplaced"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder"
            && (v.Payload as AttributeUsagePayload)?.MatchedAttribute == TestMarkerAttributeName), Is.True);
        Assert.That(violations.All(v => v.SourceType.StartsWith("AttributeUsageContractTestFixtures.Allowed", StringComparison.Ordinal) == false), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_AttributeInsideForbiddenNamespace_ReturnsForbiddenViolation()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-forbidden-namespace",
            Attributes = new List<string> { TestMarkerAttributeName },
            ForbiddenInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Forbidden" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => (v.Payload as AttributeUsagePayload)?.AttributeUsageKind == "forbidden"), Is.True);
        Assert.That(violations.All(v =>
            v.SourceType.StartsWith("AttributeUsageContractTestFixtures.Forbidden", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_BothAllowedAndForbidden_FailingBothReportsSingleForbiddenViolation()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-allowed-and-forbidden",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" },
            ForbiddenInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Forbidden" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        var forbiddenHolderViolations = violations
            .Where(v => v.SourceType == "AttributeUsageContractTestFixtures.Forbidden.ForbiddenHolder")
            .ToList();

        Assert.That(forbiddenHolderViolations, Has.Count.EqualTo(1));
        Assert.That((forbiddenHolderViolations[0].Payload as AttributeUsagePayload)?.AttributeUsageKind, Is.EqualTo("forbidden"));
    }

    [Test]
    public void CheckAttributeUsageContract_TypeLevelMatch_IsDetected()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "type-level-match",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v => v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_MethodLevelMatch_IsDetected()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "method-level-match",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.MarkedMethodTarget()"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_ConstructorLevelMatch_IsDetected()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "constructor-level-match",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder..ctor()"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_PropertyLevelMatch_IsDetected()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "property-level-match",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.MarkedProperty"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_PrivateFieldMatch_IsDetectedRegardlessOfVisibility()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "private-field-level-match",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder._markedPrivateField"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_EventLevelMatch_IsDetected()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "event-level-match",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.MarkedEvent"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_OverloadedMethods_ProduceDistinctSourceIdentifiers()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "overloaded-methods",
            Attributes = new List<string> { TestMarkerAttributeName, SecondMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        var parameterlessOverload = violations.Where(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.OverloadedMethod()").ToList();
        var intOverload = violations.Where(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.OverloadedMethod(System.Int32)").ToList();

        Assert.That(parameterlessOverload, Has.Count.EqualTo(1));
        Assert.That((parameterlessOverload[0].Payload as AttributeUsagePayload)?.MatchedAttribute, Is.EqualTo(TestMarkerAttributeName));
        Assert.That(intOverload, Has.Count.EqualTo(1));
        Assert.That((intOverload[0].Payload as AttributeUsagePayload)?.MatchedAttribute, Is.EqualTo(SecondMarkerAttributeName));
    }

    [Test]
    public void CheckAttributeUsageContract_ViolationOrder_IsDeterministicBySourceThenAttribute()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "deterministic-order",
            Attributes = new List<string> { TestMarkerAttributeName, SecondMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runnerOne = new ArchitectureContractRunner(CreateContext(), document);
        var runnerTwo = new ArchitectureContractRunner(CreateContext(), document);

        var violationsOne = runnerOne.Session.CheckAttributeUsageContract(contract);
        var violationsTwo = runnerTwo.Session.CheckAttributeUsageContract(contract);

        string[] orderOne = violationsOne.Select(v => $"{v.SourceType}|{(v.Payload as AttributeUsagePayload)?.MatchedAttribute}").ToArray();
        string[] orderTwo = violationsTwo.Select(v => $"{v.SourceType}|{(v.Payload as AttributeUsagePayload)?.MatchedAttribute}").ToArray();

        Assert.That(orderOne, Is.Not.Empty);
        Assert.That(orderOne, Is.EqualTo(orderTwo));

        string[] sortedByOrdinal = orderOne
            .OrderBy(key => key.Split('|')[0], StringComparer.Ordinal)
            .ThenBy(key => key.Split('|')[1], StringComparer.Ordinal)
            .ToArray();
        Assert.That(orderOne, Is.EqualTo(sortedByOrdinal));
    }

    [Test]
    public void CheckAttributeUsageContract_AttributePrefixes_MatchesByPrefix()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "prefix-match",
            AttributePrefixes = new List<string> { PrefixedNamespace },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.PrefixMatchedField"
            && (v.Payload as AttributeUsagePayload)?.MatchedAttribute?.StartsWith(PrefixedNamespace, StringComparison.Ordinal) == true), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_MemberWithTwoMatchingAttributes_YieldsTwoViolations()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "dual-match",
            Attributes = new List<string> { TestMarkerAttributeName, SecondMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        var dualFieldViolations = violations
            .Where(v => v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder.DualMarkedField")
            .ToList();

        Assert.That(dualFieldViolations, Has.Count.EqualTo(2));
        Assert.That(dualFieldViolations.Select(v => (v.Payload as AttributeUsagePayload)?.MatchedAttribute),
            Is.EquivalentTo(new[] { TestMarkerAttributeName, SecondMarkerAttributeName }));
    }

    [Test]
    public void CheckAttributeUsageContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitectureAttributeUsageContract
        {
            Name = "audit-attribute-usage",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(auditContract, audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictAttributeUsage, Is.Empty);
    }

    [Test]
    public void CheckAttributeUsageContract_IgnoredViolation_SuppressesViolation()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "ignored-attribute-usage",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        contract.IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = "AttributeUsageContractTestFixtures.Wrong.WrongHolder",
            ForbiddenReference = TestMarkerAttributeName,
            Reason = "test ignore"
        });

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder"
            && (v.Payload as AttributeUsagePayload)?.MatchedAttribute == TestMarkerAttributeName), Is.False);
    }

    [Test]
    public void CheckAttributeUsageContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "unmatched-ignore",
            Id = "unmatched-ignore",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInNamespaces = new List<string> { "AttributeUsageContractTestFixtures.Allowed" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "Nonexistent.Attribute",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Nonexistent.Type"), Is.True);
    }

    [Test]
    public void AttributeUsage_NoAttributeSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_attribute_usage:
                - name: no-attributes
                  allowed_only_in_layers: [api]
                  reason: Missing attributes/attribute_prefixes.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no 'attributes' or 'attribute_prefixes'"));
        Assert.That(ex.Message, Does.Contain("no-attributes"));
    }

    [Test]
    public void AttributeUsage_NoLocationExpectation_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_attribute_usage:
                - name: no-location-expectation
                  attributes: [Some.Attribute]
                  reason: Missing allowed_only_in_*/forbidden_in_*.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no-location-expectation"));
        Assert.That(ex.Message, Does.Contain("location expectation"));
    }

    [Test]
    public void CheckAttributeUsageContract_AllowedOnlyInLayers_ResolvesViaDeclaredLayerNamespace()
    {
        var layers = new Dictionary<string, ArchitectureLayer>
        {
            ["allowed-layer"] = new() { Namespace = "AttributeUsageContractTestFixtures.Allowed" },
            ["forbidden-layer"] = new() { Namespace = "AttributeUsageContractTestFixtures.Forbidden" }
        };
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-allowed-layer",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInLayers = new List<string> { "allowed-layer" }
        };
        var document = CreateDocument(contract, layers);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations.Any(v => v.SourceType.StartsWith("AttributeUsageContractTestFixtures.Allowed", StringComparison.Ordinal)), Is.False);
        Assert.That(violations.Any(v => v.SourceType == "AttributeUsageContractTestFixtures.Forbidden.ForbiddenHolder"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_ForbiddenInAssemblies_ReturnsForbiddenViolation()
    {
        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-forbidden-assembly",
            Attributes = new List<string> { TestMarkerAttributeName },
            ForbiddenInAssemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => (v.Payload as AttributeUsagePayload)?.AttributeUsageKind == "forbidden"), Is.True);
    }

    [Test]
    public void CheckAttributeUsageContract_AllowedOnlyInProjects_ResolvesViaProjectDiscoveryToAssemblyName()
    {
        var projectDiscovery = new ProjectDiscoveryResult(
            new[] { AssemblyName },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(
                    Path.Combine("/repo", "MyApp.Tests.csproj"), AssemblyName, new List<string> { "net10.0" })
            }
        };

        var contract = new ArchitectureAttributeUsageContract
        {
            Name = "marker-allowed-project",
            Attributes = new List<string> { TestMarkerAttributeName },
            AllowedOnlyInProjects = new List<string> { "MyApp.Tests" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectDiscovery), document);

        var violations = runner.Session.CheckAttributeUsageContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ValidateStrict_DanglingAllowedOnlyInLayerNotCoveredByRuleInputCoverage_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_attribute_usage:
                - id: marker-dangling-layer
                  name: marker-dangling-layer
                  attributes: [{TestMarkerAttributeName}]
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
              strict_attribute_usage:
                - id: marker-dangling-forbidden-layer
                  name: marker-dangling-forbidden-layer
                  attributes: [{TestMarkerAttributeName}]
                  forbidden_in_layers: [does_not_exist_layer]
                  reason: Placeholder with a dangling forbidden_in_layers entry.
              strict_coverage:
                - id: rule-input-coverage
                  name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [marker-dangling-forbidden-layer]
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

    [Test]
    public void ValidateStrict_AttributeUsageViolation_EndToEndThroughValidationService()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_attribute_usage:
                - id: marker-allowed-namespace-only
                  name: marker-allowed-namespace-only
                  attributes: [{TestMarkerAttributeName}]
                  allowed_only_in_namespaces: [AttributeUsageContractTestFixtures.Allowed]
                  reason: Marker must stay in the allowed namespace.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder"), Is.True);
    }

    [Test]
    public void ValidateAudit_AttributeUsageViolation_ReportsWithoutFailingStrictValidation()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              audit_attribute_usage:
                - id: marker-allowed-namespace-only-audit
                  name: marker-allowed-namespace-only-audit
                  attributes: [{TestMarkerAttributeName}]
                  allowed_only_in_namespaces: [AttributeUsageContractTestFixtures.Allowed]
                  reason: Marker must be discoverable in audit mode without blocking strict.
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
            "An audit_attribute_usage contract must not be evaluated (and therefore cannot fail) under strict mode.");
        Assert.That(strictOutcome.Violations, Is.Empty);

        Assert.That(auditOutcome.Violations.Any(v =>
            v.SourceType == "AttributeUsageContractTestFixtures.Wrong.WrongHolder"), Is.True);
    }
}
