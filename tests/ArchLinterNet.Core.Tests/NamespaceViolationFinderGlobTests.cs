using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class NamespaceViolationFinderGlobTests
{
    [Test]
    public void FindNamespaceViolations_GlobLayer_CollectsConcreteMatchedPrefixesFromForbiddenReferences()
    {
        ArchitectureLayer forbiddenLayer = new() { Namespace = "ReviewTest.Modules.*.Internal" };

        var violations = ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                "glob-rule",
                "glob-rule",
                new[] { typeof(ReviewTest.SharedKernel.SharedKernelType) },
                forbiddenLayer,
                Array.Empty<string>(),
                Array.Empty<ArchitectureIgnoredViolation>())
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

        string output = ArchitectureDiagnosticFormatter.FormatViolationsForHumans(new[] { violation });

        Assert.That(output, Does.Contain("matched ReviewTest.Modules.Billing.Internal, ReviewTest.Modules.Sales.Internal"));
    }
}
