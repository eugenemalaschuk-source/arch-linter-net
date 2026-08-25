using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;
using Fixtures = PublicApiSurfaceSelectorTestFixtures;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PublicApiSurfaceMaterializationTests
{
    private static string AssemblyName => typeof(PublicApiSurfaceMaterializationTests).Assembly.GetName().Name!;

    [Test]
    public void ContractsAndCaptureShareOneSessionSurfaceMaterialization_SeparateSessionIsFresh()
    {
        ArchitecturePublicApiSurfaceContract noSelector = new()
        {
            Name = "assembly-wide",
            Id = "assembly-wide",
            Assemblies = new List<string> { AssemblyName },
        };
        ArchitecturePublicApiSurfaceContract selector = new()
        {
            Name = "namespace-selected",
            Id = "namespace-selected",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                Namespace = "PublicApiSurfaceSelectorTestFixtures.PublicSurface",
            },
        };
        ArchitectureContractDocument document = CreateDocument(noSelector, selector);
        ArchitectureAnalysisSession session = CreateSession(document);

        Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(0));

        HashSet<string> assemblyWideTypes = SourceTypes(session.CheckPublicApiSurfaceContract(noSelector));
        HashSet<string> selectedTypes = SourceTypes(session.CheckPublicApiSurfaceContract(selector));

        Assert.Multiple(() =>
        {
            Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
            Assert.That(assemblyWideTypes, Does.Contain(typeof(Fixtures.IncidentalType).FullName));
            Assert.That(selectedTypes, Does.Contain(typeof(Fixtures.PublicSurface.SelectedByNamespace).FullName));
            Assert.That(selectedTypes, Does.Not.Contain(typeof(Fixtures.IncidentalType).FullName));
        });

        IReadOnlyList<PublicApiSnapshotEntry> captured = session.CapturePublicApiSurface(
            selector, out IReadOnlyList<string> missingAssemblies);

        Assert.Multiple(() =>
        {
            Assert.That(missingAssemblies, Is.Empty);
            Assert.That(captured, Is.Not.Empty);
            Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
        });

        ArchitectureAnalysisSession separateSession = CreateSession(document);
        separateSession.CheckPublicApiSurfaceContract(selector);

        Assert.That(separateSession.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
    }

    [Test]
    public void DifferentAssemblyArtifacts_MaterializeIndependentlyWithinOneSession()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(
            new ArchitecturePublicApiSurfaceContract(), new ArchitecturePublicApiSurfaceContract()));
        AssemblyBuilder syntheticAssembly = BuildSyntheticAssembly();

        session.GetPublicApiSurface(typeof(PublicApiSurfaceMaterializationTests).Assembly);
        session.GetPublicApiSurface(syntheticAssembly);

        Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(2));
    }

    private static HashSet<string> SourceTypes(IEnumerable<ArchitectureViolation> violations) =>
        violations.Select(violation => violation.SourceType).ToHashSet(StringComparer.Ordinal);

    private static ArchitectureAnalysisSession CreateSession(ArchitectureContractDocument document)
    {
        ArchitectureAnalysisContext context = new(
            "/tmp",
            new[] { typeof(PublicApiSurfaceMaterializationTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
        return new ArchitectureAnalysisSession(context, document, null, false, null);
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitecturePublicApiSurfaceContract noSelector,
        ArchitecturePublicApiSurfaceContract selector)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "public-api-materialization",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { noSelector },
                AuditPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { selector },
            },
        };
    }

    private static AssemblyBuilder BuildSyntheticAssembly()
    {
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"PublicApiSurfaceMaterialization-{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder typeBuilder = moduleBuilder.DefineType("Synthetic.PublicType", TypeAttributes.Public | TypeAttributes.Sealed);
        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        typeBuilder.CreateType();
        return assemblyBuilder;
    }
}
