using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AllowOnlyWithSuffixTests
{
    [Test]
    public void CheckAllowOnlyContract_WithNamespaceSuffix_RespectsSuffixBoundary()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["domain"] = new() { Namespace = "Test.Domain", NamespaceSuffix = "Models" },
                ["web"] = new() { Namespace = "Test.Web" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
                {
                    new()
                    {
                        Name = "domain-allow-only",
                        Source = "domain",
                        Allowed = new List<string> { "domain" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckAllowOnlyContract(document.Contracts.StrictAllowOnly[0]);

        Assert.That(violations, Is.Empty);
    }
}
