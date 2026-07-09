using System.CommandLine;

namespace ArchLinterNet.Cli;

internal interface ICliRootCommandFactory
{
    Command Create();
}
