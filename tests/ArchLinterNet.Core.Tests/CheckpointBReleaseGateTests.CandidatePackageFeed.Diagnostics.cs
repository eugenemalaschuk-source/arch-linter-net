using System.Diagnostics;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private sealed partial class CandidatePackageFeed
    {
        private CheckpointBPhaseTrace? _phaseTrace;
        private readonly HashSet<string> _restoredFullCycleRoots = new(StringComparer.Ordinal);

        public IDisposable BeginPhaseTrace(CheckpointBPhaseTrace phaseTrace)
        {
            ArgumentNullException.ThrowIfNull(phaseTrace);
            if (_phaseTrace is not null)
            {
                throw new InvalidOperationException("A Checkpoint B phase trace is already active.");
            }

            _phaseTrace = phaseTrace;
            return new PhaseTraceScope(this);
        }

        public CommandResult RunToolWithReusedRestore(string workingDirectory, params string[] arguments)
        {
            if (!arguments.Contains("--ensure-built", StringComparer.Ordinal))
            {
                return RunTool(workingDirectory, arguments);
            }

            string root = Path.GetFullPath(workingDirectory);
            if (arguments.Contains("--no-restore", StringComparer.Ordinal))
            {
                return RunTool(workingDirectory, arguments);
            }

            if (_restoredFullCycleRoots.Contains(root))
            {
                return RunTool(workingDirectory, arguments.Append("--no-restore").ToArray());
            }

            CommandResult result = RunTool(workingDirectory, arguments);
            _restoredFullCycleRoots.Add(root);
            return result;
        }

        private CommandResult RunTracedTool(ProcessStartInfo startInfo, IReadOnlyList<string> arguments)
        {
            CheckpointBPhaseTrace? phaseTrace = _phaseTrace;
            using CheckpointBPhaseTrace.PhaseScope? phase = phaseTrace?.Start(FormatToolCommand(arguments));
            CommandResult result = Run(startInfo);
            phase?.Complete();
            return result;
        }

        private static string FormatToolCommand(IReadOnlyList<string> arguments) =>
            $"arch-linter-net {string.Join(' ', arguments)}";

        private sealed class PhaseTraceScope : IDisposable
        {
            private readonly CandidatePackageFeed _owner;

            public PhaseTraceScope(CandidatePackageFeed owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner._phaseTrace = null;
            }
        }
    }
}
