using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CliCommandBoundaryPolicyTests
{
    private const string CommandNamespacePrefix = "ArchLinterNet.Cli.Commands.";

    [Test]
    public void DirectCommandFolders_AreExactlyTheReviewedIndependenceModules()
    {
        string repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        string commandsDirectory = Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "Commands");
        string[] commandFolders = Directory
            .EnumerateDirectories(commandsDirectory)
            .Select(directory => new DirectoryInfo(directory).Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        ArchitectureContractDocument policy = new ArchitecturePolicyDocumentLoader().Load(
            SelfPolicyRepository.PolicyPath(repositoryRoot));
        var contract = policy.Contracts.StrictIndependence.Single(
            candidate => candidate.Id == "cli-command-modules-are-independent");
        string[] commandNamespaces = contract.Layers
            .Select(layer => policy.Layers[layer].Namespace)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(contract.Layers, Is.All.StartsWith("cli_command_"));
            Assert.That(commandNamespaces, Is.All.StartsWith(CommandNamespacePrefix));
            Assert.That(commandNamespaces.Select(GetDirectCommandName), Is.EqualTo(commandFolders));
        });
    }

    private static string GetDirectCommandName(string commandNamespace)
    {
        string commandName = commandNamespace[CommandNamespacePrefix.Length..];
        Assert.That(commandName, Does.Not.Contain('.'),
            "A command boundary must target an immediate Cli.Commands child namespace.");
        return commandName;
    }
}
