namespace ArchLinterNet.Core.Contracts.Validators;

internal static class PolicyDocumentValidatorSupport
{
    public static bool HasNonBlankEntry(IEnumerable<string> values)
    {
        return values.Any(value => !string.IsNullOrWhiteSpace(value));
    }
}
