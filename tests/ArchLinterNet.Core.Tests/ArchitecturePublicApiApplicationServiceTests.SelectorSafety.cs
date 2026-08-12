using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using Fixtures = PublicApiSurfaceSelectorTestFixtures;

namespace ArchLinterNet.Core.Tests;

// Lifecycle regression (PR #529 review): the selector-safety checks strict/audit validation runs
// (zero-match, first-party escape) must also block capture/diff/update/migrate through the shared
// ResolveSurface seam — otherwise a selector configuration `validate` rejects could still produce a
// snapshot through `capture`/`update`, which `validate` would then never be able to pass against.
public sealed partial class ArchitecturePublicApiApplicationServiceTests
{
    [Test]
    public void Capture_SelectorWithFirstPartyEscape_FailsClosed()
    {
        ArchitecturePublicApiSurfaceContract contract = new()
        {
            Id = ContractId,
            Name = ContractId,
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };

        PublicApiCaptureOutcome outcome = Service(Document(contract), new FakePublicApiSnapshotStore()).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.Error, Does.Contain(typeof(Fixtures.SelectedWithEscapingDependency).FullName!));
            Assert.That(outcome.Error, Does.Contain(typeof(Fixtures.IncidentalType).FullName!));
        });
    }

    [Test]
    public void Update_SelectorWithFirstPartyEscape_FailsClosedWithoutWritingSnapshot()
    {
        ArchitecturePublicApiSurfaceContract contract = new()
        {
            Id = ContractId,
            Name = ContractId,
            Assemblies = new List<string> { AssemblyName },
            ApiSnapshot = SnapshotPath,
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };

        PublicApiUpdateOutcome outcome = Service(Document(contract), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest { PolicyPath = PolicyPath, ContractId = ContractId, SnapshotPath = SnapshotPath });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.Error, Does.Contain(typeof(Fixtures.SelectedWithEscapingDependency).FullName!));
            Assert.That(outcome.Error, Does.Contain(typeof(Fixtures.IncidentalType).FullName!));
        });
    }
}
