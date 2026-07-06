using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalAllowOnlyContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-external-allow-only-test-{Guid.NewGuid():N}");
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

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument(
        Dictionary<string, ArchitectureExternalDependencyGroup> externalDependencies,
        ArchitectureExternalAllowOnlyContract contract,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditExternalAllowOnly = new List<ArchitectureExternalAllowOnlyContract> { contract };
        }
        else
        {
            groups.StrictExternalAllowOnly = new List<ArchitectureExternalAllowOnlyContract> { contract };
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            ExternalDependencies = externalDependencies,
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = groups
        };
    }

    [Test]
    public void CheckExternalAllowOnlyContract_NoDeclaredGroups_ReturnsNoViolations()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(new Dictionary<string, ArchitectureExternalDependencyGroup>(), contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_ReferenceToAllowedGroup_ReturnsNoViolations()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string> { "system" }
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_ReferenceToNonAllowedDeclaredGroup_ReturnsViolation()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Id = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(violations.All(v => v.ForbiddenExternalGroup == "system"), Is.True);
        Assert.That(
            violations.SelectMany(v => v.ForbiddenReferences).Any(r => r.StartsWith("System", StringComparison.Ordinal)),
            Is.True);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_MultipleNonAllowedGroups_ReturnsViolationPerGroup()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } },
                ["collections"] = new() { NamespacePrefixes = new List<string> { "System.Collections" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        HashSet<string> matchedGroups = violations
            .Select(v => v.ForbiddenExternalGroup!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(matchedGroups, Does.Contain("system"));
        Assert.That(matchedGroups, Does.Contain("collections"));
    }

    [Test]
    public void CheckExternalAllowOnlyContract_AllowedTypesException_SuppressesSpecificReference()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>(),
            AllowedTypes = new List<string> { "System.String" }
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations.SelectMany(v => v.ForbiddenReferences), Does.Not.Contain("System.String"));
    }

    [Test]
    public void CheckExternalAllowOnlyContract_IgnoredViolation_SuppressesViolation()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var baselineViolations = runner.Session.CheckExternalAllowOnlyContract(contract);
        Assert.That(baselineViolations, Is.Not.Empty);

        string sourceType = baselineViolations[0].SourceType;
        string forbiddenReference = baselineViolations[0].ForbiddenReferences.First();

        contract.IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = sourceType,
            ForbiddenReference = forbiddenReference,
            Reason = "test ignore"
        });

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(
            violations.Any(v => v.SourceType == sourceType && v.ForbiddenReferences.Contains(forbiddenReference)),
            Is.False);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Id = "core-allow-only",
            Source = "core",
            Allowed = new List<string>(),
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "System.NeverReferenced",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations, Is.Not.Empty);
        Assert.That(
            runner.UnmatchedIgnoredViolations.Any(u => u.ForbiddenReference == "System.NeverReferenced"),
            Is.True);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_BclReference_NotFlaggedWhenNoMatchingGroupDeclared()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(new Dictionary<string, ArchitectureExternalDependencyGroup>(), contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_BclReference_FlaggedWhenExplicitlyCapturedByDisallowedGroup()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["bcl"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenExternalGroup == "bcl"), Is.True);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_MisspelledAllowedGroupName_HasNoRelaxingEffect()
    {
        var contractWithTypo = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string> { "systen" } // typo of "system"
        };
        var contractCorrect = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };

        var externalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
        {
            ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
        };

        var documentWithTypo = CreateDocument(externalDependencies, contractWithTypo);
        var runnerWithTypo = new ArchitectureContractRunner(CreateContext(), documentWithTypo);
        var violationsWithTypo = runnerWithTypo.Session.CheckExternalAllowOnlyContract(contractWithTypo);

        var documentCorrect = CreateDocument(externalDependencies, contractCorrect);
        var runnerCorrect = new ArchitectureContractRunner(CreateContext(), documentCorrect);
        var violationsCorrect = runnerCorrect.Session.CheckExternalAllowOnlyContract(contractCorrect);

        Assert.That(violationsWithTypo.Any(v => v.ForbiddenExternalGroup == "system"), Is.True);
        Assert.That(violationsWithTypo.Count, Is.EqualTo(violationsCorrect.Count));
    }

    [Test]
    public void CheckExternalAllowOnlyContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-audit-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            auditContract,
            audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictExternalAllowOnly, Is.Empty);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_AllowedTypesException_DoesNotAppearAsBaselineCandidate()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Id = "core-allow-only",
            Source = "core",
            Allowed = new List<string>(),
            AllowedTypes = new List<string> { "System.String" }
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(
            runner.BaselineCandidates.Any(c => c.ForbiddenReference == "System.String"),
            Is.False,
            "A reference excluded via allowed_types is not a violation and should not be suggested as a baseline candidate.");
    }

    [Test]
    public void ValidateStrict_DanglingSourceLayerCoveredByRuleInputCoverage_ReportsUnresolvedWithoutThrowing()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              audio:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_external_allow_only:
                - id: audio-allow-only-typo
                  name: audio-allow-only-typo
                  source: does_not_exist_layer
                  allowed: []
                  reason: Placeholder with a dangling source layer.
              strict_coverage:
                - id: rule-input-coverage
                  name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [audio-allow-only-typo]
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
    public void CheckConfiguration_ExternalAllowOnlyEmptySourceLayer_ReturnsViolation()
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
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternalAllowOnly = new List<ArchitectureExternalAllowOnlyContract>
                {
                    new() { Name = "test", Source = "empty", Allowed = new List<string>() }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "empty layer namespace"), Is.True);
    }

    [Test]
    public void CheckConfiguration_ExternalAllowOnlyImplicitDisallowedGroupWithoutMatchers_ReturnsViolation()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string>()
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["empty_group"] = new()
            },
            contract);

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "invalid external dependency group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_ExternalAllowOnlyAllowedGroupWithoutMatchers_DoesNotReturnViolation()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string> { "empty_group" }
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["empty_group"] = new()
            },
            contract);

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "invalid external dependency group"), Is.False);
    }

    [Test]
    public void CheckExternalAllowOnlyContract_Violation_MessageIncludesAllowedGroups()
    {
        var contract = new ArchitectureExternalAllowOnlyContract
        {
            Name = "core-allow-only",
            Source = "core",
            Allowed = new List<string> { "approved_sdk" }
        };
        var document = CreateDocument(
            new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["approved_sdk"] = new() { NamespacePrefixes = new List<string> { "Does.Not.Exist" } },
                ["system"] = new() { NamespacePrefixes = new List<string> { "System" } }
            },
            contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckExternalAllowOnlyContract(contract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(
            violations.All(v => v.ForbiddenNamespace.Contains("allowed groups: [approved_sdk]", StringComparison.Ordinal)),
            Is.True);
    }
}
