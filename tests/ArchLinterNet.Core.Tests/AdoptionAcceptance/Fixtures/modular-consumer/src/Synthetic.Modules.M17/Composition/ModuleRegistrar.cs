using Synthetic.Shared.Abstractions;

namespace Synthetic.Modules.M17.Composition;

/// <summary>The module's own composition boundary; service resolution is allowed only here.</summary>
public static class ModuleRegistrar
{
    public static IModule? Resolve(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService(typeof(IModule)) as IModule;
    }
}
