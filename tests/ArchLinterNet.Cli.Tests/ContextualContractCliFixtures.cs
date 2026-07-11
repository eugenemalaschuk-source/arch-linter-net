// Sales/Inventory/SharedKernel-style fixtures for CLI-level end-to-end tests of the contextual
// dependency/allow-only families, mirroring ArchLinterNet.Core.Tests's
// ContextualContractTestFixtures.cs. Kept in the CLI test project (rather than reused across
// projects) so the policy YAML in ContextualContractCliTests.cs can target
// "ArchLinterNet.Cli.Tests" as target_assemblies and have these types resolved from the running
// test process's own already-loaded entry assembly.
namespace ContextualContractCliFixtures
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CliContextDomainMarkerAttribute : Attribute
    {
        public CliContextDomainMarkerAttribute(string domain)
        {
            Domain = domain;
        }

        public string Domain { get; }
    }

    [CliContextDomainMarker("Sales")]
    public sealed class CliSalesCheckout
    {
        public CliInventoryStockItem Stock { get; } = null!;
    }

    [CliContextDomainMarker("Inventory")]
    public sealed class CliInventoryStockItem;
}
