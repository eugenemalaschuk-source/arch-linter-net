using Unity;

namespace ArchLinterNet.Core;

public static class ArchitectureCompositionRoot
{
    public static IUnityContainer RegisterCoreServices(this IUnityContainer container)
    {
        container.RegisterSingleton<IArchitectureValidator, ArchitectureValidator>();
        return container;
    }
}
