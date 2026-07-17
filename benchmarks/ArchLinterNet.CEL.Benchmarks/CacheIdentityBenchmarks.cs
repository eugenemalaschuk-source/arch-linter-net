using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Measures <see cref="CelCompilationKey"/> creation, equality, and hashing cost, plus cache
/// hit/miss behavior in a caller-owned cache — the pattern <c>ArchLinterNet.CEL</c> expects
/// external consumers to build themselves, since the library intentionally holds no
/// process-global cache (see the prohibited-shortcuts table in
/// <c>docs/internal/cel-engine-architecture.md</c>).
/// </summary>
/// <remarks>
/// A real caller-owned cache cannot be keyed by <see cref="CelCompilationKey"/> for the
/// "have I already compiled this?" pre-compile lookup: every component of the key
/// (<c>CelContextSchema.ComputeEnvironmentIdentity</c>, <c>CelCompilationLimits.ComputeIdentity</c>,
/// <c>CelEvaluationLimits.ComputeIdentity</c>) is internal, and the only public source of a
/// <see cref="CelCompilationKey"/> is the return value of a completed
/// <see cref="CelEnvironment.CompilePredicate"/>/<see cref="CelEnvironment.Compile"/> call — which
/// has already paid the tokenize/parse/bind cost the cache exists to avoid. The realistic pattern,
/// benchmarked below via <c>SourceKeyedCache*</c>, keys a caller-owned cache by the raw expression
/// source string instead (cheap and available before any compile call); within one
/// <see cref="CelEnvironment"/> instance the source string alone is a sufficient cache key, since
/// schema and limits are fixed for that environment's lifetime. <see cref="CelCompilationKey"/>
/// remains useful for verifying identity equivalence across environments/compiles after the fact
/// (<see cref="EqualsEquivalent"/>/<see cref="EqualsDifferentSchema"/> below), and
/// <see cref="ConstructKeyIsolated"/> isolates its own construction cost using internal access
/// (granted the same way as <c>PipelineStageBenchmarks</c>) so that cost is visible independent of
/// a full compile.
/// </remarks>
[MemoryDiagnoser]
public class CacheIdentityBenchmarks
{
    private CelEnvironment _environment = null!;
    private CelCompilationKey _keyA = null!;
    private CelCompilationKey _keyAIdentical = null!;
    private CelCompilationKey _keyDifferentSchema = null!;
    private Dictionary<string, CelCompiledPredicate> _sourceKeyedCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _environment = BenchmarkFixtures.BuildEnvironment();
        _keyA = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).CompilationKey;
        _keyAIdentical = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).CompilationKey;

        // The mismatched-schema environment only declares "source" (no "target"), so it is
        // compiled with a source-only predicate rather than RepresentativePredicateSource.
        var (mismatchedEnvironment, _) = BenchmarkFixtures.BuildMismatchedSchemaContext();
        _keyDifferentSchema = mismatchedEnvironment.CompilePredicate("source.role == 'service'").CompilationKey;

        _sourceKeyedCache = new Dictionary<string, CelCompiledPredicate>(StringComparer.Ordinal)
        {
            [BenchmarkFixtures.RepresentativePredicateSource] =
                _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).Program!,
        };
    }

    // Mirrors exactly what CelEnvironment.BuildKey computes internally on every CompilePredicate/
    // Compile call — schema/limits identity strings plus key construction — with tokenize/parse/
    // bind excluded, isolating cache-key creation cost from full-compile cost.
    [Benchmark(Description = "Isolated cache-key creation (schema/limits identity + CelCompilationKey ctor, no tokenize/parse/bind)")]
    public CelCompilationKey ConstructKeyIsolated() =>
        new(
            normalizedSource: BenchmarkFixtures.RepresentativePredicateSource,
            profileId: _environment.Profile.Id,
            schemaIdentity: _environment.Schema.ComputeEnvironmentIdentity(_environment.ObjectSchemas),
            requiredResultType: CelRequiredResultType.Predicate,
            compilationLimitsIdentity: _environment.CompilationLimits.ComputeIdentity(),
            evaluationLimitsIdentity: _environment.EvaluationLimits.ComputeIdentity());

    [Benchmark(Description = "CelCompilationKey via the only public path: full CompilePredicate call")]
    public CelCompilationKey CreateKey_ViaCompilePredicate() =>
        _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).CompilationKey;

    [Benchmark(Description = "CelCompilationKey.Equals — equivalent schema/source/limits")]
    public bool EqualsEquivalent() => _keyA.Equals(_keyAIdentical);

    [Benchmark(Description = "CelCompilationKey.Equals — different schema identity")]
    public bool EqualsDifferentSchema() => _keyA.Equals(_keyDifferentSchema);

    [Benchmark(Description = "CelCompilationKey.GetHashCode()")]
    public int GetHashCodeCost() => _keyA.GetHashCode();

    [Benchmark(Description = "Caller-owned cache keyed by source text: hit (dictionary lookup only, zero compiles)")]
    public CelCompiledPredicate SourceKeyedCacheHit() => _sourceKeyedCache[BenchmarkFixtures.RepresentativePredicateSource];

    // ComplexPredicateSource is intentionally never inserted into _sourceKeyedCache, so this
    // benchmark is a permanent, repeatable miss across every invocation/iteration — it never
    // mutates shared state, so it cannot silently turn into a hit on a later call.
    [Benchmark(Description = "Caller-owned cache keyed by source text: miss (lookup fails, falls through to compile, not inserted)")]
    public CelCompiledPredicate SourceKeyedCacheMiss()
    {
        if (_sourceKeyedCache.TryGetValue(BenchmarkFixtures.ComplexPredicateSource, out var cached))
            return cached;
        return _environment.CompilePredicate(BenchmarkFixtures.ComplexPredicateSource).Program!;
    }
}
