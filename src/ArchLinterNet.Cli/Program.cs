using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "baseline")
        {
            return BaselineCommand.Run(args[1..]);
        }

        if (args.Length > 0 && args[0] == "graph")
        {
            return GraphCommand.Run(args[1..]);
        }

        if (args.Length > 0 && args[0] == "explain")
        {
            return ExplainCommand.Run(args[1..]);
        }

        return ValidateCommand.Run(args);
    }
}
