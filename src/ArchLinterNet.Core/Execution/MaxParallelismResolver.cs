namespace ArchLinterNet.Core.Execution;

// Resolves and validates the effective bounded-parallelism degree used by type loading and
// source-file fact-index materialization. See openspec/specs/bounded-parallel-scanning/spec.md,
// "A validated, bounded max-parallelism option is exposed by CLI and Testing API".
public static class MaxParallelismResolver
{
    private const int DefaultCeiling = 4;

    public static int Resolve(int? requested)
    {
        if (requested is null)
        {
            return Math.Max(1, Math.Min(Environment.ProcessorCount, DefaultCeiling));
        }

        if (requested.Value <= 0)
        {
            throw new ArgumentException(
                $"--max-parallelism must be a positive integer; got {requested.Value}.", nameof(requested));
        }

        return requested.Value;
    }
}
