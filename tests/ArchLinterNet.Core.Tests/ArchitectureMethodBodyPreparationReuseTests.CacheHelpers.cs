using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Profiling;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureMethodBodyPreparationReuseTests
{
    private sealed record ConcurrentPreparationWork(
        int Index,
        ArchitectureMethodBodyPreparationCache Cache,
        string RepositoryRoot,
        ArchitectureLayer Layer,
        ArchitectureMethodBodyPreparation[] Results,
        BenchmarkWorkObserver Observer)
    {
        public void Prepare()
        {
            Results[Index] = Cache.Prepare(
                RepositoryRoot,
                Layer,
                ["src/Consumer", "src/Other"],
                preprocessorSymbols: null,
                projectDiscovery: null,
                buildConfiguration: "Debug",
                cancellationToken: CancellationToken.None,
                benchmarkWorkObserver: Observer);
        }
    }

    private sealed class CancelingFileSystem(CancellationTokenSource cancellationTokenSource) : IArchitectureFileSystem
    {
        public bool FileExists(string path)
        {
            return false;
        }

        public string ReadAllText(string path)
        {
            return "namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer; internal sealed class Source { }";
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
            yield return Path.Combine(path, "First.cs");
            cancellationTokenSource.Cancel();
            yield return Path.Combine(path, "Second.cs");
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
