using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* baseline generate */

    [Test]
    public void BaselineGenerate_WithValidPolicy_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True, "Output file should exist");
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });

            string content = File.ReadAllText(outputPath);
            Assert.That(content, Does.Contain("version: 1"));
            Assert.That(content, Does.Contain("baseline:"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithReason_Succeeds()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--reason", "my custom reason");

            Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate failed, stderr: {stderr}");
            Assert.That(File.Exists(outputPath), Is.True);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_MissingOutput_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "generate",
            "--config", _passingPolicy);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("--output"));
        });
    }

    [Test]
    public void BaselineGenerate_InvalidConfig_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", "/nonexistent/policy.yml",
                "--output", outputPath);

            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline generate"));
            Assert.That(stdout, Does.Contain("--config"));
            Assert.That(stdout, Does.Contain("--output"));
        });
    }

    /* baseline generate --mode */

    [Test]
    public void BaselineGenerate_StrictMode_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--mode", "strict");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline strict mode failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True);
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_AuditMode_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--mode", "audit");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline audit mode failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True);
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_InvalidMode_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--mode", "invalid");

            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Invalid mode"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithConditionSet_FlagParsed()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--condition-set", "nonexistent");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("condition set"));
                Assert.That(stderr, Does.Contain("nonexistent"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithInvalidPolicy_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _failingPolicy,
                "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("Configuration violations"));
                Assert.That(stderr, Does.Contain("missing target assembly"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    /* baseline generate (coverage contracts) */

    [Test]
    public void BaselineGenerate_WithCoveragePolicy_CapturesCoverageEntries()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _coveragePolicy,
                "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate failed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });

            string content = File.ReadAllText(outputPath);
            Assert.That(content, Does.Contain("strict_coverage:"));
            Assert.That(content, Does.Contain("validation-namespace-coverage"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    /* baseline generate --contract */

    [Test]
    public void BaselineGenerate_WithContract_ScopesGenerationToThatContract()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy,
                "--output", outputPath,
                "--contract", "no-execution-to-contracts");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate with --contract failed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });

            string content = File.ReadAllText(outputPath);
            Assert.That(content, Does.Contain("no-execution-to-contracts"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithUnknownContract_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy,
                "--output", outputPath,
                "--contract", "missing-contract-id");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("Unknown contract IDs"));
                Assert.That(stderr, Does.Contain("missing-contract-id"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithPolicyAlias_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--policy", _passingPolicy,
                "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True);
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    /* baseline --help */

    [Test]
    public void BaselineHelp_ShowsModeOption()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("--mode"));
            Assert.That(stdout, Does.Contain("strict, audit, or all"));
            Assert.That(stdout, Does.Contain("--condition-set"));
        });
    }
}
