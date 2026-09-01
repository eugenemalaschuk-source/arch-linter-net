using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.PolicyContext;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyContextContractFactsProjectorTests
{
    private static readonly string[] _expectedAssemblies = ["Product.Api"];
    private static readonly string[] _expectedProjects = ["src/Product.Api/Product.Api.csproj"];
    private static readonly string[] _expectedNamespace = ["Product.Api.Contracts"];
    private static readonly string[] _expectedLayer = ["api"];
    private static readonly string[] _expectedRole = ["ApiContract"];
    private static readonly string[] _expectedPublicApiSurface = ["reviewed-api"];
    private static readonly string[] _expectedForbiddenNamespace = ["Product.Internal"];
    private static readonly string[] _expectedForbiddenAttribute = ["Product.InternalAttribute"];
    private static readonly string[] _expectedVersionedNamespace = ["Product.Api.V1"];
    private static readonly string[] _expectedVersionedLayer = ["api"];
    private static readonly string[] _expectedVersionedForbiddenSurface = ["domain-v1"];

    [Test]
    public void Project_ContractSurfaceExposure_PreservesSourceAndForbiddenSelectorFacts()
    {
        var contract = new ArchitectureContractSurfaceExposureContract
        {
            Reason = "Keep internal entities out of published contracts.",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = ["Product.Api"],
                Projects = ["src/Product.Api/Product.Api.csproj"],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "Product.Api.Contracts",
                    Layer = "api",
                    Role = "ApiContract",
                },
                PublicApiSurface = "reviewed-api",
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "Product.Internal",
                    HasAttribute = "Product.InternalAttribute",
                },
            ],
        };

        ArchitecturePolicyContextContractProjection projection =
            ArchitecturePolicyContextContractFactsProjector.Project(contract);
        ArchitecturePolicyContextContractFact source = projection.Facts.Single(fact => fact.Name == "source");
        ArchitecturePolicyContextContractFact typesMatching = source.Items.Single(fact => fact.Name == "types_matching");
        ArchitecturePolicyContextContractFact forbidden = projection.Facts.Single(fact => fact.Name == "forbidden");
        ArchitecturePolicyContextContractFact selector = forbidden.Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(projection.Reason, Is.EqualTo(contract.Reason));
            Assert.That(source.Items.Single(fact => fact.Name == "assemblies").Values, Is.EqualTo(_expectedAssemblies));
            Assert.That(source.Items.Single(fact => fact.Name == "projects").Values,
                Is.EqualTo(_expectedProjects));
            Assert.That(typesMatching.Items.Single(fact => fact.Name == "namespace").Values,
                Is.EqualTo(_expectedNamespace));
            Assert.That(typesMatching.Items.Single(fact => fact.Name == "layer").Values, Is.EqualTo(_expectedLayer));
            Assert.That(typesMatching.Items.Single(fact => fact.Name == "role").Values,
                Is.EqualTo(_expectedRole));
            Assert.That(source.Items.Single(fact => fact.Name == "public_api_surface").Values,
                Is.EqualTo(_expectedPublicApiSurface));
            Assert.That(selector.Name, Is.EqualTo("selector"));
            Assert.That(selector.Items.Single(fact => fact.Name == "namespace").Values,
                Is.EqualTo(_expectedForbiddenNamespace));
            Assert.That(selector.Items.Single(fact => fact.Name == "has_attribute").Values,
                Is.EqualTo(_expectedForbiddenAttribute));
        });
    }

    [Test]
    public void Project_VersionedContractSurfaceIsolation_PreservesSurfaceAndReferenceFacts()
    {
        var contract = new ArchitectureVersionedContractSurfaceIsolationContract
        {
            Reason = "Keep API versions isolated.",
            Surfaces =
            [
                new ArchitectureVersionedContractSurfaceIsolationSurface
                {
                    Id = "api-v1",
                    TypesMatching = new ArchitecturePublicApiSurfaceSelector
                    {
                        Namespace = "Product.Api.V1",
                        Layer = "api",
                    },
                },
                new ArchitectureVersionedContractSurfaceIsolationSurface
                {
                    Id = "domain-v1",
                    TypesMatching = new ArchitecturePublicApiSurfaceSelector { Role = "Entity" },
                },
            ],
            SourceSurface = "api-v1",
            ForbiddenSurfaces = ["domain-v1"],
        };

        ArchitecturePolicyContextContractProjection projection =
            ArchitecturePolicyContextContractFactsProjector.Project(contract);
        ArchitecturePolicyContextContractFact surfaces = projection.Facts.Single(fact => fact.Name == "surfaces");
        ArchitecturePolicyContextContractFact source = surfaces.Items.Single(item =>
            item.Items.Single(fact => fact.Name == "id").Values.Single() == "api-v1");
        ArchitecturePolicyContextContractFact forbidden = projection.Facts.Single(fact => fact.Name == "forbidden_surfaces");

        Assert.Multiple(() =>
        {
            Assert.That(projection.Reason, Is.EqualTo(contract.Reason));
            Assert.That(source.Items.Single(fact => fact.Name == "types_matching").Items
                .Single(fact => fact.Name == "namespace").Values, Is.EqualTo(_expectedVersionedNamespace));
            Assert.That(source.Items.Single(fact => fact.Name == "types_matching").Items
                .Single(fact => fact.Name == "layer").Values, Is.EqualTo(_expectedVersionedLayer));
            Assert.That(forbidden.Values, Is.EqualTo(_expectedVersionedForbiddenSurface));
        });
    }
}
