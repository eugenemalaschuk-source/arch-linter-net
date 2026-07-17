using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Measures <see cref="CelCompilationKey"/> creation, equality, and hashing cost, plus cache
/// hit/miss behavior in a caller-owned <see cref="Dictionary{TKey,TValue}"/> cache — the pattern
/// <c>ArchLinterNet.CEL</c> expects external consumers to build themselves, since the library
/// intentionally holds no process-global cache (see the prohibited-shortcuts table in
/// <c>docs/internal/cel-engine-architecture.md</c>).
/// </summary>
[MemoryDiagnoser]
public class CacheIdentityBenchmarks
{
    private CelEnvironment _environment = null!;
    private CelCompilationKey _keyA = null!;
    private CelCompilationKey _keyAIdentical = null!;
    private CelCompilationKey _keyDifferentSchema = null!;
    private Dictionary<CelCompilationKey, CelCompiledPredicate> _warmCache = null!;

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

        _warmCache = new Dictionary<CelCompilationKey, CelCompiledPredicate>
        {
            [_keyA] = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).Program!,
        };
    }

    [Benchmark(Description = "Create CelCompilationKey via full CompilePredicate call")]
    public CelCompilationKey CreateKey() =>
        _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).CompilationKey;

    [Benchmark(Description = "CelCompilationKey.Equals — equivalent schema/source/limits")]
    public bool EqualsEquivalent() => _keyA.Equals(_keyAIdentical);

    [Benchmark(Description = "CelCompilationKey.Equals — different schema identity")]
    public bool EqualsDifferentSchema() => _keyA.Equals(_keyDifferentSchema);

    [Benchmark(Description = "CelCompilationKey.GetHashCode()")]
    public int GetHashCodeCost() => _keyA.GetHashCode();

    [Benchmark(Description = "Caller-owned Dictionary cache: hit (compile skipped by caller)")]
    public CelCompiledPredicate CacheHit() => _warmCache[_keyAIdentical];

    [Benchmark(Description = "Caller-owned Dictionary cache: miss, falls through to CompilePredicate")]
    public CelCompiledPredicate CacheMiss()
    {
        if (_warmCache.TryGetValue(_keyDifferentSchema, out var cached))
            return cached;
        return _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).Program!;
    }
}
