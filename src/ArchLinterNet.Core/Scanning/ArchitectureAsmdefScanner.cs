using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning.Abstractions;

namespace ArchLinterNet.Core.Scanning;

public sealed class ArchitectureAsmdefScanner : IArchitectureAsmdefScanner
{
    private const string DefaultAsmdefRoot = "Assets";

    public IEnumerable<ArchitectureViolation> FindAsmdefViolations(
        string contractName,
        string? contractId,
        string repositoryRoot,
        ArchitectureAsmdefContract contract,
        string? asmdefRoot = null,
        IArchitectureFileSystem? fileSystem = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;
        string root = asmdefRoot ?? DefaultAsmdefRoot;
        Dictionary<string, AsmdefEntry> asmdefMap = LoadFirstPartyAsmdefs(repositoryRoot, root, fileSystem);
        HashSet<string> editorOnlyAssemblies = BuildEditorOnlySet(asmdefMap);

        foreach (string sourceAssemblyName in contract.SourceAssemblies)
        {
            if (!asmdefMap.TryGetValue(sourceAssemblyName, out AsmdefEntry? entry))
            {
                continue;
            }

            List<string> violations = new();

            if (contract.ForbiddenEditorRefs)
            {
                violations.AddRange(
                    entry.References.Where(r => editorOnlyAssemblies.Contains(r)));
            }

            foreach (string prefix in contract.ForbiddenAsmdefPrefixes)
            {
                string trimmedPrefix = prefix.TrimEnd('*');

                violations.AddRange(
                    entry.References
                        .Where(r => r.StartsWith(trimmedPrefix, StringComparison.Ordinal)
                                    && r != sourceAssemblyName));
            }

            string[] distinct = violations.Distinct().OrderBy(v => v).ToArray();

            if (distinct.Length > 0)
            {
                yield return new ArchitectureViolation(
                    contractName,
                    contractId,
                    sourceAssemblyName,
                    "asmdef-references",
                    distinct);
            }
        }
    }

    private static Dictionary<string, AsmdefEntry> LoadFirstPartyAsmdefs(
        string repositoryRoot, string asmdefRoot, IArchitectureFileSystem fileSystem)
    {
        string fullPath = Path.Combine(repositoryRoot, asmdefRoot.Replace('/', Path.DirectorySeparatorChar));
        Dictionary<string, AsmdefEntry> result = new(StringComparer.Ordinal);

        if (!fileSystem.DirectoryExists(fullPath))
        {
            return result;
        }

        foreach (string file in fileSystem.EnumerateFiles(fullPath, "*.asmdef", SearchOption.AllDirectories))
        {
            AsmdefEntry? entry = TryParseAsmdef(file, fileSystem);

            if (entry != null)
            {
                result[entry.Name] = entry;
            }
        }

        return result;
    }

    private static HashSet<string> BuildEditorOnlySet(Dictionary<string, AsmdefEntry> asmdefMap)
    {
        return asmdefMap.Values
            .Where(e => e.IncludePlatforms.Count == 1
                        && e.IncludePlatforms[0].Equals("Editor", StringComparison.Ordinal))
            .Select(e => e.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static AsmdefEntry? TryParseAsmdef(string filePath, IArchitectureFileSystem fileSystem)
    {
        try
        {
            string json = fileSystem.ReadAllText(filePath);

            using var doc = JsonDocument.Parse(json);

            string name = doc.RootElement.TryGetProperty("name", out JsonElement nameEl)
                ? nameEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            List<string> references = ReadStringArray(doc.RootElement, "references");
            List<string> includePlatforms = ReadStringArray(doc.RootElement, "includePlatforms");

            return new AsmdefEntry(name, references, includePlatforms);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        List<string> result = new();

        if (!element.TryGetProperty(propertyName, out JsonElement arrayEl))
        {
            return result;
        }

        foreach (JsonElement item in arrayEl.EnumerateArray())
        {
            string? value = item.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private sealed record AsmdefEntry(string Name, List<string> References, List<string> IncludePlatforms);
}
