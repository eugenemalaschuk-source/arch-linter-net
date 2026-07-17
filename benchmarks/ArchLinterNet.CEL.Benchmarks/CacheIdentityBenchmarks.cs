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
/// source string instead (cheap and available before any compile call), <b>scoped to one
/// compilation kind</b>: one <see cref="CelEnvironment"/> instance fixes schema and limits for its
/// entire lifetime, but it does not fix the choice between <c>CompilePredicate</c> and
/// <c>Compile</c> — the same source text compiled both ways from the same environment produces two
/// different results (<see cref="CelCompiledPredicate"/> vs. <c>CelCompiledExpression</c>,
/// distinguished by <c>CelCompilationKey.RequiredResultType</c>). Source text alone is a sufficient
/// key only when the cache is already scoped to one kind — the
/// <c>Dictionary&lt;string, CelCompiledPredicate&gt;</c> used below is exactly that: its value type
/// makes it predicate-only by construction, so it cannot silently accept a <c>Compile()</c> result.
/// A caller needing one shared cache for both kinds instead needs a
/// <c>(source, requiredResultType)</c> composite key. <see cref="CelCompilationKey"/> remains useful
/// for verifying identity equivalence across environments/compiles after the fact
/// (<see cref="EqualsEquivalent"/>/<see cref="EqualsDifferentSchema"/> below), and
/// <see cref="ConstructKeyIsolated"/> isolates its own construction cost using internal access
/// (granted the same way as <c>PipelineStageBenchmarks</c>) so that cost is visible independent of
/// a full compile.
/// </remarks>
[MemoryDiagnoser]
public class CacheIdentityBenchmarks
{
    // Deliberately declared with no "target" reference so it compiles under both the full
    // source/target schema and the mismatched (source-only) schema below — see SchemaComparisonKeyA
    // / SchemaComparisonKeyB in Setup: EqualsDifferentSchema needs the exact same NormalizedSource
    // string on both keys, or CelCompilationKey.Equals' NormalizedSource check (checked first, and
    // short-circuiting) exits before ever reaching the SchemaIdentity comparison this benchmark
    // means to measure.
    private const string SourceOnlyPredicateSource = "source.role == 'service'";

    private CelEnvironment _environment = null!;
    private CelCompilationKey _keyA = null!;
    private CelCompilationKey _keyAIdentical = null!;
    private CelCompilationKey _schemaComparisonKeyA = null!;
    private CelCompilationKey _schemaComparisonKeyB = null!;
    private Dictionary<string, CelCompiledPredicate> _warmSourceKeyedCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _environment = BenchmarkFixtures.BuildEnvironment();
        _keyA = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).CompilationKey;
        _keyAIdentical = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).CompilationKey;

        // Same NormalizedSource (SourceOnlyPredicateSource) compiled under two structurally
        // different schemas — the full source/target schema (with its object-schema catalog) vs.
        // the mismatched schema (source-only, no catalog) — so Equals genuinely reaches and compares
        // SchemaIdentity instead of short-circuiting on a NormalizedSource mismatch.
        _schemaComparisonKeyA = _environment.CompilePredicate(SourceOnlyPredicateSource).CompilationKey;
        var (mismatchedEnvironment, _) = BenchmarkFixtures.BuildMismatchedSchemaContext();
        _schemaComparisonKeyB = mismatchedEnvironment.CompilePredicate(SourceOnlyPredicateSource).CompilationKey;

        // Hit and miss (SourceKeyedCacheHit / SourceKeyedCacheMissAndPopulate below) both look
        // up/compile the exact same expression (RepresentativePredicateSource), so the hit-vs-miss
        // cost comparison is apples-to-apples, not skewed by comparing a short expression's hit
        // against a longer expression's miss.
        _warmSourceKeyedCache = new Dictionary<string, CelCompiledPredicate>(StringComparer.Ordinal)
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

    // _schemaComparisonKeyA/B share the exact same NormalizedSource, so Equals cannot short-circuit
    // on that first check — this measures the SchemaIdentity string comparison itself, not a fast
    // exit from differing source text.
    [Benchmark(Description = "CelCompilationKey.Equals — different schema identity, same source text")]
    public bool EqualsDifferentSchema() => _schemaComparisonKeyA.Equals(_schemaComparisonKeyB);

    [Benchmark(Description = "CelCompilationKey.GetHashCode()")]
    public int GetHashCodeCost() => _keyA.GetHashCode();

    // Both hit and miss use TryGetValue (not the indexer) — using the indexer for the miss path
    // would throw KeyNotFoundException on every call, adding CLR exception overhead that has
    // nothing to do with the cache-state cost this comparison is meant to isolate.
    [Benchmark(Description = "Caller-owned cache keyed by source text: hit for RepresentativePredicateSource (TryGetValue, zero compiles)")]
    public CelCompiledPredicate? SourceKeyedCacheHit()
    {
        _warmSourceKeyedCache.TryGetValue(BenchmarkFixtures.RepresentativePredicateSource, out var cached);
        return cached;
    }

    // A fresh, empty dictionary per invocation (not a shared field never populated after Setup):
    // this models the *full* miss-and-populate path a real caller runs — failed TryGetValue,
    // compile, then insert into the cache for future reuse — including the dictionary's own
    // allocation and the 0-to-1-entry resize/insert cost, not just the lookup-then-compile cost
    // with caching silently skipped.
    [Benchmark(Description = "Caller-owned cache keyed by source text: full miss-and-populate path (fresh dict, TryGetValue miss, compile, insert)")]
    public CelCompiledPredicate SourceKeyedCacheMissAndPopulate()
    {
        var cache = new Dictionary<string, CelCompiledPredicate>(StringComparer.Ordinal);
        if (cache.TryGetValue(BenchmarkFixtures.RepresentativePredicateSource, out var cached))
            return cached;
        var compiled = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).Program!;
        cache[BenchmarkFixtures.RepresentativePredicateSource] = compiled;
        return compiled;
    }
}
