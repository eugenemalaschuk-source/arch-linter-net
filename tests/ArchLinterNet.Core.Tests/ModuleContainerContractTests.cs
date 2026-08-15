using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ModuleContainerContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
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

    [Test]
    public void CheckModuleContainerContract_CleanDynamicModules_HasNoViolations()
    {
        List<ArchitectureViolation> violations = Check("ModuleContainerFixtures.Clean");

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ModuleNamespaceMembershipResolver_MapsContainerModuleAndNestedSegmentDeterministically()
    {
        const string Container = "Example.Cli.Commands";

        bool rootResolved = ArchitectureModuleNamespaceMembershipResolver.TryResolve(
            Container, Container, out ArchitectureModuleNamespaceMembership? root);
        bool moduleResolved = ArchitectureModuleNamespaceMembershipResolver.TryResolve(
            Container, "Example.Cli.Commands.Validate", out ArchitectureModuleNamespaceMembership? moduleRoot);
        bool nestedResolved = ArchitectureModuleNamespaceMembershipResolver.TryResolve(
            Container, "Example.Cli.Commands.Validate.EntryPoint.Generated", out ArchitectureModuleNamespaceMembership? nested);
        bool outsideResolved = ArchitectureModuleNamespaceMembershipResolver.TryResolve(
            Container, "Example.Cli.Other.Validate.EntryPoint", out ArchitectureModuleNamespaceMembership? outside);

        Assert.Multiple(() =>
        {
            Assert.That(rootResolved, Is.True);
            Assert.That(root, Is.EqualTo(new ArchitectureModuleNamespaceMembership(Container, null, null, IsContainerRoot: true)));
            Assert.That(moduleResolved, Is.True);
            Assert.That(moduleRoot, Is.EqualTo(new ArchitectureModuleNamespaceMembership(Container, "Validate", null, IsContainerRoot: false)));
            Assert.That(nestedResolved, Is.True);
            Assert.That(nested, Is.EqualTo(new ArchitectureModuleNamespaceMembership(Container, "Validate", "EntryPoint", IsContainerRoot: false)));
            Assert.That(outsideResolved, Is.False);
            Assert.That(outside, Is.Null);
        });
    }

    [Test]
    public void CheckModuleContainerContract_CrossModuleReference_ReportsSourceAndTargetModule()
    {
        List<ArchitectureViolation> violations = Check("ModuleContainerFixtures.Cross");

        ArchitectureViolation violation = violations.Single();
        Assert.That(violation.SourceType, Is.EqualTo("ModuleContainerFixtures.Cross.Alpha.Application.AlphaCommand"));
        Assert.That(violation.ForbiddenNamespace, Is.EqualTo("ModuleContainerFixtures.Cross.Beta"));
        Assert.That(violation.ForbiddenReferences,
            Does.Contain("ModuleContainerFixtures.Cross.Beta.Application.BetaCommand"));
    }

    [Test]
    public void CheckModuleContainerContract_CrossModuleReference_HasStableHumanAndSarifEvidence()
    {
        List<ArchitectureViolation> violations = Check("ModuleContainerFixtures.Cross");

        string human = new ArchitectureDiagnosticFormatter().FormatViolationsForHumans(violations);
        string sarif = new ArchitectureSarifFormatter().FormatResultAsSarif(
            "audit", violations, Array.Empty<string>(), "test");

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("ModuleContainerFixtures.Cross.Alpha.Application.AlphaCommand"));
            Assert.That(human, Does.Contain("ModuleContainerFixtures.Cross.Beta"));
            Assert.That(sarif, Does.Contain("ModuleContainerFixtures.Cross.Alpha.Application.AlphaCommand"));
            Assert.That(sarif, Does.Contain("ModuleContainerFixtures.Cross.Beta.Application.BetaCommand"));
        });
    }

    [Test]
    public void CheckModuleContainerContract_InvalidModuleLayout_ReportsActionableMarkers()
    {
        List<ArchitectureViolation> violations = Check("ModuleContainerFixtures.Structure");

        Assert.That(violations.Select(violation => violation.ForbiddenNamespace), Is.EquivalentTo(new[]
        {
            "<module-root:Orders>",
            "<generic-module:Common>",
            "<generic-module:common>",
            "<undeclared-segment:Infrastructure>",
        }));
    }

    [Test]
    public void ContractLoader_LoadsModuleContainerContract()
    {
        string path = WriteContract(@"
version: 1
name: Test
layers: {}
analysis:
  target_assemblies: []
contracts:
  strict_module_containers:
    - id: cli-commands
      name: CLI commands
      container: ArchLinterNet.Cli.Commands
      profile: cli_command
      allowed_container_root_types: [ArchLinterNet.Cli.Commands.CommandCatalog]
      allowed_module_root_types: [ArchLinterNet.Cli.Commands.Help.CommandHelp]
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        ArchitectureModuleContainerContract contract = document.Contracts.StrictModuleContainers.Single();
        Assert.Multiple(() =>
        {
            Assert.That(contract.Id, Is.EqualTo("cli-commands"));
            Assert.That(contract.Profile, Is.EqualTo("cli_command"));
            Assert.That(contract.AllowedContainerRootTypes, Does.Contain("ArchLinterNet.Cli.Commands.CommandCatalog"));
        });
    }

    [TestCase("", "cli_command", "non-empty dot-separated container namespace")]
    [TestCase("ArchLinterNet..Cli.Commands", "cli_command", "non-empty dot-separated container namespace")]
    [TestCase("ArchLinterNet.Cli. Commands", "cli_command", "non-empty dot-separated container namespace")]
    [TestCase("ArchLinterNet.Cli.*", "cli_command", "non-empty dot-separated container namespace")]
    [TestCase("ArchLinterNet.Cli.Commands", "unsupported", "unsupported profile")]
    public void ContractLoader_InvalidModuleContainerConfiguration_Throws(string container, string profile, string expectedMessage)
    {
        string path = WriteContract($@"
version: 1
name: Test
layers: {{}}
analysis:
  target_assemblies: []
contracts:
  strict_module_containers:
    - name: CLI commands
      container: {container}
      profile: {profile}
");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void ContractLoader_ModuleContainerUnknownProperty_Throws()
    {
        string path = WriteContract("""
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: []
            contracts:
              strict_module_containers:
                - name: CLI commands
                  container: ArchLinterNet.Cli.Commands
                  profile: cli_command
                  profile_typo: cli_command
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain("unknown property 'profile_typo'"));
    }

    private List<ArchitectureViolation> Check(string container)
    {
        Assembly fixtureAssembly = typeof(ModuleContainerFixtures.Clean.First.Application.FirstCommand).Assembly;
        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { fixtureAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());
        var contract = new ArchitectureModuleContainerContract
        {
            Id = "module-container",
            Name = "module container",
            Container = container,
            Profile = "cli_command",
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { fixtureAssembly.GetName().Name! },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictModuleContainers = new List<ArchitectureModuleContainerContract> { contract },
            },
        };

        return new ArchitectureContractRunner(context, document).CheckModuleContainerContract(contract);
    }

    private string WriteContract(string yaml)
    {
        string contractDirectory = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDirectory);
        string contractPath = Path.Combine(contractDirectory, "dependencies.arch.yml");
        File.WriteAllText(contractPath, yaml);
        return contractPath;
    }
}
