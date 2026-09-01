using TopologyReview.Unity.Gameplay;

namespace TopologyReview.Unity.Editor;

public sealed class GameplayInspector
{
    public string Inspect() => new GameplayController().RuntimeName;
}
