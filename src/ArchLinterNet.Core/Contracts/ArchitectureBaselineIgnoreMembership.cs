using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

/// <summary>Version-specific identity index for one contract's ordered ignore list.</summary>
internal sealed class ArchitectureBaselineIgnoreMembership
{
    private const int HashSetIndexEntryThreshold = 8;

    private readonly List<ArchitectureIgnoredViolation> _ignores;
    private readonly bool _isStructured;
    private readonly ArchitectureBaselineMergeWorkCounter? _workCounter;
    private HashSet<LegacyIgnoreKey>? _legacyKeys;
    private HashSet<StructuredIgnoreKey>? _structuredKeys;

    internal ArchitectureBaselineIgnoreMembership(
        List<ArchitectureIgnoredViolation> ignores,
        bool isStructured,
        ArchitectureBaselineMergeWorkCounter? workCounter)
    {
        _ignores = ignores;
        _isStructured = isStructured;
        _workCounter = workCounter;
        if (ignores.Count > HashSetIndexEntryThreshold)
        {
            BuildIndex();
        }
    }

    private bool UsesIndex => _legacyKeys is not null || _structuredKeys is not null;

    internal bool Contains(ArchitectureBaselineIgnoredViolation candidate)
    {
        if (_isStructured)
        {
            if (_structuredKeys is not null)
            {
                return _structuredKeys.Contains(StructuredIgnoreKey.From(candidate));
            }

            return ContainsStructuredLinear(candidate);
        }

        if (_legacyKeys is not null)
        {
            return _legacyKeys.Contains(new LegacyIgnoreKey(candidate.SourceType, candidate.ForbiddenReference));
        }

        return ContainsLegacyLinear(candidate);
    }

    private void AddToIndex(ArchitectureIgnoredViolation existing, bool countAsInline)
    {
        if (_isStructured)
        {
            if (existing.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion)
            {
                _structuredKeys!.Add(StructuredIgnoreKey.From(existing));
                if (countAsInline && _workCounter is not null)
                {
                    _workCounter.InlineIgnoreIndexEntries++;
                }
            }

            return;
        }

        _legacyKeys!.Add(new LegacyIgnoreKey(existing.SourceType, existing.ForbiddenReference));
        if (countAsInline && _workCounter is not null)
        {
            _workCounter.InlineIgnoreIndexEntries++;
        }
    }

    internal void AddLoaded(ArchitectureIgnoredViolation loaded)
    {
        _ignores.Add(loaded);
        if (!UsesIndex)
        {
            if (_ignores.Count > HashSetIndexEntryThreshold)
            {
                BuildIndex();
            }

            return;
        }

        if (_isStructured)
        {
            _structuredKeys!.Add(StructuredIgnoreKey.From(loaded));
            return;
        }

        _legacyKeys!.Add(new LegacyIgnoreKey(loaded.SourceType, loaded.ForbiddenReference));
    }

    private bool ContainsLegacyLinear(ArchitectureBaselineIgnoredViolation candidate)
    {
        foreach (ArchitectureIgnoredViolation existing in _ignores)
        {
            if (_workCounter is not null)
            {
                _workCounter.LinearMembershipComparisons++;
            }

            if (string.Equals(existing.SourceType, candidate.SourceType, StringComparison.Ordinal)
                && string.Equals(existing.ForbiddenReference, candidate.ForbiddenReference, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsStructuredLinear(ArchitectureBaselineIgnoredViolation candidate)
    {
        var candidateKey = StructuredIgnoreKey.From(candidate);
        foreach (ArchitectureIgnoredViolation existing in _ignores)
        {
            if (_workCounter is not null)
            {
                _workCounter.LinearMembershipComparisons++;
            }

            if (existing.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion
                && StructuredIgnoreKey.From(existing).Equals(candidateKey))
            {
                return true;
            }
        }

        return false;
    }

    private void BuildIndex()
    {
        if (_isStructured)
        {
            _structuredKeys ??= new HashSet<StructuredIgnoreKey>(_ignores.Count);
            foreach (ArchitectureIgnoredViolation existing in _ignores)
            {
                AddToIndex(existing, countAsInline: !existing.IsFromLoadedBaseline);
            }

            return;
        }

        _legacyKeys ??= new HashSet<LegacyIgnoreKey>(_ignores.Count);
        foreach (ArchitectureIgnoredViolation existing in _ignores)
        {
            AddToIndex(existing, countAsInline: !existing.IsFromLoadedBaseline);
        }
    }

    private readonly record struct LegacyIgnoreKey(string SourceType, string ForbiddenReference);

    private readonly record struct StructuredIgnoreKey(
        string? ContractFamily,
        string? SourceAssembly,
        string? SourceType,
        string? SourceMember,
        string? TargetAssembly,
        string? TargetType,
        string? TargetMember,
        int Occurrence,
        string? Configuration)
    {
        public static StructuredIgnoreKey From(ArchitectureIgnoredViolation ignore)
        {
            return new StructuredIgnoreKey(
                ignore.ContractFamily,
                ignore.SourceAssembly,
                ignore.SourceType,
                ignore.SourceMember,
                ignore.TargetAssembly,
                ignore.TargetType,
                ignore.TargetMember,
                ignore.Occurrence ?? 0,
                ignore.Configuration);
        }

        public static StructuredIgnoreKey From(ArchitectureBaselineIgnoredViolation ignore)
        {
            return new StructuredIgnoreKey(
                ignore.ContractFamily,
                ignore.SourceAssembly,
                ignore.SourceType,
                ignore.SourceMember,
                ignore.TargetAssembly,
                ignore.TargetType,
                ignore.TargetMember,
                ignore.Occurrence ?? 0,
                ignore.Configuration);
        }
    }
}
