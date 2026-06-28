#pragma warning disable CS0649 // Fields exist only so ArchitectureReferenceScanner can discover cross-namespace references.

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.LayerGoverned
{
    internal sealed class LayerGovernedSourceType
    {
        public ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.LayerGovernedTarget.LayerGovernedTargetType? Reference;
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.LayerGovernedTarget
{
    internal sealed class LayerGovernedTargetType
    {
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.IndependenceGoverned
{
    internal sealed class IndependenceGovernedSourceType
    {
        public ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.IndependenceGovernedTarget.IndependenceGovernedTargetType? Reference;
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.IndependenceGovernedTarget
{
    internal sealed class IndependenceGovernedTargetType
    {
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.DependencyGoverned
{
    internal sealed class DependencyGovernedSourceType
    {
        public ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.DependencyGovernedTarget.DependencyGovernedTargetType? Reference;
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.DependencyGovernedTarget
{
    internal sealed class DependencyGovernedTargetType
    {
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.Uncovered
{
    internal sealed class UncoveredSourceType
    {
        public ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.UncoveredTarget.UncoveredTargetType? Reference;
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.UncoveredTarget
{
    internal sealed class UncoveredTargetType
    {
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.Excluded
{
    internal sealed class ExcludedSourceType
    {
        public ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.ExcludedTarget.ExcludedTargetType? Reference;
    }
}

namespace ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures.ExcludedTarget
{
    internal sealed class ExcludedTargetType
    {
    }
}

#pragma warning restore CS0649
