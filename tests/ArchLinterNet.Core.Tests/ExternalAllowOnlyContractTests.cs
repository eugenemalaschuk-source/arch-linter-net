using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalAllowOnlyContractTests
{
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
}
