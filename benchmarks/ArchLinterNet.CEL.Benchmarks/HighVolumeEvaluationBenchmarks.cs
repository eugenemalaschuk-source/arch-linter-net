using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// High-volume evaluation of one compiled predicate over many independent contexts in a single
/// measured batch — representative of a type/edge selector workload (many architecture facts
/// evaluated against one compiled policy expression), per #168's required scope item "high-volume
/// evaluations representative of type/edge selector workloads without introducing Core
/// dependencies." <see cref="EvaluationBenchmarks"/> measures one evaluation at a time; this class
/// exists specifically to surface GC pressure (Gen0/Gen1 collection counts, total allocated bytes)
/// at a batch size close to what a real Core integration would drive per analysis pass, which a
/// single-call benchmark cannot show.
/// </summary>
[MemoryDiagnoser]
public class HighVolumeEvaluationBenchmarks
{
    private const int BatchSize = 10_000;

    private CelCompiledPredicate _predicate = null!;
    private CelEvaluationContext[] _contexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        var environment = BenchmarkFixtures.BuildEnvironment();
        _predicate = environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource).Program!;

        // Independent contexts, precomputed once here (not inside the timed batch below), so the
        // measured method's allocation reflects only Evaluate()'s own per-call cost at scale, not
        // context construction repeated BatchSize times.
        var source = environment.Schema.Variables[0];
        var target = environment.Schema.Variables[1];
        var sourceValue = BenchmarkFixtures.BuildMatchingSourceValue();
        var targetValue = BenchmarkFixtures.BuildMatchingTargetValue();
        _contexts = new CelEvaluationContext[BatchSize];
        for (var i = 0; i < BatchSize; i++)
            _contexts[i] = BenchmarkFixtures.BuildContextFromValues(environment, source, target, sourceValue, targetValue);
    }

    [Benchmark(Description = "Evaluate one compiled predicate across 10,000 independent pre-built contexts (selector-like batch workload)")]
    public int EvaluateBatch()
    {
        var successCount = 0;
        foreach (var context in _contexts)
        {
            if (_predicate.Evaluate(context).IsSuccess)
                successCount++;
        }
        return successCount;
    }
}
