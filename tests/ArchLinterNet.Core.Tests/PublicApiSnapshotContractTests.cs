using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Covers the reviewed-snapshot side of public API surface contracts: load-time snapshot resolution
// and path safety, and the exact comparison mode that reports removals and changed signatures.
[TestFixture]
public sealed class PublicApiSnapshotContractTests
{
    private const string CleanDeclaredTypeName = "PublicApiSurfaceContractTestFixtures.CleanDeclaredType";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-public-api-snapshot-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static string AssemblyName => typeof(PublicApiSnapshotContractTests).Assembly.GetName().Name!;

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private string WriteSnapshot(string fileName, params string[] signatures)
    {
        string path = Path.Combine(_tempDir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion,
            "surface",
            signatures.Select(signature => new PublicApiSnapshotEntry(AssemblyName, signature)).ToArray())));
        return path;
    }

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(PublicApiSnapshotContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static List<ArchitectureViolation> Check(ArchitecturePublicApiSurfaceContract contract)
    {
        var groups = new ArchitectureContractGroups
        {
            StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract },
        };

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName },
            },
            Contracts = groups,
        };

        return new ArchitectureContractRunner(CreateContext(), document).Session.CheckPublicApiSurfaceContract(contract);
    }

    private static PublicApiSurfacePayload? PayloadFor(IEnumerable<ArchitectureViolation> violations, string signature)
    {
        return violations
            .Select(violation => violation.Payload as PublicApiSurfacePayload)
            .FirstOrDefault(payload => payload?.UndeclaredApiSignature == signature);
    }

    private static string PolicyYaml(string assemblyName, string snapshotFileName, string comparison) => $"""
        version: 1
        name: Test

        analysis:
          target_assemblies: [{assemblyName}]

        contracts:
          strict_public_api_surface:
            - id: surface
              name: surface
              assemblies: [{assemblyName}]
              api_snapshot: {snapshotFileName}
              api_comparison: {comparison}
              reason: Reviewed snapshot governs the exported surface.
        """;

    [Test]
    public void Load_SnapshotEntriesAreResolvedIntoTheContract()
    {
        WriteSnapshot("surface.txt", $"class {CleanDeclaredTypeName}");
        string policyPath = WritePolicy(PolicyYaml(AssemblyName, "surface.txt", "additions_only"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.That(
            document.Contracts.StrictPublicApiSurface[0].ResolvedSnapshotEntries.Select(entry => entry.Signature),
            Is.EqualTo(new[] { $"class {CleanDeclaredTypeName}" }));
    }

    [Test]
    public void Load_MissingSnapshotFile_Throws()
    {
        string policyPath = WritePolicy(PolicyYaml(AssemblyName, "absent.txt", "additions_only"));

        Assert.That(
            () => new ArchitecturePolicyDocumentLoader().Load(policyPath),
            Throws.InvalidOperationException.With.Message.Contains("does not exist"));
    }

    [Test]
    public void Load_UnparsableSnapshot_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "broken.txt"), "@format wrong\n");
        string policyPath = WritePolicy(PolicyYaml(AssemblyName, "broken.txt", "additions_only"));

        Assert.That(
            () => new ArchitecturePolicyDocumentLoader().Load(policyPath),
            Throws.InvalidOperationException.With.Message.Contains("unsupported format"));
    }

    [Test]
    public void Load_EscapingSnapshotPath_Throws()
    {
        string policyPath = WritePolicy(PolicyYaml(AssemblyName, "../outside.txt", "additions_only"));

        Assert.That(
            () => new ArchitecturePolicyDocumentLoader().Load(policyPath),
            Throws.InvalidOperationException.With.Message.Contains("outside the policy boundary"));
    }

    [Test]
    public void Load_AbsoluteSnapshotPath_Throws()
    {
        string absolute = Path.Combine(_tempDir, "surface.txt").Replace('\\', '/');
        WriteSnapshot("surface.txt", $"class {CleanDeclaredTypeName}");
        string policyPath = WritePolicy(PolicyYaml(AssemblyName, absolute, "additions_only"));

        Assert.That(
            () => new ArchitecturePolicyDocumentLoader().Load(policyPath),
            Throws.InvalidOperationException.With.Message.Contains("absolute public API snapshot path"));
    }

    [Test]
    public void Load_InvalidComparisonMode_Throws()
    {
        WriteSnapshot("surface.txt", $"class {CleanDeclaredTypeName}");
        string policyPath = WritePolicy(PolicyYaml(AssemblyName, "surface.txt", "loose"));

        Assert.That(
            () => new ArchitecturePolicyDocumentLoader().Load(policyPath),
            Throws.InvalidOperationException.With.Message.Contains("api_comparison: loose"));
    }

    [Test]
    public void Check_SnapshotDeclaredMemberIsNotReportedAsUndeclared()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            ResolvedSnapshotEntries = new[]
            {
                new PublicApiSnapshotEntry(AssemblyName, $"class {CleanDeclaredTypeName}"),
            },
        };

        Assert.That(PayloadFor(Check(contract), $"class {CleanDeclaredTypeName}"), Is.Null);
    }

    [Test]
    public void Check_AdditionsOnlyMode_DoesNotReportRemovedDeclaration()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string> { "class PublicApiSurfaceContractTestFixtures.NeverExisted" },
        };

        Assert.That(PayloadFor(Check(contract), "class PublicApiSurfaceContractTestFixtures.NeverExisted"), Is.Null);
    }

    [Test]
    public void Check_ExactMode_ReportsRemovedDeclaration()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            ApiComparison = PublicApiComparisonModes.Exact,
            DeclaredApi = new List<string> { "class PublicApiSurfaceContractTestFixtures.NeverExisted" },
        };

        PublicApiSurfacePayload? payload = PayloadFor(
            Check(contract), "class PublicApiSurfaceContractTestFixtures.NeverExisted");

        Assert.Multiple(() =>
        {
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.ApiDeltaKind, Is.EqualTo("removed"));
        });
    }

    [Test]
    public void Check_ExactMode_ReportsChangedSignatureOnceWithPreviousSignature()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            ApiComparison = PublicApiComparisonModes.Exact,
            DeclaredApi = new List<string> { $"method {CleanDeclaredTypeName}.DoWork(): System.Int32" },
        };

        List<ArchitectureViolation> violations = Check(contract);
        PublicApiSurfacePayload? changed = PayloadFor(violations, $"method {CleanDeclaredTypeName}.DoWork(): System.Void");

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.Not.Null);
            Assert.That(changed!.ApiDeltaKind, Is.EqualTo("changed"));
            Assert.That(changed.PreviousApiSignature, Is.EqualTo($"method {CleanDeclaredTypeName}.DoWork(): System.Int32"));
            Assert.That(PayloadFor(violations, $"method {CleanDeclaredTypeName}.DoWork(): System.Int32"), Is.Null,
                "A re-signed member must not additionally be reported as a removal.");
        });
    }

    [Test]
    public void Check_ExactMode_StillReportsUndeclaredAdditionAsAdded()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            ApiComparison = PublicApiComparisonModes.Exact,
        };

        PublicApiSurfacePayload? payload = PayloadFor(
            Check(contract), "class PublicApiSurfaceContractTestFixtures.AccidentalPublicType");

        Assert.Multiple(() =>
        {
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.ApiDeltaKind, Is.EqualTo("added"));
        });
    }

    [Test]
    public void Check_ExactMode_UnresolvedAssemblyDoesNotFabricateRemovals()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName, "Absent.Assembly" },
            ApiComparison = PublicApiComparisonModes.Exact,
            DeclaredApi = new List<string> { "class PublicApiSurfaceContractTestFixtures.NeverExisted" },
        };

        Assert.That(PayloadFor(Check(contract), "class PublicApiSurfaceContractTestFixtures.NeverExisted"), Is.Null);
    }
}
