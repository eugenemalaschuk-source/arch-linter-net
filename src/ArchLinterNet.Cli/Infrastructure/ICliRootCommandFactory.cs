using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal interface ICliRootCommandFactory
{
    Command Create();
}
