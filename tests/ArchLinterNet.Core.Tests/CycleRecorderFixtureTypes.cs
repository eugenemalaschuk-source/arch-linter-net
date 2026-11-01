namespace ArchLinterNet.Core.Tests.CycleRecorderFixtures.Left
{
    public sealed class LeftNode
    {
        public global::ArchLinterNet.Core.Tests.CycleRecorderFixtures.Right.RightNode? Peer { get; set; }
    }
}

namespace ArchLinterNet.Core.Tests.CycleRecorderFixtures.Right
{
    public sealed class RightNode
    {
        public global::ArchLinterNet.Core.Tests.CycleRecorderFixtures.Left.LeftNode? Peer { get; set; }
    }
}

namespace ArchLinterNet.Core.Tests.CycleRecorderFixtures.Isolated
{
    public sealed class IsolatedNode
    {
        public string Name { get; set; } = string.Empty;
    }
}
