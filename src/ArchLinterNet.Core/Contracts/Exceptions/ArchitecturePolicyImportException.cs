namespace ArchLinterNet.Core.Contracts;

public sealed class ArchitecturePolicyImportException : InvalidOperationException
{
    public ArchitecturePolicyImportException(object category, string message)
        : this(category, message, diagnostic: null)
    {
    }

    public ArchitecturePolicyImportException(object category, string message, object? diagnostic)
        : base(message)
    {
        Category = category;
        Diagnostic = diagnostic;
    }

    public dynamic Category { get; }

    public dynamic? Diagnostic { get; }
}
