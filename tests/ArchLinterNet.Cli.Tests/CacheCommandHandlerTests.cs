using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Cache;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Issue #365's `cache inspect`/`cache clear` — see openspec/specs/analysis-cache/spec.md,
// "Inspect and clear operations are safe and deterministic".
//
// This assembly runs with [assembly: Parallelizable(ParallelScope.All)] (see AssemblyInfo.cs), so
// each test creates and tears down its own uniquely-named temp directory locally instead of
// relying on shared [SetUp]/[TearDown] instance state, which would race across concurrently
// running test methods (see ContextualContractCliTests.cs for the same convention).
[TestFixture]
public sealed class CacheCommandHandlerTests
{
    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-command-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfExists(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeCliConsole : ICliConsole
    {
        private readonly StringBuilder _stdout = new();
        private readonly StringBuilder _stderr = new();

        public TextWriter Out => new StringWriter(_stdout);
        public TextWriter Error => new StringWriter(_stderr);
        public string StdOut => _stdout.ToString();
        public string StdErr => _stderr.ToString();
    }

    [Test]
    public void Inspect_EmptyCache_ReturnsEmptyDeterministicArray()
    {
        string root = CreateTempRoot();
        try
        {
            FakeCliConsole console = new();
            CacheCommandHandler handler = new(console);

            int exitCode = handler.Inspect(root, showHelp: false);

            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StdOut.Trim(), Is.EqualTo("[]"));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Test]
    public void Inspect_PopulatedCache_ReportsEntryWithoutAbsolutePath()
    {
        string root = CreateTempRoot();
        try
        {
            AnalysisCacheLocation location = new(root, AnalysisCacheMode.ExplicitPath);
            AnalysisCacheStore.Put(
                location,
                new AnalysisCacheKey("repo", "policy", "strict", null, "contracts", null, null, null, null),
                new[] { new AnalysisCacheProjectManifest("src/A/A.csproj", "digest", CacheEligibility.VerifiedCacheEligible) },
                new AnalysisCacheFactsV1(true, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1));

            FakeCliConsole console = new();
            CacheCommandHandler handler = new(console);

            int exitCode = handler.Inspect(root, showHelp: false);

            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StdOut, Does.Contain("\"Readable\": true"));
            Assert.That(console.StdOut, Does.Not.Contain(root));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Test]
    public void Clear_RemovesEntries()
    {
        string root = CreateTempRoot();
        try
        {
            AnalysisCacheLocation location = new(root, AnalysisCacheMode.ExplicitPath);
            AnalysisCacheStore.Put(
                location,
                new AnalysisCacheKey("repo", "policy", "strict", null, "contracts", null, null, null, null),
                new[] { new AnalysisCacheProjectManifest("src/A/A.csproj", "digest", CacheEligibility.VerifiedCacheEligible) },
                new AnalysisCacheFactsV1(true, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1));

            FakeCliConsole console = new();
            CacheCommandHandler handler = new(console);

            int exitCode = handler.Clear(root, showHelp: false);

            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(AnalysisCacheStore.Inspect(location), Is.Empty);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Test]
    public void Inspect_MissingCacheDestination_IsRuntimeError()
    {
        FakeCliConsole console = new();
        CacheCommandHandler handler = new(console);

        int exitCode = handler.Inspect(null, showHelp: false);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("--cache <auto|path> is required"));
        });
    }

    [Test]
    public void Inspect_UnsafeCacheDestination_IsRuntimeError()
    {
        string unsafePath = OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";
        FakeCliConsole console = new();
        CacheCommandHandler handler = new(console);

        int exitCode = handler.Inspect(unsafePath, showHelp: false);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    [Test]
    public void ShowHelp_PrintsHelpTextAndSucceeds()
    {
        FakeCliConsole console = new();
        CacheCommandHandler handler = new(console);

        int exitCode = handler.Inspect(null, showHelp: true);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.StdOut, Does.Contain("arch-linter-net cache"));
    }
}
