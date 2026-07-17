using System.Threading;
using System.Threading.Tasks;
using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Measures concurrent reuse of one immutable <see cref="CelCompiledPredicate"/> across
/// independent, per-task evaluation contexts — the shape <c>CelEnvironment</c>/
/// <c>CelCompiledPredicate</c> documents as thread-safe (build once, evaluate concurrently from
/// many threads, no shared mutable evaluation context). Each task builds and owns its own
/// <see cref="CelEvaluationContext"/>; no context is shared across threads.
/// </summary>
[MemoryDiagnoser]
public class ConcurrencyBenchmarks
{
    private const int TaskCount = 8;

    private CelEnvironment _environment = null!;
    private CelCompiledPredicate _predicate = null!;
    private CelEvaluationContext[] _perTaskContexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _environment = BenchmarkFixtures.BuildEnvironment();
        var compilation = _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource);
        _predicate = compilation.Program!;

        // BuildEnvironment() and BuildSourceTargetSchema() each construct a fresh schema, so the
        // predicate's own environment's variable handles are recovered from its Schema.Variables
        // rather than rebuilding a second, structurally-identical-but-distinct schema.
        var source = _environment.Schema.Variables[0];
        var target = _environment.Schema.Variables[1];

        // Independent contexts, one per simulated worker — no cross-thread sharing.
        _perTaskContexts = new CelEvaluationContext[TaskCount];
        for (var i = 0; i < TaskCount; i++)
            _perTaskContexts[i] = BenchmarkFixtures.BuildMatchingContext(_environment, source, target);
    }

    [Benchmark(Baseline = true, Description = "Sequential evaluation of one compiled predicate across N independent contexts")]
    public int SequentialEvaluate()
    {
        var successes = 0;
        foreach (var context in _perTaskContexts)
        {
            if (_predicate.Evaluate(context).IsSuccess)
                successes++;
        }
        return successes;
    }

    [Benchmark(Description = "Concurrent evaluation of one compiled predicate across N independent contexts (Parallel.For)")]
    public int ConcurrentEvaluate()
    {
        var successes = 0;
        Parallel.For(0, _perTaskContexts.Length, i =>
        {
            if (_predicate.Evaluate(_perTaskContexts[i]).IsSuccess)
                Interlocked.Increment(ref successes);
        });
        return successes;
    }
}
