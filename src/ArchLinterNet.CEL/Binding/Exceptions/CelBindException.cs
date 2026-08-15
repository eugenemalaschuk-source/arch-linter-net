namespace ArchLinterNet.CEL.Binding;

// Internal control-flow signal for CelBinder's recursive bind pass. It is caught before a result
// leaves the assembly; the diagnostic remains untyped here so exception types stay independent.
internal sealed class CelBindException(object diagnostic) : Exception
{
    public object Diagnostic { get; } = diagnostic;
}
