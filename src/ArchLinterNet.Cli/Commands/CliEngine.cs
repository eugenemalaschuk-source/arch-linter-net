using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands;

internal static class CliEngine
{
    public static readonly ArchitectureDiagnosticFormatter Formatter = new();
    public static readonly ArchitectureSarifFormatter SarifFormatter = new();

    public static readonly Lazy<ArchitectureEngine> Engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static bool TryParseLevel(string level, out ArchitectureGraphLevel graphLevel)
    {
        switch (level)
        {
            case "namespace":
                graphLevel = ArchitectureGraphLevel.Namespace;
                return true;
            case "type":
                graphLevel = ArchitectureGraphLevel.Type;
                return true;
            case "assembly":
                graphLevel = ArchitectureGraphLevel.Assembly;
                return true;
            default:
                graphLevel = default;
                return false;
        }
    }
}
