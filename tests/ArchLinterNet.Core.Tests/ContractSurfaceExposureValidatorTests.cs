using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.Validators;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceExposureValidatorTests
{
    [Test]
    public void Validate_BlankId_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = " ",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource { Assemblies = ["Fixture"] },
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("non-blank 'id'"));
    }

    [Test]
    public void Validate_BlankName_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = " ",
            Source = new ArchitectureContractSurfaceExposureSource { Assemblies = ["Fixture"] },
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("non-blank 'name'"));
    }

    [Test]
    public void Validate_NullSource_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = null!,
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("must declare a 'source' object"));
    }

    [Test]
    public void Validate_EmptyForbidden_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource { Assemblies = ["Fixture"] },
            Forbidden = [],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("no 'forbidden' selectors"));
    }

    [Test]
    public void Validate_AssembliesAndProjectsBothPopulated_ValidatesBothLists()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = ["Fixture"],
                Projects = ["src/Fixture/Fixture.csproj"],
            },
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });
        document.Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = ["Fixture"] };

        Assert.DoesNotThrow(() => new ContractSurfaceExposureValidator().Validate(document));
    }

    [Test]
    public void Validate_NoUsableSourceSelector_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource(),
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("no usable source selector"));
    }

    [Test]
    public void Validate_BlankEntryInSourceList_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource { Assemblies = [" "] },
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("blank entry in 'assemblies'"));
    }

    [Test]
    public void Validate_EmptyOrUnboundedForbiddenSelector_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource { Assemblies = ["Fixture"] },
            Forbidden = [new ArchitecturePublicApiSurfaceSelector()],
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("empty or unbounded 'forbidden[0]' selector"));
    }

    [Test]
    public void Validate_AmbiguousPublicApiSurfaceReference_Throws()
    {
        ArchitectureContractDocument document = Document(new ArchitectureContractSurfaceExposureContract
        {
            Id = "exposure",
            Name = "exposure",
            Source = new ArchitectureContractSurfaceExposureSource { PublicApiSurface = "reviewed-api" },
            Forbidden = [new ArchitecturePublicApiSurfaceSelector { Role = "Entity" }],
        });
        document.Contracts = new ArchitectureContractGroups
        {
            StrictContractSurfaceExposure = document.Contracts.StrictContractSurfaceExposure,
            StrictPublicApiSurface =
            [
                new ArchitecturePublicApiSurfaceContract { Id = "reviewed-api", Name = "reviewed-api-1", Assemblies = ["Fixture"] },
                new ArchitecturePublicApiSurfaceContract { Id = "reviewed-api", Name = "reviewed-api-2", Assemblies = ["Fixture"] },
            ],
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new ContractSurfaceExposureValidator().Validate(document))!;
        Assert.That(error.Message, Does.Contain("ambiguous public API surface"));
    }

    private static ArchitectureContractDocument Document(ArchitectureContractSurfaceExposureContract contract) => new()
    {
        Name = "contract-surface-exposure-validator",
        Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = ["Fixture"] },
        Contracts = new ArchitectureContractGroups
        {
            StrictContractSurfaceExposure = [contract],
        },
    };
}
