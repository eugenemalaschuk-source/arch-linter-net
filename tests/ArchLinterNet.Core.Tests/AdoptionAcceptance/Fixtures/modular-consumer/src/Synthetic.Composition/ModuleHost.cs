using Synthetic.Shared.Abstractions;

namespace Synthetic.Composition;

/// <summary>The synthetic solution's single composition root.</summary>
public static class ModuleHost
{
    public static IEnumerable<IModule> Modules()
    {
        yield return new Synthetic.Modules.M01.Module();
        yield return new Synthetic.Modules.M02.Module();
        yield return new Synthetic.Modules.M03.Module();
        yield return new Synthetic.Modules.M04.Module();
        yield return new Synthetic.Modules.M05.Module();
        yield return new Synthetic.Modules.M06.Module();
        yield return new Synthetic.Modules.M07.Module();
        yield return new Synthetic.Modules.M08.Module();
        yield return new Synthetic.Modules.M09.Module();
        yield return new Synthetic.Modules.M10.Module();
        yield return new Synthetic.Modules.M11.Module();
        yield return new Synthetic.Modules.M12.Module();
        yield return new Synthetic.Modules.M13.Module();
        yield return new Synthetic.Modules.M14.Module();
        yield return new Synthetic.Modules.M15.Module();
        yield return new Synthetic.Modules.M16.Module();
        yield return new Synthetic.Modules.M17.Module();
        yield return new Synthetic.Modules.M18.Module();
        yield return new Synthetic.Modules.M19.Module();
        yield return new Synthetic.Modules.M20.Module();
    }

    public static IModule? Resolve(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService(typeof(IModule)) as IModule;
    }
}
