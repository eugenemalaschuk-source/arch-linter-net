namespace ArchLinterNet.Core.Tests;

internal static class CheckpointBReleaseGateProcessTree
{
    public static bool HasReadAtLeast(int processId, long minimumBytes)
    {
        return Enumerate(processId).Any(process => ReadCharacterCount(process) >= minimumBytes);
    }

    private static IEnumerable<int> Enumerate(int processId)
    {
        yield return processId;
        string children;
        try
        {
            children = File.ReadAllText($"/proc/{processId}/task/{processId}/children");
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (string child in children.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(child, out int childProcessId))
            {
                foreach (int descendant in Enumerate(childProcessId))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static long ReadCharacterCount(int processId)
    {
        try
        {
            string? value = File.ReadLines($"/proc/{processId}/io")
                .FirstOrDefault(line => line.StartsWith("rchar:", StringComparison.Ordinal));
            return value is null || !long.TryParse(value.AsSpan("rchar:".Length).Trim(), out long bytes)
                ? 0
                : bytes;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
