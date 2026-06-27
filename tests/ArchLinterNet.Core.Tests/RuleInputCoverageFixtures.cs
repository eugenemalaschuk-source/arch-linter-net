#pragma warning disable CS0649 // Fields exist only so the architecture scanners can observe namespace presence and references.

namespace ArchLinterNet.Core.Tests.RuleInputCoverageFixtures.Audio
{
    internal sealed class AudioType
    {
    }
}

namespace ArchLinterNet.Core.Tests.RuleInputCoverageFixtures.Video
{
    internal sealed class VideoType
    {
        public ArchLinterNet.Core.Tests.RuleInputCoverageFixtures.Audio.AudioType? Audio;
    }
}

#pragma warning restore CS0649
