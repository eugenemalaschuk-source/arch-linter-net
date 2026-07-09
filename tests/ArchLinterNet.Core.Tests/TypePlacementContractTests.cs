using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using RoleFixtures = TypePlacementContractTestFixtures.Roles;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class TypePlacementContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-type-placement-test-{Guid.NewGuid():N}");
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

    private static ArchitectureAnalysisContext CreateContext(ProjectDiscoveryResult? projectDiscovery = null)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(TypePlacementContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            projectDiscovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitectureTypePlacementContract contract,
        Dictionary<string, ArchitectureLayer>? layers = null,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditTypePlacement = new List<ArchitectureTypePlacementContract> { contract };
        }
        else
        {
            groups.StrictTypePlacement = new List<ArchitectureTypePlacementContract> { contract };
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = layers ?? new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(TypePlacementContractTests).Assembly.GetName().Name! }
            },
            Contracts = groups
        };
    }

    [Test]
    public void CheckTypePlacementContract_NameSuffixSelector_MatchesOnlyMatchingTypes()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-in-correct",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Correct" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("Wrong.SampleController", StringComparison.Ordinal)), Is.True);
        Assert.That(violations.Any(v => v.SourceType.Contains("Correct.SampleController", StringComparison.Ordinal)), Is.False);
        Assert.That(violations.Any(v => v.SourceType.Contains("SampleService", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckTypePlacementContract_NamespaceSelector_MatchesTypesInNamespace()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "fixtures-must-be-named",
            TypesMatching = new ArchitectureTypeMatcher { Namespace = "TypePlacementContractTestFixtures.Wrong" },
            RequiredNameSuffix = "Controller"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        HashSet<string> violatingTypes = violations.Select(v => v.SourceType).ToHashSet(StringComparer.Ordinal);
        Assert.That(violatingTypes.Any(t => t.EndsWith("SampleHandler", StringComparison.Ordinal)), Is.True);
        Assert.That(violatingTypes.Any(t => t.EndsWith("SampleHandlerImpl", StringComparison.Ordinal)), Is.True);
        Assert.That(violatingTypes.Any(t => t.EndsWith("SampleController", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckTypePlacementContract_LayerSelector_MatchesTypesResolvedToLayer()
    {
        var layers = new Dictionary<string, ArchitectureLayer>
        {
            ["correct"] = new() { Namespace = "TypePlacementContractTestFixtures.Correct" }
        };
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "correct-layer-naming",
            TypesMatching = new ArchitectureTypeMatcher { Layer = "correct" },
            RequiredNameSuffix = "Controller"
        };
        var document = CreateDocument(contract, layers);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => v.SourceType.EndsWith("SampleService", StringComparison.Ordinal)), Is.True);
        Assert.That(violations.Any(v => v.SourceType.EndsWith("SampleController", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckTypePlacementContract_BaseTypeSelector_MatchesDerivedTypesOnly()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "role-derived-naming",
            TypesMatching = new ArchitectureTypeMatcher { BaseType = typeof(RoleFixtures.RoleBase).FullName! },
            RequiredNameSuffix = "DoesNotMatchAnything"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Count, Is.EqualTo(1));
        Assert.That(violations[0].SourceType, Is.EqualTo(typeof(RoleFixtures.RoleDerived).FullName));
    }

    [Test]
    public void CheckTypePlacementContract_InterfaceSelector_MatchesImplementersOnly()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "role-implementer-naming",
            TypesMatching = new ArchitectureTypeMatcher { ImplementsInterface = typeof(RoleFixtures.IRoleMarker).FullName! },
            RequiredNameSuffix = "DoesNotMatchAnything"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Count, Is.EqualTo(1));
        Assert.That(violations[0].SourceType, Is.EqualTo(typeof(RoleFixtures.RoleImplementer).FullName));
    }

    [Test]
    public void CheckTypePlacementContract_AttributeSelector_MatchesMarkedTypesOnly()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "role-marked-naming",
            TypesMatching = new ArchitectureTypeMatcher { HasAttribute = typeof(RoleFixtures.RoleMarkerAttribute).FullName! },
            RequiredNameSuffix = "DoesNotMatchAnything"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Count, Is.EqualTo(1));
        Assert.That(violations[0].SourceType, Is.EqualTo(typeof(RoleFixtures.RoleMarkedType).FullName));
    }

    [Test]
    public void CheckTypePlacementContract_CombinedSelectorFields_CombineWithAnd()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "combined-selector",
            TypesMatching = new ArchitectureTypeMatcher
            {
                NameSuffix = "Controller",
                Namespace = "TypePlacementContractTestFixtures.Wrong"
            },
            RequiredNameSuffix = "DoesNotMatchAnything"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Count, Is.EqualTo(1));
        Assert.That(violations[0].SourceType.EndsWith("Wrong.SampleController", StringComparison.Ordinal), Is.True);
    }

    [Test]
    public void CheckTypePlacementContract_TypeOutsideEveryDeclaredLocation_ReturnsViolation()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-must-be-correct",
            Id = "controllers-must-be-correct",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Correct" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        var violation = violations.Single(v => v.SourceType.Contains("Wrong.SampleController", StringComparison.Ordinal));
        Assert.That((violation.Payload as TypePlacementPayload)?.ExpectedTypeLocation, Does.Contain("TypePlacementContractTestFixtures.Correct"));
        Assert.That((violation.Payload as TypePlacementPayload)?.ActualTypeLocation, Does.Contain("TypePlacementContractTestFixtures.Wrong"));
        Assert.That((violation.Payload as TypePlacementPayload)?.ExpectedTypeName, Is.Null);
    }

    [Test]
    public void CheckTypePlacementContract_TypeInsideDeclaredLayer_Passes()
    {
        var layers = new Dictionary<string, ArchitectureLayer>
        {
            ["correct"] = new() { Namespace = "TypePlacementContractTestFixtures.Correct" }
        };
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-must-be-in-layer",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInLayers = new List<string> { "correct" }
        };
        var document = CreateDocument(contract, layers);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("Correct.SampleController", StringComparison.Ordinal)), Is.False);
        Assert.That(violations.Any(v => v.SourceType.Contains("Wrong.SampleController", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckTypePlacementContract_MustResideInProjects_ResolvesViaProjectDiscoveryToAssemblyName()
    {
        string assemblyName = typeof(TypePlacementContractTests).Assembly.GetName().Name!;
        var projectDiscovery = new ProjectDiscoveryResult(
            new[] { assemblyName },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(
                    Path.Combine("/repo", "MyApp.Tests.csproj"), assemblyName, new List<string> { "net10.0" })
            }
        };

        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-must-be-in-project",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInProjects = new List<string> { "MyApp.Tests" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectDiscovery), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckTypePlacementContract_MissingRequiredSuffix_ReturnsViolation()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "wrong-namespace-naming",
            TypesMatching = new ArchitectureTypeMatcher { Namespace = "TypePlacementContractTestFixtures.Wrong" },
            RequiredNameSuffix = "Controller"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        var violation = violations.Single(v => v.SourceType.EndsWith("SampleHandler", StringComparison.Ordinal));
        Assert.That((violation.Payload as TypePlacementPayload)?.ExpectedTypeName, Does.Contain("required_suffix: Controller"));
        Assert.That((violation.Payload as TypePlacementPayload)?.ActualTypeName, Is.EqualTo("SampleHandler"));
        Assert.That((violation.Payload as TypePlacementPayload)?.ExpectedTypeLocation, Is.Null);
    }

    [Test]
    public void CheckTypePlacementContract_ForbiddenSuffixPresent_ReturnsViolation()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "no-impl-suffix",
            TypesMatching = new ArchitectureTypeMatcher { Namespace = "TypePlacementContractTestFixtures.Wrong" },
            ForbiddenNameSuffix = "Impl"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => (v.Payload as TypePlacementPayload)?.ActualTypeName == "SampleHandlerImpl"), Is.True);
        Assert.That(violations.Any(v => (v.Payload as TypePlacementPayload)?.ActualTypeName == "SampleHandler"), Is.False);
    }

    [Test]
    public void CheckTypePlacementContract_SatisfyingNaming_Passes()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "correct-naming",
            TypesMatching = new ArchitectureTypeMatcher { Namespace = "TypePlacementContractTestFixtures.Correct" },
            RequiredNameSuffix = "Controller"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => v.SourceType.EndsWith("SampleController", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckTypePlacementContract_TypeFailingBothPlacementAndNaming_ReturnsSingleViolationWithBoth()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-strict",
            TypesMatching = new ArchitectureTypeMatcher { Namespace = "TypePlacementContractTestFixtures.Wrong" },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Correct" },
            RequiredNameSuffix = "Controller"
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        var violation = violations.Single(v => v.SourceType.EndsWith("SampleHandler", StringComparison.Ordinal));
        Assert.That((violation.Payload as TypePlacementPayload)?.ExpectedTypeLocation, Is.Not.Null);
        Assert.That((violation.Payload as TypePlacementPayload)?.ActualTypeLocation, Is.Not.Null);
        Assert.That((violation.Payload as TypePlacementPayload)?.ExpectedTypeName, Is.Not.Null);
        Assert.That((violation.Payload as TypePlacementPayload)?.ActualTypeName, Is.Not.Null);
    }

    [Test]
    public void CheckTypePlacementContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitectureTypePlacementContract
        {
            Name = "audit-controllers",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Correct" }
        };
        var document = CreateDocument(auditContract, audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictTypePlacement, Is.Empty);
    }

    [Test]
    public void CheckTypePlacementContract_IgnoredViolation_SuppressesViolation()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-must-be-correct",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Correct" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var baseline = runner.Session.CheckTypePlacementContract(contract);
        Assert.That(baseline, Is.Not.Empty);

        string sourceType = baseline[0].SourceType;
        string forbiddenReference = baseline[0].ForbiddenReferences.First();

        contract.IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = sourceType,
            ForbiddenReference = forbiddenReference,
            Reason = "test ignore"
        });

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => v.SourceType == sourceType), Is.False);
    }

    [Test]
    public void CheckTypePlacementContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "controllers-must-be-correct",
            Id = "controllers-must-be-correct",
            TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Correct" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "namespace:Nonexistent (assembly Nonexistent)",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckTypePlacementContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations, Is.Not.Empty);
        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Nonexistent.Type"), Is.True);
    }

    [Test]
    public void TypePlacement_SelectorWithNoExpectation_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement:
                - name: controllers-no-expectation
                  types_matching:
                    name_suffix: Controller
                  reason: Missing placement/naming expectation.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no placement"));
        Assert.That(ex.Message, Does.Contain("controllers-no-expectation"));
    }

    [Test]
    public void TypePlacement_EmptyTypesMatching_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement:
                - name: controllers-empty-selector
                  types_matching: {}
                  required_name_suffix: Controller
                  reason: Empty selector would match every loaded type.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no usable types_matching selector field"));
        Assert.That(ex.Message, Does.Contain("controllers-empty-selector"));
    }

    [Test]
    public void TypePlacement_TypoedSelectorFieldName_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement:
                - name: controllers-typoed-selector
                  types_matching:
                    name_sufix: Controller
                  required_name_suffix: Controller
                  reason: Typo'd selector field name is silently ignored by the YAML deserializer.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no usable types_matching selector field"));
        Assert.That(ex.Message, Does.Contain("controllers-typoed-selector"));
    }

    [Test]
    public void TypePlacement_OmittedTypesMatching_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement:
                - name: controllers-omitted-selector
                  required_name_suffix: Controller
                  reason: Omitted types_matching defaults to an empty matcher.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no usable types_matching selector field"));
        Assert.That(ex.Message, Does.Contain("controllers-omitted-selector"));
    }

    [Test]
    public void ValidateStrict_DanglingMustResideInLayerCoveredByRuleInputCoverage_ReportsUnresolvedWithoutThrowing()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{typeof(TypePlacementContractTests).Assembly.GetName().Name}]

            contracts:
              strict_type_placement:
                - id: controllers-dangling-layer
                  name: controllers-dangling-layer
                  types_matching:
                    name_suffix: Controller
                  must_reside_in_layers: [does_not_exist_layer]
                  reason: Placeholder with a dangling must_reside_in_layers entry.
              strict_coverage:
                - id: rule-input-coverage
                  name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [controllers-dangling-layer]
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
    public void ValidateStrict_DanglingMustResideInLayerNotCoveredByRuleInputCoverage_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{typeof(TypePlacementContractTests).Assembly.GetName().Name}]

            contracts:
              strict_type_placement:
                - id: controllers-dangling-layer
                  name: controllers-dangling-layer
                  types_matching:
                    name_suffix: Controller
                  must_reside_in_layers: [does_not_exist_layer]
                  reason: Placeholder with a dangling must_reside_in_layers entry and no coverage deferral.
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
    public void CheckConfiguration_TypePlacementEmptyMustResideInLayer_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["empty"] = new() { Namespace = "Test.Empty.Namespace.That.Has.No.Types" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(TypePlacementContractTests).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictTypePlacement = new List<ArchitectureTypePlacementContract>
                {
                    new()
                    {
                        Name = "test",
                        TypesMatching = new ArchitectureTypeMatcher { NameSuffix = "Controller" },
                        MustResideInLayers = new List<string> { "empty" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.True);
    }
}
