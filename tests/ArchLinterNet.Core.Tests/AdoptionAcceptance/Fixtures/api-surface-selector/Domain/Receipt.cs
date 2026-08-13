using Synthetic.ApiSurfaceSelector.Architecture;

namespace Synthetic.ApiSurfaceSelector.Domain;

// The exact-delta-lifecycle probe for the marker-selected surface. The gate test rewrites this
// file in place to prove added/removed/changed signatures are all observed through the reviewed
// snapshot lifecycle for a selected type, exactly as an unselected #94 contract already proves.
[PublicApiContract]
public sealed class Receipt
{
    public string Removed() => "removed";

    public int Changed(int value) => value;
}
