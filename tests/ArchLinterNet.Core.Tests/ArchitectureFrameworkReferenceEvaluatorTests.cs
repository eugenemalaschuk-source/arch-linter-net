using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureFrameworkReferenceEvaluatorTests
{
    [Test]
    public void Evaluate_ProjectFileDoesNotExist_ReturnsFailure()
    {
        string projectPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.csproj");

        ArchitectureFrameworkReferenceEvaluationResult result = new ArchitectureFrameworkReferenceEvaluator().Evaluate(projectPath);

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
                new ArchitectureFrameworkReferenceEvaluator().Evaluate(projectPath);

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
