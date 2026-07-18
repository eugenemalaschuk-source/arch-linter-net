using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class NamespaceViolationFinderGlobTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();
    private static readonly string[] _matchedInternalPrefixes =
        { "ReviewTest.Modules.Billing.Internal", "ReviewTest.Modules.Sales.Internal" };

    [Test]
    public void FindNamespaceViolations_GlobLayer_CollectsConcreteMatchedPrefixesFromForbiddenReferences()
    {
        ArchitectureLayer forbiddenLayer = new() { Namespace = "ReviewTest.Modules.*.Internal" };

        var executionContext = new ArchitectureContractExecutionContext(
            "glob-rule", "glob-rule", Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        var violations = ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                new[] { typeof(ReviewTest.SharedKernel.SharedKernelType) },
                forbiddenLayer,
                Array.Empty<string>(),
                executionContext)
            .ToArray();

        Assert.That(violations, Has.Length.EqualTo(1));
        Assert.That(
            violations[0].MatchedNamespacePrefixes,
            Is.EquivalentTo(_matchedInternalPrefixes));
    }

    [Test]
    public void FormatViolationsForHumans_MultipleConcreteMatches_IncludesAllPrefixes()
    {
        var violation = new ArchLinterNet.Core.Model.ArchitectureViolation(
            "glob-rule",
            "glob-rule",
            "ReviewTest.SharedKernel.SharedKernelType",
            "ReviewTest.Modules.*.Internal",
            new[]
            {
                "ReviewTest.Modules.Sales.Internal.SalesService",
                "ReviewTest.Modules.Billing.Internal.BillingService"
            })
        {
            MatchedNamespacePrefixes = new[]
            {
                "ReviewTest.Modules.Sales.Internal",
                "ReviewTest.Modules.Billing.Internal"
            }
        };

        string output = _formatter.FormatViolationsForHumans(new[] { violation });

        Assert.That(output, Does.Contain("matched ReviewTest.Modules.Billing.Internal, ReviewTest.Modules.Sales.Internal"));
    }

    [Test]
    public void FindNamespaceViolations_SelectorOnlyLayer_MatchesReferencedRole()
    {
        var typeIndex = new ArchitectureTypeIndex(new[] { typeof(SelectorReferenceSource).Assembly });
        var roleIndex = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer"
                    }
                }
            },
            typeIndex);
        var executionContext = new ArchitectureContractExecutionContext(
            "selector-rule", "selector-rule", Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        ArchitectureLayer forbiddenLayer = new()
        {
            Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
        };

        var violations = ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                new[] { typeof(SelectorReferenceSource) },
                forbiddenLayer,
                Array.Empty<string>(),
                executionContext,
                roleIndex: roleIndex,
                expressionFacts: new ArchitectureExpressionFactService(
                    roleIndex,
                    new ArchitectureSourceFileFactIndex(
                        new[] { typeof(SelectorReferenceSource).Assembly }, ".", Array.Empty<string>()),
                    projectDiscovery: null))
            .ToArray();

        Assert.That(violations, Has.Length.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences,
            Does.Contain(typeof(TypeWithBooleanProperty).FullName));
    }

    [Test]
    public void FindNamespaceViolations_GlobLayer_WithRoleIndex_PreservesConcreteMatchedPrefixes()
    {
        var roleIndex = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer"
                    }
                }
            },
            new ArchitectureTypeIndex(new[] { typeof(SelectorReferenceSource).Assembly }));
        ArchitectureLayer forbiddenLayer = new() { Namespace = "ReviewTest.Modules.*.Internal" };

        var executionContext = new ArchitectureContractExecutionContext(
            "glob-rule", "glob-rule", Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        var violations = ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                new[] { typeof(ReviewTest.SharedKernel.SharedKernelType) },
                forbiddenLayer,
                Array.Empty<string>(),
                executionContext,
                roleIndex: roleIndex,
                expressionFacts: new ArchitectureExpressionFactService(
                    roleIndex,
                    new ArchitectureSourceFileFactIndex(
                        new[] { typeof(SelectorReferenceSource).Assembly }, ".", Array.Empty<string>()),
                    projectDiscovery: null))
            .ToArray();

        Assert.That(violations, Has.Length.EqualTo(1));
        Assert.That(
            violations[0].MatchedNamespacePrefixes,
            Is.EquivalentTo(new[]
            {
                "ReviewTest.Modules.Billing.Internal",
                "ReviewTest.Modules.Sales.Internal"
            }));
    }

    [Test]
    public void CheckAllowOnlyContract_SelectorBackedSource_IgnoresExternalClrReferences()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                    }
                }
            },
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["semantic"] = new()
                {
                    Namespace = "AttributeRoleExtractionTestFixtures",
                    Selector = new ArchitectureLayerSelector
                    {
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
                    }
                },
                ["fixtures"] = new()
                {
                    Namespace = "AttributeRoleExtractionTestFixtures"
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core.Tests" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
                {
                    new()
                    {
                        Name = "selector-allow-only",
                        Source = "semantic",
                        Allowed = new List<string> { "fixtures" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(TypeWithConstructorDefault).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckAllowOnlyContract(document.Contracts.StrictAllowOnly[0]);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAllowOnlyContract_DeclaredExternalLayerStillProducesViolationWhenNotAllowed()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["tests"] = new() { Namespace = "ArchLinterNet.Core.Tests" },
                ["execution_external"] = new()
                {
                    Namespace = "ArchLinterNet.Core.Execution",
                    External = true
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    "ArchLinterNet.Core",
                    "ArchLinterNet.Core.Tests"
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
                {
                    new()
                    {
                        Name = "tests-must-not-use-execution",
                        Source = "tests",
                        Allowed = new List<string> { "tests" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[]
            {
                typeof(ArchitectureContractRunner).Assembly,
                typeof(ProtectedContractTests.ExecutionUser).Assembly
            },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckAllowOnlyContract(document.Contracts.StrictAllowOnly[0]);

        Assert.That(violations, Has.Some.Matches<ArchitectureViolation>(v =>
            v.SourceType == typeof(ProtectedContractTests.ExecutionUser).FullName
            && v.ForbiddenReferences.Contains(typeof(ArchitectureContractRunner).FullName!)));
    }
}

public sealed class SelectorReferenceSource
{
    public TypeWithBooleanProperty Target { get; } = null!;
}
