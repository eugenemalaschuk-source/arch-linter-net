namespace ArchLinterNet.Core.Model;

public sealed class ArchitecturePolicyValidationException : InvalidOperationException
{
    public ArchitecturePolicyValidationException(string message, object diagnostic, Exception innerException)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
    }

    public dynamic Diagnostic { get; }
}
