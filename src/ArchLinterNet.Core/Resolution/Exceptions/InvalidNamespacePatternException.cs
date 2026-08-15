namespace ArchLinterNet.Core.Resolution;

public sealed class InvalidNamespacePatternException : InvalidOperationException
{
    public InvalidNamespacePatternException(string pattern, string reason)
        : base(BuildMessage(pattern, reason))
    {
        Pattern = pattern;
        Reason = reason;
    }

    public string Pattern { get; }
    public string Reason { get; }

    private static string BuildMessage(string pattern, string reason)
    {
        return $"Invalid namespace glob pattern '{pattern}': {reason}";
    }
}
