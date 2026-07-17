using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Binding;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Isolates tokenization, parsing, and binding/type-checking cost using internal pipeline types
/// (granted via <c>InternalsVisibleTo</c>, see <c>ArchLinterNet.CEL.csproj</c>). The public
/// compilation surface (<see cref="CompilationBenchmarks"/>) only exposes the combined
/// tokenize+parse+bind cost through <c>CompilePredicate</c>/<c>Compile</c>; this class exists so
/// each internal stage's contribution to that combined cost is separately visible, per this
/// benchmark suite's acceptance criteria. No public API is added or bypassed by this isolation —
/// it only observes costs already paid inside the existing public compile path.
/// </summary>
[MemoryDiagnoser]
public class PipelineStageBenchmarks
{
    private readonly CelCompilationLimits _limits = CelCompilationLimits.SafeDefaults;
    private readonly CelProfileId _profileId = CelProfile.V1.Id;
    private CelContextSchema _schema = null!;
    private IReadOnlyDictionary<string, CelObjectSchema> _objectSchemas = null!;
    private IReadOnlyList<CelToken> _tokens = null!;
    private CelSyntaxNode _root = null!;

    // Internal pipeline result types (CelTokenizeResult/CelParseResult/CelBindResult) cannot be
    // public method return types here — BenchmarkDotNet's generated harness cannot see a type
    // less accessible than the internal-visible benchmark assembly boundary allows. Each
    // benchmark instead stashes its result kind into this field so the JIT cannot treat the call
    // as dead code and elide it.
    private bool _lastResultSucceeded;

    [GlobalSetup]
    public void Setup()
    {
        _schema = BenchmarkFixtures.BuildSourceTargetSchema(out _, out _);
        _objectSchemas = new Dictionary<string, CelObjectSchema>
        {
            ["assembly"] = BenchmarkFixtures.BuildAssemblyObjectSchema(),
        };
        _tokens = CelTokenizer.Tokenize(BenchmarkFixtures.RepresentativePredicateSource, _limits, _profileId).Tokens!;
        _root = CelParser.Parse(_tokens, _limits, _profileId).Root!;
    }

    [Benchmark(Description = "Tokenize representative predicate")]
    public bool Tokenize() =>
        _lastResultSucceeded = CelTokenizer.Tokenize(BenchmarkFixtures.RepresentativePredicateSource, _limits, _profileId).IsSuccess;

    [Benchmark(Description = "Parse pre-tokenized representative predicate")]
    public bool Parse() =>
        _lastResultSucceeded = CelParser.Parse(_tokens, _limits, _profileId).IsSuccess;

    [Benchmark(Description = "Bind pre-parsed representative predicate against generic context schema")]
    public bool Bind() =>
        _lastResultSucceeded = CelBinder.Bind(_root, _schema, _objectSchemas, CelRequiredResultType.Predicate, _profileId).IsSuccess;

    [Benchmark(Description = "Tokenize + parse + bind, staged (sum of the three isolated stages)")]
    public bool TokenizeParseBindStaged()
    {
        var tokenizeResult = CelTokenizer.Tokenize(BenchmarkFixtures.RepresentativePredicateSource, _limits, _profileId);
        var parseResult = CelParser.Parse(tokenizeResult.Tokens!, _limits, _profileId);
        return _lastResultSucceeded = CelBinder.Bind(parseResult.Root!, _schema, _objectSchemas, CelRequiredResultType.Predicate, _profileId).IsSuccess;
    }
}
