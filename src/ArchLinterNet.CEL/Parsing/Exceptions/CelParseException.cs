namespace ArchLinterNet.CEL.Parsing;

// Internal control-flow signal for CelParser's recursive-descent parse. It is caught before a
// result leaves the assembly; the diagnostic remains untyped here so exception types stay independent.
internal sealed class CelParseException(object diagnostic) : Exception
{
    public object Diagnostic { get; } = diagnostic;
}
