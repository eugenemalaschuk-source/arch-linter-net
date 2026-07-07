using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CompositionContractTests
{
    private const string CompositionNamespace = "CompositionContractTestFixtures.Composition";
    private const string ApplicationNamespace = "CompositionContractTestFixtures.Application";
    private const string GetServiceApi = "CompositionContractTestFixtures.Fakes.IFakeServiceProvider.GetService";
    private const string AddSingletonApi = "CompositionContractTestFixtures.Fakes.IFakeServiceCollection.AddSingleton";
    private const string ContainerNamespacePrefix = "CompositionContractTestFixtures.Fakes.";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-composition-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(CompositionContractTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(CompositionContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            null);
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitectureCompositionContract contract,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditComposition = new List<ArchitectureCompositionContract> { contract };
        }
        else
        {
            groups.StrictComposition = new List<ArchitectureCompositionContract> { contract };
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName }
            },
            Contracts = groups
        };
    }

    [Test]
    public void CheckCompositionContract_ForbiddenApiCallInsideBoundary_ProducesNoViolation()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "service-locator-confined-to-composition",
            ForbiddenApis = new List<string> { GetServiceApi, AddSingletonApi },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v => v.SourceType.StartsWith(CompositionNamespace, StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckCompositionContract_ForbiddenApiCallOutsideBoundary_ReturnsViolation()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "service-locator-confined-to-composition",
            ForbiddenApis = new List<string> { GetServiceApi },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ServiceLocatorLeak"
            && v.MatchedForbiddenApi == GetServiceApi), Is.True);
    }

    [Test]
    public void CheckCompositionContract_ContainerStyleResolveRegisterOutsideBoundary_ReturnsViolation()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "container-confined-to-composition",
            ForbiddenApis = new List<string> { "Resolve", "Register" },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ContainerLeak"
            && v.MatchedForbiddenApi == "CompositionContractTestFixtures.Fakes.FakeContainer.Resolve"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ContainerLeak"
            && v.MatchedForbiddenApi == "CompositionContractTestFixtures.Fakes.FakeContainer.Register"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType.StartsWith(CompositionNamespace, StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckCompositionContract_NamespacePrefixPattern_MatchesForbiddenApiUsage()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "namespace-prefix-forbidden",
            ForbiddenApis = new List<string> { ContainerNamespacePrefix },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ServiceLocatorLeak"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.DiRegistrationLeak"), Is.True);
    }

    [Test]
    public void CheckCompositionContract_TypeWithNoForbiddenCalls_ProducesNoViolation()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "no-forbidden-calls",
            ForbiddenApis = new List<string> { GetServiceApi },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.CleanApplicationType"), Is.False);
    }

    [Test]
    public void CheckCompositionContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitectureCompositionContract
        {
            Name = "audit-composition",
            ForbiddenApis = new List<string> { GetServiceApi },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(auditContract, audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictComposition, Is.Empty);
    }

    [Test]
    public void CheckCompositionContract_IgnoredViolation_SuppressesViolation()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "ignored-composition",
            ForbiddenApis = new List<string> { GetServiceApi },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "CompositionContractTestFixtures.Application.ServiceLocatorLeak",
                    ForbiddenReference = GetServiceApi,
                    Reason = "test ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ServiceLocatorLeak"), Is.False);
    }

    [Test]
    public void CheckCompositionContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "unmatched-ignore",
            Id = "unmatched-ignore",
            ForbiddenApis = new List<string> { GetServiceApi },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "Nonexistent.Api",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckCompositionContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Nonexistent.Type"), Is.True);
    }

    [Test]
    public void CheckCompositionContract_ViolationOrder_IsDeterministicBySourceThenMatchedApi()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "deterministic-order",
            ForbiddenApis = new List<string> { GetServiceApi, AddSingletonApi, "Resolve", "Register" },
            AllowedOnlyInNamespaces = new List<string> { CompositionNamespace }
        };
        var document = CreateDocument(contract);
        var runnerOne = new ArchitectureContractRunner(CreateContext(), document);
        var runnerTwo = new ArchitectureContractRunner(CreateContext(), document);

        var violationsOne = runnerOne.Session.CheckCompositionContract(contract);
        var violationsTwo = runnerTwo.Session.CheckCompositionContract(contract);

        string[] orderOne = violationsOne.Select(v => $"{v.SourceType}|{v.MatchedForbiddenApi}").ToArray();
        string[] orderTwo = violationsTwo.Select(v => $"{v.SourceType}|{v.MatchedForbiddenApi}").ToArray();

        Assert.That(orderOne, Is.Not.Empty);
        Assert.That(orderOne, Is.EqualTo(orderTwo));
        Assert.That(orderOne, Is.EqualTo(orderOne.Distinct().ToArray()),
            "At most one violation per (type, matched forbidden API) pair.");

        string[] sortedByOrdinal = orderOne
            .OrderBy(key => key.Split('|')[0], StringComparer.Ordinal)
            .ThenBy(key => key.Split('|')[1], StringComparer.Ordinal)
            .ToArray();
        Assert.That(orderOne, Is.EqualTo(sortedByOrdinal));
    }

    [Test]
    public void Composition_NoForbiddenApisSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_composition:
                - name: no-forbidden-apis
                  allowed_only_in_layers: [composition]
                  reason: Missing forbidden_apis.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no 'forbidden_apis'"));
        Assert.That(ex.Message, Does.Contain("no-forbidden-apis"));
    }

    [Test]
    public void Composition_NoAllowedOnlyBoundary_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_composition:
                - name: no-boundary
                  forbidden_apis: [System.IServiceProvider.GetService]
                  reason: Missing allowed_only_in_*.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no-boundary"));
        Assert.That(ex.Message, Does.Contain("composition boundary"));
    }

    [Test]
    public void ValidateStrict_CompositionViolation_EndToEndThroughValidationService()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_composition:
                - id: service-locator-confined-to-composition
                  name: service-locator-confined-to-composition
                  forbidden_apis: [{GetServiceApi}]
                  allowed_only_in_namespaces: [{CompositionNamespace}]
                  reason: Service-locator usage may only occur in the composition root.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ServiceLocatorLeak"
            && v.MatchedForbiddenApi == GetServiceApi), Is.True);
    }

    [Test]
    public void ValidateAudit_CompositionViolation_ReportsWithoutFailingStrictValidation()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              audit_composition:
                - id: service-locator-confined-to-composition-audit
                  name: service-locator-confined-to-composition-audit
                  forbidden_apis: [{GetServiceApi}]
                  allowed_only_in_namespaces: [{CompositionNamespace}]
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
            "An audit_composition contract must not be evaluated (and therefore cannot fail) under strict mode.");
        Assert.That(strictOutcome.Violations, Is.Empty);

        Assert.That(auditOutcome.Violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ServiceLocatorLeak"), Is.True);
    }
}
