using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FolderPurityLayoutConventionTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-folder-purity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        WriteSource("Abstractions/IOrderPort.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Abstractions { public interface IOrderPort { } }");
        WriteSource("Abstractions/AbstractOrderPort.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Abstractions { public abstract class AbstractOrderPort { } }");
        WriteSource("Abstractions/ConcreteOrderPort.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Abstractions { public sealed class ConcreteOrderPort { } }");
        WriteSource("Abstractions/ValueOrderPort.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Abstractions { public readonly struct ValueOrderPort { } }");
        WriteSource("Exceptions/OrderRejectedException.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Exceptions { public sealed class OrderRejectedException : System.Exception { } }");
        WriteSource("Exceptions/IncorrectExceptionRecord.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Exceptions { public sealed record IncorrectExceptionRecord; }");
        WriteSource("Exceptions/IIncorrectException.cs", "namespace LayoutConventionContractTestFixtures.FolderPurity.Exceptions { public interface IIncorrectException { } }");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void AllDeclarations_AbstractionsAcceptInterfacesAndAbstractClasses_ButRejectConcreteAndValueTypes()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "abstractions must be pure",
            Id = "abstraction-purity",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Abstractions" },
            AllDeclarations = new ArchitectureLayoutDeclarationShape
            {
                AllowedTypeKinds = new List<string> { "interface", "class" },
                RequireAbstractClasses = true,
            },
        };

        List<ArchitectureViolation> violations = Check(contract);

        Assert.That(violations.Select(violation => violation.SourceType), Is.EquivalentTo(new[]
        {
            "LayoutConventionContractTestFixtures.FolderPurity.Abstractions.ConcreteOrderPort",
            "LayoutConventionContractTestFixtures.FolderPurity.Abstractions.ValueOrderPort",
        }));
        LayoutConventionPayload payload = (LayoutConventionPayload)violations.Single(
            violation => violation.SourceType.EndsWith("ConcreteOrderPort", StringComparison.Ordinal)).Payload!;
        Assert.Multiple(() =>
        {
            Assert.That(payload.ActualTypeKind, Is.EqualTo("Class"));
            Assert.That(payload.ActualRole, Is.EqualTo("unclassified"));
            Assert.That(payload.ActualIsAbstract, Is.False);
            Assert.That(payload.ExpectedAbstractClass, Is.True);
        });
    }

    [Test]
    public void AllDeclarations_ExceptionsRejectEveryNonExceptionRoleRegardlessOfTypeKind()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "exceptions must be pure",
            Id = "exception-purity",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Exceptions" },
            AllDeclarations = new ArchitectureLayoutDeclarationShape
            {
                AllowedRoles = new List<string> { "Exception" },
            },
        };

        List<ArchitectureViolation> violations = Check(contract);

        Assert.That(violations.Select(violation => violation.SourceType), Is.EquivalentTo(new[]
        {
            "LayoutConventionContractTestFixtures.FolderPurity.Exceptions.IncorrectExceptionRecord",
            "LayoutConventionContractTestFixtures.FolderPurity.Exceptions.IIncorrectException",
        }));
        Assert.That(violations.All(violation => violation.Payload is LayoutConventionPayload
        {
            ActualRole: "unclassified",
            ActualIsAbstract: false,
        }), Is.True);
    }

    [Test]
    public void ContractLoader_AllDeclarationsWithoutPermittedKindsOrRoles_Throws()
    {
        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: []
            contracts:
              strict_layout_conventions:
                - name: invalid purity
                  files_matching:
                    folder_segment: Abstractions
                  all_declarations:
                    require_abstract_classes: true
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(exception.Message, Does.Contain("effective permitted shape"));
    }

    private List<ArchitectureViolation> Check(ArchitectureLayoutConventionContract contract)
    {
        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(LayoutConventionContractTestFixtures.FolderPurity.Abstractions.IOrderPort).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    typeof(LayoutConventionContractTestFixtures.FolderPurity.Abstractions.IOrderPort).Assembly.GetName().Name!,
                },
                SourceRoots = new List<string> { "." },
            },
            Classification = new ArchitectureClassificationConfiguration
            {
                Inheritance = new List<ArchitectureInheritanceClassificationMapping>
                {
                    new() { BaseType = "System.Exception", Role = "Exception" },
                },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayoutConventions = new List<ArchitectureLayoutConventionContract> { contract },
            },
        };

        return new ArchitectureContractRunner(context, document).Session.CheckLayoutConventionsContract(contract);
    }

    private void WriteSource(string relativePath, string content)
    {
        string path = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
