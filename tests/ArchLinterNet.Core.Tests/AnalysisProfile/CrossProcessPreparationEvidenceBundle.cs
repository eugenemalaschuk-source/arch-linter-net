using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

// Issue #493 compares the same synthetic candidate through both consumer-shaped compilation
// boundaries. Keeping the pair in one envelope prevents a single archetype from being mistaken
// for the complete decision evidence.
internal sealed record CrossProcessPreparationEvidenceBundle
{
    public const string SchemaId = "cross-process-preparation-evidence-bundle/v1";

    public required string EvidenceSchemaId { get; init; }

    public required IReadOnlyList<CrossProcessPreparationEvidenceDocument> Archetypes { get; init; }

    public void Validate()
    {
        if (!string.Equals(EvidenceSchemaId, SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported preparation evidence bundle schema '{EvidenceSchemaId}'.");
        }

        if (Archetypes.Count < 2)
        {
            throw new InvalidOperationException("Issue #493 evidence must include real-MSBuild and staged-assembly archetypes.");
        }

        foreach (CrossProcessPreparationEvidenceDocument archetype in Archetypes)
        {
            archetype.Validate();
        }

        if (Archetypes.Select(archetype => archetype.Workflow.PreparationBoundary).Distinct().Count() != Archetypes.Count)
        {
            throw new InvalidOperationException("Issue #493 evidence must retain distinct preparation boundaries.");
        }

        if (!Archetypes.Any(archetype => archetype.Workflow.PreparationBoundary == PreparationBoundaryKind.MsBuildReceipt) ||
            !Archetypes.Any(archetype => archetype.Workflow.PreparationBoundary == PreparationBoundaryKind.StagedAssemblies))
        {
            throw new InvalidOperationException("Issue #493 evidence is incomplete without both preparation archetypes.");
        }
    }
}

internal static class CrossProcessPreparationEvidenceBundleJson
{
    public static string Serialize(CrossProcessPreparationEvidenceBundle evidence)
    {
        evidence.Validate();
        return BenchmarkJson.Serialize(evidence);
    }

    public static CrossProcessPreparationEvidenceBundle Deserialize(string json)
    {
        CrossProcessPreparationEvidenceBundle evidence = BenchmarkJson.Deserialize<CrossProcessPreparationEvidenceBundle>(json);
        evidence.Validate();
        return evidence;
    }
}
