using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.PolicyContext;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyContextContractFactsProjectorTests
{
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
            Assert.That(source.Items.Single(fact => fact.Name == "assemblies").Values, Is.EqualTo(new[] { "Product.Api" }));
            Assert.That(source.Items.Single(fact => fact.Name == "projects").Values,
                Is.EqualTo(new[] { "src/Product.Api/Product.Api.csproj" }));
            Assert.That(typesMatching.Items.Single(fact => fact.Name == "namespace").Values,
                Is.EqualTo(new[] { "Product.Api.Contracts" }));
            Assert.That(typesMatching.Items.Single(fact => fact.Name == "layer").Values, Is.EqualTo(new[] { "api" }));
            Assert.That(typesMatching.Items.Single(fact => fact.Name == "role").Values,
                Is.EqualTo(new[] { "ApiContract" }));
            Assert.That(source.Items.Single(fact => fact.Name == "public_api_surface").Values,
                Is.EqualTo(new[] { "reviewed-api" }));
            Assert.That(selector.Name, Is.EqualTo("selector"));
            Assert.That(selector.Items.Single(fact => fact.Name == "namespace").Values,
                Is.EqualTo(new[] { "Product.Internal" }));
            Assert.That(selector.Items.Single(fact => fact.Name == "has_attribute").Values,
                Is.EqualTo(new[] { "Product.InternalAttribute" }));
        });
    }
}
