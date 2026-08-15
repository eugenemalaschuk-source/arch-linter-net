using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CliCommandBoundaryPolicyTests
{
    private const string CommandNamespacePrefix = "ArchLinterNet.Cli.Commands.";

    [Test]
    public void DirectCommandFolders_AreGovernedWithoutAPeerInventory()
    {
        string repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        string commandsDirectory = Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "Commands");
        string[] commandFolders = Directory
            .EnumerateDirectories(commandsDirectory)
            .Select(directory => new DirectoryInfo(directory).Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] commandRootSourceFiles = Directory
            .EnumerateFiles(commandsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        ArchitectureContractDocument policy = new ArchitecturePolicyDocumentLoader().Load(
            SelfPolicyRepository.PolicyPath(repositoryRoot));
        ArchitectureModuleContainerContract discoveredContract = policy.Contracts.StrictModuleContainers.Single(
            candidate => candidate.Id == "cli-command-modules-follow-the-feature-profile");

        Assert.Multiple(() =>
        {
            Assert.That(commandFolders, Is.Not.Empty);
            Assert.That(policy.Layers.Keys, Is.All.Not.StartsWith("cli_command_"));
            Assert.That(policy.Contracts.StrictIndependence,
                Has.None.Matches<ArchitectureIndependenceContract>(candidate => candidate.Id == "cli-command-modules-are-independent"));
            Assert.That(discoveredContract.Container, Is.EqualTo(CommandNamespacePrefix.TrimEnd('.')));
            Assert.That(discoveredContract.Profile, Is.EqualTo("cli_command"));
            Assert.That(commandRootSourceFiles, Is.Empty,
                "Cli.Commands is a module container, not a shared behaviour bucket. Shared output belongs to the named Integration.OutputFormatting boundary.");
        });
    }

}
