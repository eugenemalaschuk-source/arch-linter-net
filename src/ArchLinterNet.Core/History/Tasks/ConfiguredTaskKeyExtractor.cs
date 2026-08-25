using System.Globalization;
using System.Numerics;
using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Tasks.Abstractions;

namespace ArchLinterNet.Core.History.Tasks;

// A configuration pattern has deliberately been reduced to literal prefix/suffix bytes plus its one
// decimal capture. That lets this extractor preserve raw-message byte spans without exposing a
// general regex language whose capture and backtracking behavior would become canonical semantics.
internal sealed class ConfiguredTaskKeyExtractor : ITaskKeyExtractor
{
    private readonly byte[] _prefix;
    private readonly byte[] _suffix;
    private readonly string _keyNamespace;

    public ConfiguredTaskKeyExtractor(HistoryTaskExtractorConfiguration configuration)
    {
        ExtractorId = configuration.Id;
        _keyNamespace = configuration.Namespace;
        _prefix = Encoding.UTF8.GetBytes(configuration.Pattern.Prefix);
        _suffix = Encoding.UTF8.GetBytes(configuration.Pattern.Suffix);
    }

    public string ExtractorId { get; }

    public void Extract(byte[] rawMessage, ICollection<TaskKeyMatch> matches)
    {
        for (int matchStart = 0; matchStart <= rawMessage.Length - _prefix.Length; matchStart++)
        {
            if (TryMatchAt(rawMessage, matchStart, out TaskKeyMatch? match))
            {
                matches.Add(match!);
            }
        }
    }

    private bool TryMatchAt(byte[] rawMessage, int matchStart, out TaskKeyMatch? match)
    {
        match = null;
        if (!rawMessage.AsSpan(matchStart, _prefix.Length).SequenceEqual(_prefix)
            || (matchStart > 0 && IsBoundaryByte(rawMessage[matchStart - 1])))
        {
            return false;
        }

        int identifierStart = matchStart + _prefix.Length;
        int identifierEnd = identifierStart;
        while (identifierEnd < rawMessage.Length && IsAsciiDigit(rawMessage[identifierEnd]))
        {
            identifierEnd++;
        }

        if (identifierEnd == identifierStart
            || identifierEnd + _suffix.Length > rawMessage.Length
            || !rawMessage.AsSpan(identifierEnd, _suffix.Length).SequenceEqual(_suffix))
        {
            return false;
        }

        int matchEnd = identifierEnd + _suffix.Length;
        if (matchEnd < rawMessage.Length && IsBoundaryByte(rawMessage[matchEnd]))
        {
            return false;
        }

        BigInteger identifier = BigInteger.Parse(
            Encoding.ASCII.GetString(rawMessage, identifierStart, identifierEnd - identifierStart),
            CultureInfo.InvariantCulture);
        if (identifier <= BigInteger.Zero)
        {
            return false;
        }

        match = new TaskKeyMatch(
            ExtractorId,
            new TaskKey(_keyNamespace, identifier),
            matchStart,
            matchEnd,
            Encoding.UTF8.GetString(rawMessage, matchStart, matchEnd - matchStart));
        return true;
    }

    private static bool IsAsciiDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool IsBoundaryByte(byte value) =>
        value is >= (byte)'A' and <= (byte)'Z'
        || value is >= (byte)'a' and <= (byte)'z'
        || value is >= (byte)'0' and <= (byte)'9'
        || value is (byte)'_' or (byte)'#';
}
