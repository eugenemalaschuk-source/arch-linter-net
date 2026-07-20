using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* graph */

    [Test]
    public void Graph_Help_ShowsUsageAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("graph"));
            Assert.That(stdout, Does.Contain("--level"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void Graph_DefaultInvocation_ProducesJsonWithNodesAndEdges()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--policy", _graphPolicy);

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("nodes", out _), Is.True);
            Assert.That(root.TryGetProperty("edges", out _), Is.True);
        });
    }

    [Test]
    public void Graph_NamespaceLevel_TagsViolatingEdgeWithContractId()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--policy", _graphPolicy, "--level", "namespace");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement edges = doc.RootElement.GetProperty("edges");

        bool found = edges.EnumerateArray().Any(edge =>
            edge.GetProperty("source").GetString() == "ArchLinterNet.Core.Execution"
            && edge.GetProperty("target").GetString() == "ArchLinterNet.Core.Contracts"
            && edge.GetProperty("contractIds").EnumerateArray()
                .Any(id => id.GetString() == "no-execution-to-contracts"));

        Assert.That(found, Is.True, $"expected a tagged execution -> contracts edge, stdout: {stdout}");
    }

    [Test]
    public void Graph_DotFormat_ProducesGraphvizDigraph()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--policy", _graphPolicy, "--format", "dot");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");
            Assert.That(stdout, Does.StartWith("digraph {"));
            Assert.That(stdout, Does.Contain("->"));
        });
    }

    [Test]
    public void Graph_MermaidFormat_ProducesMermaidGraph()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--policy", _graphPolicy, "--format", "mermaid");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");
            Assert.That(stdout, Does.StartWith("graph TD"));
        });
    }

    [Test]
    public void Graph_AssemblyLevel_ProducesAssemblyNodes()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--policy", _graphPolicy, "--level", "assembly");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement nodes = doc.RootElement.GetProperty("nodes");

        Assert.That(nodes.EnumerateArray().All(n => n.GetProperty("kind").GetString() == "assembly"), Is.True);
    }

    [Test]
    public void Graph_InvalidLevel_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli("graph", "--policy", _graphPolicy, "--level", "bogus");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Invalid level"));
        });
    }

    [Test]
    public void Graph_InvalidFormat_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli("graph", "--policy", _graphPolicy, "--format", "bogus");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Invalid format"));
        });
    }

    [Test]
    public void Graph_MissingPolicyFile_ExitsWithError()
    {
        var (exitCode, stdout, stderr) = RunCli("graph", "--policy", "/nonexistent/path.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Does.Contain("architecture_policy_error").And.Contain("path.yml"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void Graph_UnknownContractId_ExitsTwoWithDiagnostic()
    {
        var (exitCode, _, stderr) = RunCli(
            "graph", "--policy", _graphPolicy, "--contract", "no-execution-to-contractss");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Unknown contract IDs"));
            Assert.That(stderr, Does.Contain("no-execution-to-contractss"));
            Assert.That(stderr, Does.Contain("no-execution-to-contracts"));
        });
    }

    [Test]
    public void Graph_ValidContractId_RestrictsExecutionAndSucceeds()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "graph", "--policy", _graphPolicy, "--contract", "no-execution-to-contracts", "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement edges = doc.RootElement.GetProperty("edges");

        bool found = edges.EnumerateArray().Any(edge =>
            edge.GetProperty("source").GetString() == "ArchLinterNet.Core.Execution"
            && edge.GetProperty("target").GetString() == "ArchLinterNet.Core.Contracts"
            && edge.GetProperty("contractIds").EnumerateArray()
                .Any(id => id.GetString() == "no-execution-to-contracts"));

        Assert.That(found, Is.True, $"expected the selected contract's edge to still be tagged, stdout: {stdout}");
    }

    /* explain */

    [Test]
    public void Explain_Help_ShowsUsageAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("explain", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("explain"));
            Assert.That(stdout, Does.Contain("--source"));
            Assert.That(stdout, Does.Contain("--target"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void Explain_DirectDependency_ReportsPathAndContractId()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "explain", "--policy", _graphPolicy,
            "--source", "ArchLinterNet.Core.Execution",
            "--target", "ArchLinterNet.Core.Contracts");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");
            Assert.That(stdout, Does.Contain("ArchLinterNet.Core.Execution -> ArchLinterNet.Core.Contracts"));
            Assert.That(stdout, Does.Contain("no-execution-to-contracts"));
        });
    }

    [Test]
    public void Explain_NoPath_ReportsNoPathFoundAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "explain", "--policy", _graphPolicy,
            "--source", "ArchLinterNet.Core.Contracts",
            "--target", "ArchLinterNet.Core.NonExistent");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");
            Assert.That(stdout, Does.Contain("No dependency path found"));
        });
    }

    [Test]
    public void Explain_JsonFormat_ProducesValidJsonWithNullPathWhenUnreachable()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "explain", "--policy", _graphPolicy,
            "--source", "ArchLinterNet.Core.Contracts",
            "--target", "ArchLinterNet.Core.NonExistent",
            "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        Assert.That(doc.RootElement.GetProperty("path").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void Explain_AssemblyLevel_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli(
            "explain", "--policy", _graphPolicy,
            "--source", "ArchLinterNet.Core",
            "--target", "ArchLinterNet.Core",
            "--level", "assembly");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Assembly-level explain is not supported"));
        });
    }

    [Test]
    public void Explain_MissingSourceOrTarget_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli("explain", "--policy", _graphPolicy, "--source", "ArchLinterNet.Core.Execution");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("--source and --target are required"));
        });
    }
}
