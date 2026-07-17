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
/// at batch sizes close to what a real Core integration would drive per analysis pass, which a
/// single-call benchmark cannot show.
/// </summary>
/// <remarks>
/// <see cref="BatchSize"/> is parameterized (<c>[Params]</c>), not a single fixed constant: a
/// measurement at one batch size alone can only show that no *additional* per-call overhead
/// appears at that specific size — it cannot support a "linearly-scaling" claim, since one point
/// does not establish a line. Measuring at multiple sizes and checking Mean/Allocated scale
/// proportionally with <see cref="BatchSize"/> is what actually substantiates (or refutes) linear
/// scaling; see <c>RESULTS.md</c> for the resulting per-size table and ratio check.
/// </remarks>
[MemoryDiagnoser]
public class HighVolumeEvaluationBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int BatchSize { get; set; }

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

    [Benchmark(Description = "Evaluate one compiled predicate across BatchSize independent pre-built contexts (selector-like batch workload)")]
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
