using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Ratchets the test-cleanup boundary without governing legitimate generated interop or source
/// snippets that deliberately model C# partial declarations.
/// </summary>
[TestFixture]
public sealed class CoreTestArchitectureCleanupTests
{
    [Test]
    public void PartialLanguageFixture_RemainsDiscoverableWithEveryDeclarationPath()
    {
        const string Part = "namespace Fixture { public partial class IntentionalFixture { } }";
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory("/fake/src");
        fileSystem.AddFile("/fake/src/IntentionalFixture.Part1.cs", Part, DateTime.UtcNow);
        fileSystem.AddFile("/fake/src/IntentionalFixture.Part2.cs", Part, DateTime.UtcNow);

        var index = new ArchitectureSourceFileFactIndex(
            [typeof(CoreTestArchitectureCleanupTests).Assembly],
            "/fake",
            ["src"],
            preprocessorSymbols: null,
            fileSystem);

        IReadOnlyList<ArchitectureTypeSourceDeclaration> declarations = index.SourceDeclarations
            .Where(declaration => declaration.FullTypeName == "Fixture.IntentionalFixture")
            .ToArray();

        Assert.That(declarations, Has.Count.EqualTo(2));
        Assert.That(declarations.All(declaration => declaration.IsPartial), Is.True);
        Assert.That(
            declarations.Select(declaration => declaration.SourceFilePath),
            Is.EqualTo(new[] { "src/IntentionalFixture.Part1.cs", "src/IntentionalFixture.Part2.cs" }));
    }

    [Test]
    public void ProductionPartialDeclarations_DoNotSpanMultipleSourceFiles()
    {
        string repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        ArchitectureContractDocument policy = new ArchitecturePolicyDocumentLoader().Load(
            SelfPolicyRepository.PolicyPath(repositoryRoot));
        ProjectDiscoveryResult projectDiscovery = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(policy, repositoryRoot, resolveAssemblyOutputs: true);
        IReadOnlyList<Assembly> productionAssemblies = projectDiscovery.DiscoveredProjects
            .Select(project => project.AssemblyName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(assemblyName => LoadProductionAssembly(
                assemblyName, projectDiscovery.ResolvedAssemblyPaths[assemblyName]))
            .ToArray();
        var sourceIndex = new ArchitectureSourceFileFactIndex(
            productionAssemblies,
            repositoryRoot,
            projectDiscovery.SourceRoots.ToArray(),
            preprocessorSymbols: null,
            fileSystem: null,
            new ArchitectureSourceFileFactIndex.ProjectOwnership(projectDiscovery, SourceRootAssemblyOwnership: null));
        Assert.That(sourceIndex.SourceDeclarations, Is.Not.Empty,
            "The production source index must discover the solution's production projects through project ownership.");
        Assert.That(sourceIndex.SourceDeclarations.Select(declaration => declaration.AssemblyName).Distinct(),
            Is.EquivalentTo(productionAssemblies.Select(assembly => assembly.GetName().Name!)),
            "Project ownership must preserve each production source file's assembly identity.");

        var declarations = sourceIndex.SourceDeclarations
            .Where(declaration => declaration.IsPartial)
            .GroupBy(declaration => (declaration.AssemblyName, declaration.FullTypeName))
            .Select(group => new
            {
                group.Key.AssemblyName,
                group.Key.FullTypeName,
                SourcePaths = group
                    .Select(declaration => declaration.SourceFilePath)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray(),
            })
            .Where(group => group.SourcePaths.Length > 1)
            .OrderBy(group => group.AssemblyName, StringComparer.Ordinal)
            .ThenBy(group => group.FullTypeName, StringComparer.Ordinal)
            .ToArray();

        Assert.That(declarations, Is.Empty,
            "A production type may use a generated partial declaration, but must not span multiple source files: "
            + string.Join(", ", declarations.Select(group =>
                $"{group.AssemblyName}:{group.FullTypeName} ({string.Join(", ", group.SourcePaths)})")));
    }

    private static Assembly LoadProductionAssembly(string assemblyName, string assemblyPath)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
            ?? ArchitectureAssemblyLoader.Real.LoadFrom(assemblyPath);
    }
}
