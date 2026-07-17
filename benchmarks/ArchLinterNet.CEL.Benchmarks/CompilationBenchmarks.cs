using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Measures the full public compilation pipeline (tokenize + parse + bind, reached only through
/// <see cref="CelEnvironment.CompilePredicate"/>/<see cref="CelEnvironment.Compile"/>) and the
/// overhead difference between the predicate-convenience path and the general-expression path.
/// </summary>
[MemoryDiagnoser]
public class CompilationBenchmarks
{
    private CelEnvironment _environment = null!;

    [GlobalSetup]
    public void Setup()
    {
        _environment = BenchmarkFixtures.BuildEnvironment();
    }

    [Benchmark(Description = "CompilePredicate: full public pipeline, representative expression")]
    public CelCompilationResult<CelCompiledPredicate> CompilePredicate() =>
        _environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource);

    [Benchmark(Description = "CompilePredicate: full public pipeline, longer expression")]
    public CelCompilationResult<CelCompiledPredicate> CompilePredicateComplex() =>
        _environment.CompilePredicate(BenchmarkFixtures.ComplexPredicateSource);

    [Benchmark(Description = "Compile: general-expression path, same source required to type-check as Bool")]
    public CelCompilationResult<CelCompiledExpression> CompileGeneralExpression() =>
        _environment.Compile(BenchmarkFixtures.RepresentativePredicateSource);

    // CelBinder fails fast internally via a caught exception (see CelBinder.Bind's remarks) to
    // produce one structured diagnostic per attempt. BenchmarkDotNet's exception counter reports
    // this as "Exceptions: 1" even though nothing propagates out of CompilePredicate — that is
    // this path's real, intended cost (CLR exception unwinding included), not a benchmark defect.
    [Benchmark(Description = "CompilePredicate: failing compilation (undeclared member), diagnostic overhead")]
    public CelCompilationResult<CelCompiledPredicate> CompilePredicateFailure() =>
        _environment.CompilePredicate(BenchmarkFixtures.UnboundMemberPredicateSource);
}
