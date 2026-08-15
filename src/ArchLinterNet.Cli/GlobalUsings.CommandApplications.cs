// Command entry points depend on their feature-local application services. Keeping these imports
// centralized makes the physical feature folders the single source of truth for command ownership.
global using ArchLinterNet.Cli.Commands.Baseline.Application;
global using ArchLinterNet.Cli.Commands.Cache.Application;
global using ArchLinterNet.Cli.Commands.Explain.Application;
global using ArchLinterNet.Cli.Commands.Graph.Application;
global using ArchLinterNet.Cli.Commands.Policy.Application;
global using ArchLinterNet.Cli.Commands.PublicApi.Application;
global using ArchLinterNet.Cli.Commands.Scaffold.Application;
global using ArchLinterNet.Cli.Commands.Schema.Application;
global using ArchLinterNet.Cli.Commands.Validate.Application;
