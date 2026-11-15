using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

/// <summary>Version-specific identity index for one contract's ordered ignore list.</summary>
internal sealed class ArchitectureBaselineIgnoreMembership
{
    private readonly bool _isStructured;
    private readonly HashSet<LegacyIgnoreKey>? _legacyKeys;
    private readonly HashSet<StructuredIgnoreKey>? _structuredKeys;

    internal ArchitectureBaselineIgnoreMembership(
        List<ArchitectureIgnoredViolation> ignores,
        bool isStructured)
    {
        Ignores = ignores;
        _isStructured = isStructured;
        if (isStructured)
        {
            _structuredKeys = new HashSet<StructuredIgnoreKey>(ignores.Count);
        }
        else
        {
            _legacyKeys = new HashSet<LegacyIgnoreKey>(ignores.Count);
        }
    }

    internal List<ArchitectureIgnoredViolation> Ignores { get; }

    internal bool Contains(ArchitectureBaselineIgnoredViolation candidate)
    {
        return _isStructured
            ? _structuredKeys!.Contains(StructuredIgnoreKey.From(candidate))
            : _legacyKeys!.Contains(new LegacyIgnoreKey(candidate.SourceType, candidate.ForbiddenReference));
    }

    internal void SeedInline(ArchitectureIgnoredViolation existing)
    {
        if (_isStructured)
        {
            if (existing.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion)
            {
                _structuredKeys!.Add(StructuredIgnoreKey.From(existing));
            }

            return;
        }

        _legacyKeys!.Add(new LegacyIgnoreKey(existing.SourceType, existing.ForbiddenReference));
    }

    internal void AddLoaded(ArchitectureIgnoredViolation loaded)
    {
        Ignores.Add(loaded);
        if (_isStructured)
        {
            _structuredKeys!.Add(StructuredIgnoreKey.From(loaded));
        }
        else
        {
            _legacyKeys!.Add(new LegacyIgnoreKey(loaded.SourceType, loaded.ForbiddenReference));
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
        public static StructuredIgnoreKey From(ArchitectureIgnoredViolation ignore) => new(
            ignore.ContractFamily,
            ignore.SourceAssembly,
            ignore.SourceType,
            ignore.SourceMember,
            ignore.TargetAssembly,
            ignore.TargetType,
            ignore.TargetMember,
            ignore.Occurrence ?? 0,
            ignore.Configuration);

        public static StructuredIgnoreKey From(ArchitectureBaselineIgnoredViolation ignore) => new(
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
