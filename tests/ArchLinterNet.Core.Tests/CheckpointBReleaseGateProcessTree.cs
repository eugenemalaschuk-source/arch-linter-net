namespace ArchLinterNet.Core.Tests;

internal static class CheckpointBReleaseGateProcessTree
{
    public static bool TargetAssemblyIsMapped(int processId, string assemblyFileName)
    {
        return Enumerate(processId).Any(process => IsMapped(process, assemblyFileName));
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

    private static bool IsMapped(int processId, string assemblyFileName)
    {
        try
        {
            return File.ReadLines($"/proc/{processId}/maps")
                .Any(line => line.EndsWith(assemblyFileName, StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return false;
        }
    }
}
