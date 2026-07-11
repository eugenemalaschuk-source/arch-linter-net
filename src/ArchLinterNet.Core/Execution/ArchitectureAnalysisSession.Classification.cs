using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    // Runs attribute-based role extraction across every scanned type and returns the deduplicated
    // conflict/evidence-extraction-failure facts, for CLI/CI surfacing. Assembly-level facts recur
    // identically for every type declared in that assembly; ArchitectureClassificationConflict and
    // ArchitectureClassificationMetadataFailure are records, so HashSet deduplication is exact-value
    // based rather than requiring bespoke assembly-vs-type bookkeeping here.
    public (IReadOnlyList<ArchitectureClassificationConflict> Conflicts, IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures)
        CheckClassificationFacts()
    {
        Type[] types = TypeIndex.AllTypes();
        var extractor = new ArchitectureAttributeRoleExtractor(Document.Classification, types);

        HashSet<ArchitectureClassificationConflict> conflicts = new();
        HashSet<ArchitectureClassificationMetadataFailure> metadataFailures = new();

        foreach (Type type in types)
        {
            ArchitectureTypeClassificationResult result = extractor.Extract(type);
            conflicts.UnionWith(result.Conflicts);
            metadataFailures.UnionWith(result.MetadataFailures);
        }

        return (conflicts.ToList(), metadataFailures.ToList());
    }
}
