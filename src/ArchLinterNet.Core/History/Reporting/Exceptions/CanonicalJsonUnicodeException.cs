using System.Globalization;

namespace ArchLinterNet.Core.History.Reporting;

// Report strings originate with canonical Git evidence, but optional downstream enrichment can
// introduce .NET text independently. A dedicated type lets the CLI retain the normal fail-closed
// diagnostic boundary instead of relying on a host encoder to replace malformed UTF-16 later.
internal sealed class CanonicalJsonUnicodeException(int utf16Index)
    : ArgumentException($"A canonical JSON string contains an unpaired UTF-16 surrogate at index {utf16Index.ToString(CultureInfo.InvariantCulture)}.")
{
}
