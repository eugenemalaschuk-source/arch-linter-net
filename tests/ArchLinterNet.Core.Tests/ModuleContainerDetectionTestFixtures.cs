namespace ModuleContainerFixtures.Clean.First.Models
{
    public sealed class FirstModel;
}

namespace ModuleContainerFixtures.Clean.First.Abstractions
{
    public interface IFirstFactory
    {
        Models.FirstModel Create();
    }
}

namespace ModuleContainerFixtures.Clean.First.Exceptions
{
    public sealed class FirstCommandException : Exception;
}

namespace ModuleContainerFixtures.Clean.First.Application
{
    public sealed class FirstCommand
    {
        public static Models.FirstModel Execute(Abstractions.IFirstFactory factory)
        {
            return factory.Create();
        }
    }
}

namespace ModuleContainerFixtures.Clean.First.EntryPoint
{
    public sealed class FirstCommandEntryPoint
    {
        public Application.FirstCommand Command { get; } = new();
    }
}

namespace ModuleContainerFixtures.Clean.Second.Models
{
    public sealed class SecondModel;
}

namespace ModuleContainerFixtures.Clean.Second.Application
{
    public sealed class SecondCommand
    {
        public static Models.SecondModel Execute()
        {
            return new Models.SecondModel();
        }
    }
}

namespace ModuleContainerFixtures.Cross.Alpha.Application
{
    public sealed class AlphaCommand
    {
        public Beta.Application.BetaCommand Command { get; } = new();
    }
}

namespace ModuleContainerFixtures.Cross.Beta.Application
{
    public sealed class BetaCommand;
}

namespace ModuleContainerFixtures.Structure.Orders
{
    public sealed class OrdersRootCommand;
}

namespace ModuleContainerFixtures.Structure.Common.Models
{
    public sealed class CommonModel;
}

namespace ModuleContainerFixtures.Structure.common.Models
{
    public sealed class LowercaseCommonModel;
}

namespace ModuleContainerFixtures.Structure.Payments.Infrastructure
{
    public sealed class PaymentInfrastructure;
}
