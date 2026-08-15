using System.Security.Cryptography;
using System.Text;

namespace ArchLinterNet.Core.Model;

// The policy and baseline loaders consume decoded text rather than raw file bytes. Preserve an
// identity for precisely that consumed value, so cache authorization can neither key nor publish
// an outcome under text that was written after the document was loaded.
internal sealed record ArchitectureLoadedTextIdentity(string FullPath, string ContentDigest);

internal static class ArchitectureLoadedTextIdentityFactory
{
    public static ArchitectureLoadedTextIdentity FromText(string path, string text)
    {
        return new ArchitectureLoadedTextIdentity(
            Path.GetFullPath(path),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }

    public static ArchitectureLoadedTextIdentity FromPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return FromText(fullPath, File.ReadAllText(fullPath));
    }
}
