using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class VersionedContractSurfaceIsolationValidatorTests
{
    [Test]
    public void Validate_ValidStrictAndAuditContracts_DoesNotThrow()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        ArchitectureContractDocument document = new()
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["api"] = new ArchitectureLayer { Namespace = "Product.Api" },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictVersionedContractSurfaceIsolation = [contract],
                AuditVersionedContractSurfaceIsolation = [Contract()],
            },
        };

        Assert.DoesNotThrow(() => new VersionedContractSurfaceIsolationValidator().Validate(document));
    }

    [Test]
    public void Validate_BlankIdentity_Throws()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.Id = " ";

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain("non-blank 'id' and 'name'"));
    }

    [Test]
    public void Validate_EmptySurfaces_Throws()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.Surfaces = [];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain("non-empty 'surfaces' list"));
    }

    [TestCase(" ", "blank surface ID")]
    [TestCase("api-v1", "duplicate surface ID")]
    public void Validate_InvalidSurfaceId_Throws(string secondId, string expected)
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.Surfaces.Add(new ArchitectureVersionedContractSurfaceIsolationSurface
        {
            Id = secondId,
            TypesMatching = new ArchitecturePublicApiSurfaceSelector { Namespace = "Product.Api" },
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain(expected));
    }

    [Test]
    public void Validate_EmptySurfaceSelector_Throws()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.Surfaces[0].TypesMatching = new ArchitecturePublicApiSurfaceSelector();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain("empty or unbounded 'types_matching' selector"));
    }

    [Test]
    public void Validate_UnknownLayer_Throws()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.Surfaces[0].TypesMatching.Layer = "missing-layer";

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain("unknown layer 'missing-layer'"));
    }

    [TestCase("", "unknown source surface")]
    [TestCase("missing", "unknown source surface")]
    public void Validate_InvalidSourceSurface_Throws(string source, string expected)
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.SourceSurface = source;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain(expected));
    }

    [Test]
    public void Validate_EmptyForbiddenSurfaces_Throws()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.ForbiddenSurfaces = [];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain("non-empty 'forbidden_surfaces' list"));
    }

    [TestCase("", "blank or duplicate forbidden surface")]
    [TestCase("api-v1", "cannot forbid its source surface")]
    [TestCase("domain-v1", "blank or duplicate forbidden surface")]
    public void Validate_InvalidForbiddenSurfaceReference_Throws(string forbidden, string expected)
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.ForbiddenSurfaces = [forbidden, "domain-v1"];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain(expected));
    }

    [Test]
    public void Validate_UnknownForbiddenSurface_Throws()
    {
        ArchitectureVersionedContractSurfaceIsolationContract contract = Contract();
        contract.ForbiddenSurfaces = ["missing"];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(contract))!;

        Assert.That(error.Message, Does.Contain("unknown forbidden surface 'missing'"));
    }

    private static void Validate(ArchitectureVersionedContractSurfaceIsolationContract contract)
    {
        new VersionedContractSurfaceIsolationValidator().Validate(Document(contract));
    }

    private static ArchitectureContractDocument Document(
        ArchitectureVersionedContractSurfaceIsolationContract contract) => new()
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["api"] = new ArchitectureLayer { Namespace = "Product.Api" },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictVersionedContractSurfaceIsolation = [contract],
            },
        };

    private static ArchitectureVersionedContractSurfaceIsolationContract Contract() => new()
    {
        Id = "isolation",
        Name = "Isolation",
        Surfaces =
        [
            new ArchitectureVersionedContractSurfaceIsolationSurface
            {
                Id = "api-v1",
                TypesMatching = new ArchitecturePublicApiSurfaceSelector { Layer = "api" },
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
}
