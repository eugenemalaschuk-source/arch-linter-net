using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureMethodBodyPreparationReuseTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void CheckMethodBodyContract_DifferentNamespaceSuffixesKeepSeparateInventories(bool consumerLayerFirst)
    {
        const string NamespaceRoot = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets";
        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["consumer"] = new() { Namespace = NamespaceRoot, NamespaceSuffix = "Consumer" },
            ["other"] = new() { Namespace = NamespaceRoot, NamespaceSuffix = "Other" },
        };
        ArchitectureAnalysisSession session = CreateSession(new BenchmarkWorkObserver(), layers);
        try
        {
            string[] order = consumerLayerFirst ? ["consumer", "other"] : ["other", "consumer"];
            foreach (string layerName in order)
            {
                List<ArchitectureViolation> violations = session.CheckMethodBodyContract(
                    CreateContract(layerName, layerName, "System.Console.WriteLine"));

                Assert.That(
                    violations.Any(violation => violation.SourceType.EndsWith(
                        layerName == "consumer" ? "src/Consumer/Caller.cs" : "src/Other/Caller.cs",
                        StringComparison.Ordinal)),
                    Is.True,
                    $"Layer '{layerName}' must scan only its own source namespace suffix.");
            }
        }
        finally
        {
            session.Context.Dispose();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CheckMethodBodyContract_DifferentNamespaceExclusionsKeepSeparateInventories(bool restrictedLayerFirst)
    {
        const string NamespaceRoot = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets";
        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["broad"] = new()
            {
                Namespace = NamespaceRoot + ".Consumer",
            },
            ["restricted"] = new()
            {
                Namespace = NamespaceRoot + ".Consumer",
                Exclude = [new ArchitectureLayerExclusion
                {
                    Namespace = NamespaceRoot + ".Consumer.Secret",
                }],
            },
        };
        ArchitectureAnalysisSession session = CreateSession(new BenchmarkWorkObserver(), layers);
        try
        {
            string[] order = restrictedLayerFirst ? ["restricted", "broad"] : ["broad", "restricted"];
            foreach (string layerName in order)
            {
                List<ArchitectureViolation> violations = session.CheckMethodBodyContract(
                    CreateContract(
                        layerName,
                        layerName,
                        "System.Console.WriteLine",
                        "System.Diagnostics.Debug.Fail"));
                bool secretFileWasReported = violations.Any(violation =>
                    violation.SourceType.EndsWith("src/Consumer/Secret/SecretCaller.cs", StringComparison.Ordinal));

                Assert.That(
                    secretFileWasReported,
                    Is.EqualTo(layerName == "broad"),
                    $"Layer '{layerName}' must preserve its own exclude rules in either evaluation order.");
            }
        }
        finally
        {
            session.Context.Dispose();
        }
    }

    [Test]
    public void SourceInventory_ReadAllTextFailureAfterNamespaceMatch_PropagatesIOException()
    {
        ReadAllTextFailingFileSystem fileSystem = new();
        ArchitectureLayer layer = new()
        {
            Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer",
        };
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "arch-linter-method-body-read-failure");

        Assert.Throws<IOException>(() => ArchitectureSourceScanner.FindMatchingSourceFileInputs(
            repositoryRoot,
            layer,
            ["src"],
            fileSystem,
            CancellationToken.None,
            benchmarkWorkObserver: null));
    }

    private sealed class ReadAllTextFailingFileSystem : IArchitectureFileSystem
    {
        public bool FileExists(string path)
        {
            return false;
        }

        public string ReadAllText(string path)
        {
            throw new IOException("Injected source read failure.");
        }

        public IEnumerable<string> ReadLines(string path)
        {
            return ["namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer;"];
        }

        public bool DirectoryExists(string path)
        {
            return true;
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return [Path.Combine(path, "ReadFailure.cs")];
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return [];
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            return DateTime.UnixEpoch;
        }

        public string GetCurrentDirectory()
        {
            return Path.GetTempPath();
        }
    }
}
