using System.IO;

namespace ArchLinterNet.Core.History.Git;

// Deterministic authored-operand resolution. Git's own DWIM precedence is deliberately not used:
// a shorthand matching both a tag and a head is ambiguous rather than silently preferring the tag.
internal sealed class GitRefResolver(GitRepositoryLayout layout, GitObjectDatabase objects)
{
    private const string SymbolicPrefix = "ref: ";
    private const int MaxSymbolicDepth = 32;

    private Dictionary<string, GitObjectId>? _packedRefs;

    public GitObjectId ResolveToCommit(string authoredOperand)
    {
        GitObjectId resolved = ResolveOperand(authoredOperand);
        return PeelToCommit(authoredOperand, resolved);
    }

    private GitObjectId ResolveOperand(string authoredOperand)
    {
        if (string.IsNullOrEmpty(authoredOperand))
        {
            throw Unresolved(authoredOperand);
        }

        if (authoredOperand == "HEAD")
        {
            return ResolveRefName("HEAD", authoredOperand);
        }

        // A full-length hexadecimal operand is an object ID, never a shorthand ref name.
        if (GitObjectId.TryParseHex(authoredOperand, layout.DigestLength, out GitObjectId direct))
        {
            return direct;
        }

        if (authoredOperand.StartsWith("refs/", StringComparison.Ordinal))
        {
            return ResolveRefName(authoredOperand, authoredOperand);
        }

        bool tagExists = TryReadRef($"refs/tags/{authoredOperand}", out GitObjectId tagTarget, out string? tagSymbolic);
        bool headExists = TryReadRef($"refs/heads/{authoredOperand}", out GitObjectId headTarget, out string? headSymbolic);
        if (tagExists && headExists)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.RefAmbiguous,
                $"The authored operand '{authoredOperand}' matches both refs/tags/{authoredOperand} and refs/heads/{authoredOperand}.");
        }

        if (tagExists)
        {
            return tagSymbolic is null ? tagTarget : ResolveRefName(tagSymbolic, authoredOperand, 1);
        }

        if (headExists)
        {
            return headSymbolic is null ? headTarget : ResolveRefName(headSymbolic, authoredOperand, 1);
        }

        throw Unresolved(authoredOperand);
    }

    private GitObjectId ResolveRefName(string refName, string authoredOperand, int depth = 0)
    {
        if (depth > MaxSymbolicDepth)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.RefCycle,
                $"The authored operand '{authoredOperand}' resolves through a symbolic reference cycle.");
        }

        if (!TryReadRef(refName, out GitObjectId target, out string? symbolic))
        {
            throw Unresolved(authoredOperand);
        }

        return symbolic is null ? target : ResolveRefName(symbolic, authoredOperand, depth + 1);
    }

    private GitObjectId PeelToCommit(string authoredOperand, GitObjectId start)
    {
        GitObjectId current = start;
        for (int depth = 0; depth <= MaxSymbolicDepth; depth++)
        {
            GitRawObject? raw = objects.TryRead(current);
            if (raw is null)
            {
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.ObjectMissing,
                    $"The authored operand '{authoredOperand}' resolves to object '{current.Hex}', which is not in the object database.",
                    objectId: current.Hex);
            }

            if (raw.Kind == GitObjectKind.Commit)
            {
                return current;
            }

            if (raw.Kind != GitObjectKind.Tag)
            {
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.RefNotACommit,
                    $"The authored operand '{authoredOperand}' resolves to a {raw.Kind.ToString().ToLowerInvariant()} object rather than a commit.",
                    objectId: current.Hex);
            }

            current = ReadTagTarget(current, raw);
        }

        throw HistoryFailures.Fail(
            HistoryDiagnosticKind.RefCycle,
            $"The authored operand '{authoredOperand}' peels through an unterminated tag chain.");
    }

    private GitObjectId ReadTagTarget(GitObjectId tagId, GitRawObject raw)
    {
        foreach (GitHeaderLine header in GitHeaderReader.ReadDirectHeaders(raw.Payload))
        {
            if (header.Name != "object")
            {
                continue;
            }

            if (GitObjectId.TryParseHex(header.ValueText, layout.DigestLength, out GitObjectId target))
            {
                return target;
            }

            break;
        }

        throw HistoryFailures.Fail(
            HistoryDiagnosticKind.ObjectMalformed,
            $"The annotated tag object '{tagId.Hex}' has no usable object header.",
            objectId: tagId.Hex);
    }

    private bool TryReadRef(string refName, out GitObjectId target, out string? symbolicTarget)
    {
        target = default;
        symbolicTarget = null;
        string loosePath = Path.Combine(layout.GitDirectory, refName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(loosePath))
        {
            string content = File.ReadAllText(loosePath).Trim();
            if (content.StartsWith(SymbolicPrefix, StringComparison.Ordinal))
            {
                symbolicTarget = content[SymbolicPrefix.Length..].Trim();
                return true;
            }

            if (GitObjectId.TryParseHex(content, layout.DigestLength, out target))
            {
                return true;
            }

            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.RefUnresolved,
                $"The reference '{refName}' does not contain a canonical object ID.");
        }

        return PackedRefs().TryGetValue(refName, out target);
    }

    private Dictionary<string, GitObjectId> PackedRefs()
    {
        if (_packedRefs is not null)
        {
            return _packedRefs;
        }

        _packedRefs = new Dictionary<string, GitObjectId>(StringComparer.Ordinal);
        string path = Path.Combine(layout.GitDirectory, "packed-refs");
        if (!File.Exists(path))
        {
            return _packedRefs;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            // `^<id>` peel lines are ignored: peeling is performed from the object database so a
            // stale or absent peel line cannot change canonical resolution.
            if (line.Length == 0 || line[0] is '#' or '^')
            {
                continue;
            }

            int space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0 && GitObjectId.TryParseHex(line[..space], layout.DigestLength, out GitObjectId id))
            {
                _packedRefs[line[(space + 1)..].Trim()] = id;
            }
        }

        return _packedRefs;
    }

    private static HistoryFailureException Unresolved(string authoredOperand)
        => HistoryFailures.Fail(
            HistoryDiagnosticKind.RefUnresolved,
            $"The authored operand '{authoredOperand}' is not HEAD, a full object ID, a fully-qualified ref, or a unique tag/head shorthand.");
}
