using System.Diagnostics;
using System.Text;

namespace ArchLinterNet.Core.Tests;

internal sealed class CheckpointBPhaseTrace
{
    private const int MaximumEntries = 64;
    private const int MaximumCommandCharacters = 512;
    private readonly List<CompletedPhase> _completed = [];
    private ActivePhase? _active;

    public PhaseScope Start(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (_active is not null)
        {
            throw new InvalidOperationException("A Checkpoint B command phase is already active.");
        }

        var active = new ActivePhase(Bound(command), Stopwatch.StartNew());
        _active = active;
        return new PhaseScope(this, active);
    }

    public string FormatCompleted() => Format("Checkpoint B v0.8 full-cycle command timings:");

    public string FormatCancellation() =>
        Format("Checkpoint B v0.8 full-cycle command timings before NUnit cancellation:");

    private string Format(string heading)
    {
        var builder = new StringBuilder(heading);
        foreach (CompletedPhase completed in _completed)
        {
            builder.AppendLine();
            builder.Append("  completed [");
            builder.Append(completed.ElapsedMilliseconds);
            builder.Append(" ms] ");
            builder.Append(completed.Command);
        }

        if (_active is not null)
        {
            builder.AppendLine();
            builder.Append("  active [");
            builder.Append(_active.Stopwatch.ElapsedMilliseconds);
            builder.Append(" ms] ");
            builder.Append(_active.Command);
        }

        return builder.ToString();
    }

    private void Complete(ActivePhase active)
    {
        if (!ReferenceEquals(_active, active))
        {
            throw new InvalidOperationException("The Checkpoint B command phase is no longer active.");
        }

        active.Stopwatch.Stop();
        if (_completed.Count == MaximumEntries)
        {
            _completed.RemoveAt(0);
        }

        _completed.Add(new CompletedPhase(active.Command, active.Stopwatch.ElapsedMilliseconds));
        _active = null;
    }

    private static string Bound(string command) => command.Length <= MaximumCommandCharacters
        ? command
        : $"{command[..(MaximumCommandCharacters - 3)]}...";

    internal sealed class PhaseScope : IDisposable
    {
        private readonly CheckpointBPhaseTrace _owner;
        private readonly ActivePhase _active;
        private bool _completed;

        internal PhaseScope(CheckpointBPhaseTrace owner, ActivePhase active)
        {
            _owner = owner;
            _active = active;
        }

        public void Complete()
        {
            if (_completed)
            {
                throw new InvalidOperationException("The Checkpoint B command phase already completed.");
            }

            _owner.Complete(_active);
            _completed = true;
        }

        public void Dispose()
        {
        }
    }

    internal sealed record ActivePhase(string Command, Stopwatch Stopwatch);

    private sealed record CompletedPhase(string Command, long ElapsedMilliseconds);
}
