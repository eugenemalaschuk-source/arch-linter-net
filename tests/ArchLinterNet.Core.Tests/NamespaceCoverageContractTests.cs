using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class NamespaceCoverageContractTests
{
    private const string FeatureRoot = "ArchLinterNet.Core.Tests.NamespaceCoverageFixtures.Features";

    private static readonly Assembly[] _targetAssemblies = { typeof(NamespaceCoverageContractTests).Assembly };

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());
    }

    private static ArchitectureCoverageContract CreateCoverageContract()
    {
        return new ArchitectureCoverageContract
        {
            Name = "namespace-feature-coverage",
            Id = "namespace-feature-coverage",
            Scope = "namespace",
            Reason = "Feature namespaces must be mapped or explicitly excluded.",
            Roots =
            {
                new ArchitectureCoverageRoot { Namespace = FeatureRoot }
            },
            Exclude =
            {
                new ArchitectureCoverageExclusion
                {
                    NamespaceSuffix = "Generated",
                    Reason = "Generated namespaces are excluded."
                }
            }
        };
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        ArchitectureContractDocument document = new();
        document.Layers["audio"] = new ArchitectureLayer
        {
            Namespace = $"{FeatureRoot}.Audio"
        };
        document.Layers["feature_api"] = new ArchitectureLayer
        {
            Namespace = $"{FeatureRoot}.*",
            NamespaceSuffix = "Api"
        };
        document.Contracts.StrictLayerTemplates.Add(new ArchitectureLayerTemplateContract
        {
            Name = "billing-template",
            Containers = { $"{FeatureRoot}.Billing" },
            Layers = { new ArchitectureTemplateLayer { Name = "Contracts" } },
            Reason = "Template coverage."
        });

        return document;
    }

    [Test]
    public void CheckCoverageContract_UsesLayersGlobsTemplatesAndExclusions()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(CreateCoverageContract());

        Assert.That(findings.Select(f => f.SourceType), Is.EqualTo(new[]
        {
            $"{FeatureRoot}.AlphaGap",
            $"{FeatureRoot}.ZetaGap"
        }));
        Assert.That(findings.SelectMany(f => f.ForbiddenReferences), Is.EqualTo(new[]
        {
            $"{FeatureRoot}.AlphaGap.AlphaGapRepresentative",
            $"{FeatureRoot}.ZetaGap.ZetaGapRepresentative"
        }));
    }

    [Test]
    public void CheckCoverageContract_RepeatedRuns_AreDeterministic()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureCoverageContract contract = CreateCoverageContract();

        ArchitectureContractRunner firstRunner = new(CreateContext(), document);
        ArchitectureContractRunner secondRunner = new(CreateContext(), document);

        List<ArchitectureViolation> first = firstRunner.CheckCoverageContract(contract);
        List<ArchitectureViolation> second = secondRunner.CheckCoverageContract(contract);

        Assert.That(
            first.Select(f => (f.SourceType, Representative: f.ForbiddenReferences.Single())),
            Is.EqualTo(second.Select(f => (f.SourceType, Representative: f.ForbiddenReferences.Single()))));
    }
}
