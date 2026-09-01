using TopologyReview.Unity.Runtime;

namespace TopologyReview.Unity.Gameplay;

public sealed class GameplayController
{
    private readonly RuntimeBootstrap _runtime = new();

    public string RuntimeName => _runtime.ProductName;
}
