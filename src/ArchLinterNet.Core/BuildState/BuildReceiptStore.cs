using System.Text.Json;

namespace ArchLinterNet.Core.BuildState;

// Receipts are untrusted data until every digest/identity check succeeds (see the security/trust
// boundary in the fingerprint spec) — TryRead never throws on a missing/corrupt file, it simply
// reports no receipt, which the preflight evaluator treats as `unverifiable-artifact`.
public static class BuildReceiptStore
{
    private const string ReceiptFileSuffix = ".archlinternet-receipt.json";

    public static string ReceiptPathFor(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        return assemblyPath + ReceiptFileSuffix;
    }

    public static bool TryRead(string assemblyPath, out BuildReceiptV1? receipt)
    {
        string receiptPath = ReceiptPathFor(assemblyPath);
        if (!File.Exists(receiptPath))
        {
            receipt = null;
            return false;
        }

        try
        {
            receipt = JsonSerializer.Deserialize<BuildReceiptV1>(File.ReadAllText(receiptPath));
            return receipt != null;
        }
        catch (JsonException)
        {
            receipt = null;
            return false;
        }
    }

    public static void Write(string assemblyPath, BuildReceiptV1 receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        File.WriteAllText(ReceiptPathFor(assemblyPath), JsonSerializer.Serialize(receipt));
    }
}
