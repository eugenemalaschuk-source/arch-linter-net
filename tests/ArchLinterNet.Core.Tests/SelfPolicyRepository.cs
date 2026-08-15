using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Shared helpers for the self-policy fixtures: locating the repository root, and materializing a
/// deliberately mutated copy of the real policy next to the original so every repository-relative
/// input it declares (<c>ArchLinterNet.slnx</c>, <c>architecture/api/*.public-api.txt</c>) still
/// resolves against the same policy boundary.
/// </summary>
internal static class SelfPolicyRepository
{
    /// <summary>Prefix for throwaway policy/snapshot copies; also matched by .gitignore.</summary>
    public const string MutationPrefix = "self-policy-mutation-";

    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && dir.GetFiles("ArchLinterNet.slnx").Length == 0)
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    public static string PolicyPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");

    /// <summary>
    /// Reads the real policy with line endings normalized to <c>\n</c> and flattens its imported
    /// contract fragments. The mutation regressions can therefore keep their exact anchors while
    /// the production policy is decomposed into independently reviewable files. `.gitattributes`
    /// pins only `schema/*.json` to LF, so this file is checked out CRLF on Windows; without
    /// normalizing here, every multi-line mutation anchor in <see cref="Replace"/> would match zero
    /// times and the negative regressions would fail before reaching the contract they exercise.
    /// </summary>
    public static string ReadPolicy(string repositoryRoot)
    {
        string policyPath = PolicyPath(repositoryRoot);
        string[] rootLines = File.ReadAllText(policyPath).ReplaceLineEndings("\n").Split('\n');
        int importsStart = Array.IndexOf(rootLines, "imports:");
        if (importsStart < 0)
        {
            return string.Join('\n', rootLines);
        }

        int importsEnd = importsStart + 1;
        while (importsEnd < rootLines.Length &&
               (string.IsNullOrWhiteSpace(rootLines[importsEnd]) || rootLines[importsEnd].StartsWith(' ')))
        {
            importsEnd++;
        }

        var importPaths = new List<string>();
        for (int index = importsStart + 1; index < importsEnd; index++)
        {
            const string ImportPrefix = "  - ";
            if (rootLines[index].StartsWith(ImportPrefix, StringComparison.Ordinal))
            {
                importPaths.Add(rootLines[index][ImportPrefix.Length..].Trim());
            }
        }

        var flattenedRootLines = new List<string>(rootLines.Length - (importsEnd - importsStart));
        for (int index = 0; index < importsStart; index++)
        {
            flattenedRootLines.Add(rootLines[index]);
        }

        for (int index = importsEnd; index < rootLines.Length; index++)
        {
            flattenedRootLines.Add(rootLines[index]);
        }

        var contractLines = new List<string>();
        string policyDirectory = Path.GetDirectoryName(policyPath)!;
        foreach (string importPath in importPaths)
        {
            string fragmentPath = Path.Combine(policyDirectory, importPath.Replace('/', Path.DirectorySeparatorChar));
            string[] fragmentLines = File.ReadAllText(fragmentPath).ReplaceLineEndings("\n").Split('\n');
            int contractsStart = Array.IndexOf(fragmentLines, "contracts:");
            if (contractsStart < 0)
            {
                throw new InvalidOperationException($"Imported self-policy fragment lacks contracts: {importPath}");
            }

            for (int index = contractsStart + 1; index < fragmentLines.Length; index++)
            {
                contractLines.Add(fragmentLines[index]);
            }
        }

        return string.Join('\n', flattenedRootLines).TrimEnd('\n')
            + "\n\ncontracts:\n"
            + string.Join('\n', contractLines).TrimEnd('\n')
            + "\n";
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a uniquely named sibling of the real policy and returns
    /// its path. The caller is responsible for deleting it (see <see cref="DeleteMutations"/>).
    /// </summary>
    public static string WriteMutatedPolicy(string repositoryRoot, string content)
    {
        string path = Path.Combine(
            repositoryRoot,
            "architecture",
            $"{MutationPrefix}{Guid.NewGuid():N}.arch.yml");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Writes a throwaway snapshot next to the reviewed ones and returns its path.</summary>
    public static string WriteMutatedSnapshot(string repositoryRoot, string content)
    {
        string path = Path.Combine(
            repositoryRoot,
            "architecture",
            "api",
            $"{MutationPrefix}{Guid.NewGuid():N}.public-api.txt");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Repository-relative path with forward slashes, as the policy declares them.</summary>
    public static string RelativePolicyPath(string repositoryRoot, string absolutePath) =>
        Path.GetRelativePath(repositoryRoot, absolutePath).Replace('\\', '/');

    public static void DeleteMutations(string repositoryRoot)
    {
        foreach (string directory in new[]
                 {
                     Path.Combine(repositoryRoot, "architecture"),
                     Path.Combine(repositoryRoot, "architecture", "api"),
                 })
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (string file in Directory.EnumerateFiles(directory, $"{MutationPrefix}*"))
                TryDelete(file);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover file is a nuisance, not a test failure; the next run's setup retries.
        }
    }

    /// <summary>
    /// Applies an exact, single-occurrence replacement, failing loudly when the anchor text no
    /// longer exists so a policy edit can never silently turn a negative regression into a no-op.
    /// Both operands are normalized to <c>\n</c> so an anchor written as a C# literal matches the
    /// same policy text on every platform, whatever line endings the checkout produced.
    /// </summary>
    public static string Replace(string policy, string anchor, string replacement)
    {
        policy = policy.ReplaceLineEndings("\n");
        anchor = anchor.ReplaceLineEndings("\n");
        replacement = replacement.ReplaceLineEndings("\n");

        int occurrences = CountOccurrences(policy, anchor);
        Assert.That(
            occurrences,
            Is.EqualTo(1),
            $"Expected exactly one occurrence of the mutation anchor in the repository policy:\n{anchor}");
        return policy.Replace(anchor, replacement);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
