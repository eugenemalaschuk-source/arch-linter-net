namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkEvidenceJson
{
    public static string Serialize(BenchmarkEvidenceDocument evidence)
    {
        evidence.Validate();
        return BenchmarkJson.Serialize(evidence);
    }

    public static BenchmarkEvidenceDocument Deserialize(string json)
    {
        BenchmarkEvidenceDocument evidence = BenchmarkJson.Deserialize<BenchmarkEvidenceDocument>(json);
        evidence.Validate();
        return evidence;
    }
}
