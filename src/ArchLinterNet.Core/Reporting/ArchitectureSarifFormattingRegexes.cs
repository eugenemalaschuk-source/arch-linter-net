using System.Text.RegularExpressions;

namespace ArchLinterNet.Core.Reporting;

// Source-generated regular expressions are kept with the SARIF formatting helpers rather than
// requiring the public formatter façade itself to remain partial.
internal static partial class ArchitectureSarifFormattingRegexes
{
    [GeneratedRegex(@"^line (?<line>\d+):", RegexOptions.CultureInvariant)]
    internal static partial Regex MethodBodyLinePattern();

    [GeneratedRegex(@"^\[(?<id>[^\]]+)\] ", RegexOptions.CultureInvariant)]
    internal static partial Regex CycleIdPrefixPattern();
}
