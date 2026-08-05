namespace ArchLinterNet.Core.Validation;

// An inert, opt-in probe for the release gate's process-level cancellation test. The marker is
// created only after a snapshot has entered Evaluate, before cache lookup or contract execution.
internal static class ValidationTestBarrier
{
    private const string EnvironmentVariable = "ARCH_LINTER_TEST_VALIDATION_BARRIER";

    public static void WaitForCancellationIfEnabled(CancellationToken cancellationToken)
    {
        string? markerPath = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(markerPath))
        {
            return;
        }

        File.WriteAllText(markerPath, "entered");
        if (!cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("The validation test barrier was not cancelled within 15 seconds.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
