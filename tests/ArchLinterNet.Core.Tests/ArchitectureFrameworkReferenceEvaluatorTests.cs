using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureFrameworkReferenceEvaluatorTests
{
    [Test]
    public void HasRestoredTargets_ProjectPathHasNoDirectory_ReturnsFalse()
    {
        Assert.That(ArchitectureFrameworkReferenceEvaluator.HasRestoredTargets(string.Empty), Is.False);
    }

    [Test]
    public void HasRestoredTargets_AssetsFileDoesNotExist_ReturnsFalse()
    {
        string projectPath = Path.Combine(Path.GetTempPath(), $"arch-linter-no-assets-{Guid.NewGuid():N}", "Fixture.csproj");

        Assert.That(ArchitectureFrameworkReferenceEvaluator.HasRestoredTargets(projectPath), Is.False);
    }

    [TestCase("{}", false)]
    [TestCase("[]", false)]
    [TestCase("{\"targets\":[]}", false)]
    [TestCase("{\"targets\":{}}", false)]
    [TestCase("{\"targets\":{\"net10.0\":{}}}", true)]
    [TestCase("not-json", false)]
    public void HasRestoredTargets_AssetsContentIsValidated(string assetsContent, bool expected)
    {
        string repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-assets-{Guid.NewGuid():N}");
        string objDirectory = Path.Combine(repoRoot, "obj");
        Directory.CreateDirectory(objDirectory);

        try
        {
            string projectPath = Path.Combine(repoRoot, "Fixture.csproj");
            File.WriteAllText(Path.Combine(objDirectory, "project.assets.json"), assetsContent);

            Assert.That(ArchitectureFrameworkReferenceEvaluator.HasRestoredTargets(projectPath), Is.EqualTo(expected));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Test]
    public void Evaluate_ProjectFileDoesNotExist_ReturnsFailure()
    {
        string projectPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.csproj");

        ArchitectureFrameworkReferenceEvaluationResult result = new ArchitectureFrameworkReferenceEvaluator().Evaluate(projectPath, "Debug");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failures, Has.Count.EqualTo(1));
        Assert.That(result.Failures[0].Reason, Does.Contain("does not exist"));
        Assert.That(result.References, Is.Empty);
    }

    [Test]
    public void Evaluate_MalformedProjectFile_ReturnsFailureInsteadOfThrowing()
    {
        string repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-malformed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);
        try
        {
            string projectPath = Path.Combine(repoRoot, "Malformed.csproj");
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><Unclosed>");

            ArchitectureFrameworkReferenceEvaluationResult result =
                new ArchitectureFrameworkReferenceEvaluator().Evaluate(projectPath, "Debug");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failures, Is.Not.Empty);
            Assert.That(result.References, Is.Empty);
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }
}
