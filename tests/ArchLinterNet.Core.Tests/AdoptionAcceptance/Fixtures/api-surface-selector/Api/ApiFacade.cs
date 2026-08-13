namespace Synthetic.ApiSurfaceSelector.Api;

// Selected by the namespace-selected-api contract, a second bounded selector source that carries
// no marker attribute at all — proving selection is not annotation-specific.
public sealed class ApiFacade
{
    public string Ping() => "pong";
}
