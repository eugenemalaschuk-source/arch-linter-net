namespace ProtectedGlob.Target.Execution
{
    public sealed class ProtectedExecutionService;
}

namespace ProtectedGlob.Source
{
    public sealed class SourceConsumer
    {
        public ProtectedGlob.Target.Execution.ProtectedExecutionService Service { get; } = new();
    }
}

namespace ProtectedGlob.Importers
{
    public sealed class DirectImporter
    {
        public ProtectedGlob.Target.Execution.ProtectedExecutionService Service { get; } = new();
    }
}
