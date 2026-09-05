using System.Text.Json;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Topology;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Focused proof of the public capture seam. These tests intentionally use a policy without a
// declared topology: capture is an observation/review operation and must remain useful before a
// topology has been authored.
[TestFixture]
public sealed class ArchitectureTopologyCaptureTests
{
    private string _repositoryRoot = null!;
    private string _policyPath = null!;

    private static string AssemblyName => typeof(ArchitectureTopologyCaptureTests).Assembly.GetName().Name!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_repositoryRoot, "architecture"));
        _policyPath = Path.Combine(_repositoryRoot, "architecture", "dependencies.arch.yml");
        File.WriteAllText(_policyPath, $"""
            version: 1
            name: Capture tests

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict: []
              audit: []
            """);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, recursive: true);
        }
    }

    [Test]
    public void Capture_IsByteStableForUnchangedInputs()
    {
        using ArchitectureEngine engine = CreateEngine();

        ArchitectureTopologyCaptureOutcome first = Capture(engine, "type");
        ArchitectureTopologyCaptureOutcome second = Capture(engine, "type");

        JsonSerializerOptions options = new() { WriteIndented = false };
        Assert.That(JsonSerializer.Serialize(first, options), Is.EqualTo(JsonSerializer.Serialize(second, options)));
        Assert.That(first.PolicyImportPaths, Is.EqualTo(new[] { Path.GetFullPath(_policyPath) }));
    }

    [Test]
    public void Capture_ReportsCanonicalRootAndImportedPolicyPaths()
    {
        string fragmentPath = Path.Combine(_repositoryRoot, "architecture", "topology-fragment.yml");
        File.WriteAllText(fragmentPath, "layers: {}\n");
        File.WriteAllText(_policyPath, $"""
            version: 1
            name: Capture imports
            imports: [topology-fragment.yml]

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict: []
              audit: []
            """);
        using ArchitectureEngine engine = CreateEngine();

        ArchitectureTopologyCaptureOutcome outcome = Capture(engine, "assembly");

        Assert.That(outcome.PolicyImportPaths, Is.EqualTo(new[]
        {
            Path.GetFullPath(_policyPath),
            Path.GetFullPath(fragmentPath),
        }));
    }

    [TestCase("type")]
    [TestCase("namespace")]
    [TestCase("project")]
    [TestCase("assembly")]
    public void Capture_WorksForEverySupportedSubjectKind(string subjectKind)
    {
        using ArchitectureEngine engine = CreateEngine();

        ArchitectureTopologyCaptureOutcome outcome = Capture(engine, subjectKind);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.PreflightBlocked, Is.False);
            Assert.That(outcome.SubjectKind, Is.EqualTo(subjectKind));
            Assert.That(outcome.Subjects, Is.Not.Empty);
            Assert.That(
                outcome.Subjects.Select(subject => subject.Identity),
                Is.EqualTo(outcome.Subjects.Select(subject => subject.Identity)
                    .OrderBy(identity => identity, StringComparer.Ordinal)));
            Assert.That(
                outcome.Subjects.All(subject =>
                    !subject.Identity.Contains("canonical_assembly=", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                outcome.Relationships.Select(relationship =>
                    $"{relationship.SourceIdentity}\u001f{relationship.TargetIdentity}\u001f{relationship.Witness}"),
                Is.EqualTo(outcome.Relationships.Select(relationship =>
                        $"{relationship.SourceIdentity}\u001f{relationship.TargetIdentity}\u001f{relationship.Witness}")
                    .OrderBy(identity => identity, StringComparer.Ordinal)));
        });
    }

    private ArchitectureTopologyCaptureOutcome Capture(
        ArchitectureEngine engine,
        string subjectKind) =>
        engine.CaptureTopology(new ArchitectureTopologyCaptureRequest
        {
            PolicyPath = _policyPath,
            SubjectKind = subjectKind,
        });

    private static ArchitectureEngine CreateEngine() =>
        new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
}
