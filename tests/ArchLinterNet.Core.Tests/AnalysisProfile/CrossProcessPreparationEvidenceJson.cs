namespace ArchLinterNet.Core.Tests;

internal static class CrossProcessPreparationEvidenceJson
{
    public static string Serialize(CrossProcessPreparationEvidenceDocument evidence)
    {
        evidence.Validate();
        return BenchmarkJson.Serialize(evidence);
    }

    public static CrossProcessPreparationEvidenceDocument Deserialize(string json)
    {
        CrossProcessPreparationEvidenceDocument evidence = BenchmarkJson.Deserialize<CrossProcessPreparationEvidenceDocument>(json);
        evidence.Validate();
        return evidence;
    }
}
