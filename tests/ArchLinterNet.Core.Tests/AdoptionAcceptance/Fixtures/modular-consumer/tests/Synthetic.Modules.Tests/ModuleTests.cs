using Synthetic.Shared.Abstractions;

namespace Synthetic.Modules.Tests;

/// <summary>Deliberately excluded from project governance by analysis.project_exclude.</summary>
public static class ModuleTests
{
    public static string Name(IModule module) => module.Name;
}
