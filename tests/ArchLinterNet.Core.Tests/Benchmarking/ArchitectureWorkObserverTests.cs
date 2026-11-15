using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
public sealed class ArchitectureWorkObserverTests
{
    [Test]
    public void ProjectFileParser_ObserverCountsParseWithoutChangingResult()
    {
        string root = Path.Combine(Path.GetTempPath(), "arch-linter-observer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "Sample.csproj");
        File.WriteAllText(projectPath, "<Project><PropertyGroup><AssemblyName>Sample</AssemblyName></PropertyGroup></Project>");

        try
        {
            ArchitectureProjectFileParser parser = new();
            DiscoveredProjectFile expected = parser.Parse(projectPath);
            BenchmarkWorkObserver observer = new();
            DiscoveredProjectFile actual = parser.Parse(projectPath, ArchitectureFileSystem.Real, observer);

            Assert.Multiple(() =>
            {
                Assert.That(actual.AssemblyName, Is.EqualTo(expected.AssemblyName));
                Assert.That(actual.TargetFrameworks, Is.EqualTo(expected.TargetFrameworks));
                Assert.That(actual.PackageReferences, Is.EqualTo(expected.PackageReferences));
                Assert.That(actual.FrameworkReferences, Is.EqualTo(expected.FrameworkReferences));
                Assert.That(actual.ProjectReferences, Is.EqualTo(expected.ProjectReferences));
            });
            Assert.That(observer.ProjectFileParses, Is.EqualTo(1));
            Assert.That(observer.Snapshot()[nameof(BenchmarkWorkObserver.ProjectFileParses)], Is.EqualTo(1));
            Assert.That(observer.SourceFactFileParses, Is.Zero);

            ArchitectureContractDocument document = new()
            {
                Analysis = new ArchitectureAnalysisConfiguration { Projects = ["Sample.csproj"] },
            };
            ProjectDiscoveryResult expectedDiscovery = new ArchitectureProjectDiscoveryService()
                .ResolveFromDocument(document, root, resolveAssemblyOutputs: false);
            BenchmarkWorkObserver discoveryObserver = new();
            ProjectDiscoveryResult observedDiscovery = new ArchitectureProjectDiscoveryService()
                .ResolveAndApply(document, root, resolveAssemblyOutputs: false, default, discoveryObserver);

            Assert.That(observedDiscovery.DiscoveredProjects.Select(project => project.AssemblyName),
                Is.EqualTo(expectedDiscovery.DiscoveredProjects.Select(project => project.AssemblyName)));
            Assert.That(discoveryObserver.ProjectFileParses, Is.EqualTo(1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void SourceFactIndex_ObserverCountsActualSourceParsesAndPreservesFacts()
    {
        const string Source = "namespace ArchLinterNet.Core.Tests.Benchmarking; public sealed class SourceFixture { }";
        const string Root = "/fake/work-observer";
        FakeArchitectureFileSystem fileSystem = new();
        fileSystem.AddDirectory(Root);
        fileSystem.AddDirectory(Root + "/src");
        fileSystem.AddFile(Root + "/src/SourceFixture.cs", Source, DateTime.UtcNow);
        BenchmarkWorkObserver observer = new();

        ArchitectureSourceFileFactIndex Build(BenchmarkWorkObserver? workObserver) =>
            new(
                [typeof(ArchitectureWorkObserverTests).Assembly], Root, ["src"], null, fileSystem,
                new ArchitectureSourceFileFactIndex.ProjectOwnership(null,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["src"] = "ArchLinterNet.Core.Tests" }),
                new ArchitectureSourceFileFactIndex.ConstructionOptions(
                    ProfilingCounters: null, CancellationToken: default, BenchmarkWorkObserver: workObserver));

        ArchitectureSourceFileFactIndex baseline = Build(null);
        ArchitectureSourceFileFactIndex observed = Build(observer);
        string[] baselineFacts = baseline.AllFacts.Select(fact => fact.FullTypeName).ToArray();
        string[] observedFacts = observed.AllFacts.Select(fact => fact.FullTypeName).ToArray();

        Assert.That(observedFacts, Is.EqualTo(baselineFacts));
        Assert.That(observer.SourceFactFileParses, Is.EqualTo(1));
        Assert.That(observer.ProjectFileParses, Is.Zero);
    }

    [Test]
    public void MethodBodyScanner_ObserverCountsCompilationTreesAndSemanticLookupsWithoutChangingViolations()
    {
        string root = Path.Combine(Path.GetTempPath(), "arch-linter-observer-" + Guid.NewGuid().ToString("N"));
        string sourceRoot = Path.Combine(root, "src", "Sample");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Calls.cs"),
            "namespace Sample.Domain;\npublic sealed class Calls { public void Run() { System.Console.WriteLine(\"x\"); } }");

        try
        {
            ArchitectureContractExecutionContext executionContext = new(
                "method-body", "observer-test", Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
            ArchitectureSourceScanner scanner = new();
            ArchitectureLayer layer = new() { Namespace = "Sample.Domain" };
            string[] patterns = ["Console.WriteLine"];
            IReadOnlyList<ArchitectureViolation> expected = scanner.FindMethodBodyViolations(
                root, layer.Namespace, patterns, executionContext, ["src"], layer).ToArray();
            BenchmarkWorkObserver observer = new();
            IReadOnlyList<ArchitectureViolation> actual = scanner.FindMethodBodyViolationsWithObserver(
                root, layer.Namespace, patterns, executionContext, ["src"], layer,
                preprocessorSymbols: null, fileSystem: ArchitectureFileSystem.Real,
                compilationFactory: null, assemblyLoader: null, explicitReferenceAssemblyPaths: null,
                sourceAssemblyHint: null, cancellationToken: default, benchmarkWorkObserver: observer).ToArray();

            static string[] Describe(IEnumerable<ArchitectureViolation> violations) => violations
                .Select(violation => string.Join("|", violation.ContractName, violation.ContractId,
                    violation.SourceType, violation.ForbiddenNamespace,
                    string.Join(";", violation.ForbiddenReferences)))
                .ToArray();

            Assert.That(Describe(actual), Is.EqualTo(Describe(expected)));
            Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(1));
            Assert.That(observer.MethodBodySyntaxTrees, Is.EqualTo(1));
            Assert.That(observer.AstSemanticLookups, Is.GreaterThan(0));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void CelContextFactory_ObserverCountsContextConstructionAndLeavesUnexercisedCountsAbsent()
    {
        BenchmarkWorkObserver observer = new();
        var expected = ArchitectureExpressionContextFactory.CreateSelectorContext(CreateSubject());
        var actual = ArchitectureExpressionContextFactory.CreateSelectorContext(CreateSubject(), observer);

        Assert.That(actual.Assignments.Count, Is.EqualTo(expected.Assignments.Count));
        Assert.That(observer.CelContextConstructions, Is.EqualTo(1));
        Assert.That(observer.LayerMembershipProbes, Is.Zero);
        Assert.That(observer.Snapshot().ContainsKey(nameof(BenchmarkWorkObserver.LayerMembershipProbes)), Is.False);
    }

    [Test]
    public void CheckerContext_ObserverCountsActualLayerMembershipProbesAndPreservesResolution()
    {
        string namespaceName = typeof(ArchitectureWorkObserverTests).Namespace!;
        ArchitectureContractDocument document = new() { Version = 1, Name = "observer-test" };
        document.Layers["first"] = new ArchitectureLayer { Namespace = namespaceName };
        document.Layers["second"] = new ArchitectureLayer { Namespace = "No.Match" };

        string? Resolve(BenchmarkWorkObserver? observer, CountingReadOnlySet candidateLayerNames)
        {
            ArchitectureAnalysisContext analysisContext = new(
                "/tmp", [typeof(ArchitectureWorkObserverTests).Assembly], Array.Empty<string>(), Array.Empty<string>())
            {
                BenchmarkWorkObserver = observer,
            };
            ArchitectureAnalysisSession session = new(analysisContext, document, null, false, null);
            return session.CheckerContext.ResolveContainingLayer(typeof(ArchitectureWorkObserverTests), candidateLayerNames);
        }

        CountingReadOnlySet unobservedCandidates = new(["first", "second"]);
        string? expected = Resolve(null, unobservedCandidates);
        BenchmarkWorkObserver benchmarkWorkObserver = new();
        CountingReadOnlySet observedCandidates = new(["first", "second"]);
        string? actual = Resolve(benchmarkWorkObserver, observedCandidates);

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(benchmarkWorkObserver.LayerMembershipProbes, Is.EqualTo(2));
        Assert.That(observedCandidates.CountReads, Is.GreaterThan(unobservedCandidates.CountReads),
            "The observer-enabled path should read the candidate count for counter recording; the disabled path should skip that counter-only pass.");
    }

    private sealed class CountingReadOnlySet(IEnumerable<string> values) : IReadOnlySet<string>
    {
        private readonly HashSet<string> _values = new(values, StringComparer.Ordinal);

        public int CountReads { get; private set; }

        public int Count
        {
            get
            {
                CountReads++;
                return _values.Count;
            }
        }

        public bool Contains(string item) => _values.Contains(item);

        public bool IsProperSubsetOf(IEnumerable<string> other) => _values.IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<string> other) => _values.IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<string> other) => _values.IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<string> other) => _values.IsSupersetOf(other);

        public bool Overlaps(IEnumerable<string> other) => _values.Overlaps(other);

        public bool SetEquals(IEnumerable<string> other) => _values.SetEquals(other);

        public IEnumerator<string> GetEnumerator() => _values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static ArchitectureExpressionSubjectFacts CreateSubject() => new(
        "Sample.Domain.Type", "Type", "Sample.Domain", "Sample", "Sample", "Domain",
        new Dictionary<string, string>(), new Dictionary<string, bool>(), "class", false, false,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
}
