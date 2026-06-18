using Unity;

namespace ArchLinterNet.Core;

public static class ServiceLocator
{
    public static IUnityContainer Container { get; private set; } = new UnityContainer()!;
}
