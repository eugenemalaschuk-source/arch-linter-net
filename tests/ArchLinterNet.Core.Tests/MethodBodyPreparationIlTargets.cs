namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer
{
    internal static class MethodBodyPreparationIlTarget
    {
        internal static void Write() => System.Console.WriteLine("consumer IL input");
    }
}

namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Other
{
    internal static class MethodBodyPreparationIlTarget
    {
        internal static void Write() => System.Console.WriteLine("other IL input");
    }
}
