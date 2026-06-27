#pragma warning disable CS0649 // Fields exist only so ArchitectureReferenceScanner can discover cross-namespace references.

namespace ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Alpha
{
    internal sealed class AlphaType
    {
        public ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Beta.BetaType? Reference;
    }

    internal sealed class AlphaOtherType
    {
        public AlphaType? SameNamespaceReference;
    }
}

namespace ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Beta
{
    internal sealed class BetaType
    {
    }

    internal sealed class BetaOtherType
    {
    }
}

#pragma warning restore CS0649
