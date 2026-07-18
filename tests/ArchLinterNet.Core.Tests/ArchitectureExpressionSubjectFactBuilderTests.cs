using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Expressions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Focused coverage for ArchitectureExpressionSubjectFactBuilder.Build's project-name resolution,
// exercised directly rather than through a full YAML-load pipeline.
[TestFixture]
public sealed class ArchitectureExpressionSubjectFactBuilderTests
{
    private static ArchitectureRoleIndex EmptyRoleIndex(Type[] types) =>
        new(new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex(new[] { typeof(ArchitectureExpressionSubjectFactBuilderTests).Assembly }));

    private static ArchitectureSourceFileFactIndex EmptySourceIndex() =>
        new(new[] { typeof(ArchitectureExpressionSubjectFactBuilderTests).Assembly }, ".", Array.Empty<string>());

    [Test]
    public void Build_ProjectNameResolvesFromDiscoveredProjectFileNameNotAssemblyName()
    {
        string assemblyName = typeof(ArchitectureExpressionSubjectFactBuilderTests).Assembly.GetName().Name!;
        ProjectDiscoveryResult discovery = new(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(
                    $"tests/{assemblyName}/{assemblyName}.Custom.csproj", assemblyName, new[] { "net10.0" })
            }
        };

        ArchitectureExpressionSubjectFacts facts = ArchitectureExpressionSubjectFactBuilder.Build(
            typeof(ArchitectureExpressionSubjectFactBuilderTests),
            EmptyRoleIndex(Array.Empty<Type>()),
            EmptySourceIndex(),
            discovery);

        Assert.That(facts.ProjectName, Is.EqualTo($"{assemblyName}.Custom"));
    }

    [Test]
    public void Build_NoProjectDiscovery_ProjectNameFallsBackToAssemblyName()
    {
        string assemblyName = typeof(ArchitectureExpressionSubjectFactBuilderTests).Assembly.GetName().Name!;

        ArchitectureExpressionSubjectFacts facts = ArchitectureExpressionSubjectFactBuilder.Build(
            typeof(ArchitectureExpressionSubjectFactBuilderTests),
            EmptyRoleIndex(Array.Empty<Type>()),
            EmptySourceIndex(),
            projectDiscovery: null);

        Assert.That(facts.ProjectName, Is.EqualTo(assemblyName));
    }

    [Test]
    public void Build_UnclassifiedType_RoleAndMetadataAreEmptyWithoutThrowing()
    {
        ArchitectureExpressionSubjectFacts facts = ArchitectureExpressionSubjectFactBuilder.Build(
            typeof(ArchitectureExpressionSubjectFactBuilderTests),
            EmptyRoleIndex(Array.Empty<Type>()),
            EmptySourceIndex(),
            projectDiscovery: null);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Role, Is.Empty);
            Assert.That(facts.MetadataText, Is.Empty);
            Assert.That(facts.MetadataBool, Is.Empty);
            Assert.That(facts.Kind, Is.EqualTo("class"));
        });
    }
}
