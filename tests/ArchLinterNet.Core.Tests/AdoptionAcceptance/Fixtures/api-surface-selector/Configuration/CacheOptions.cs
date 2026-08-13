namespace Synthetic.ApiSurfaceSelector.Configuration;

// Incidental CLR-public configuration type, part of the large exported surface the selected
// snapshots must exclude.
public sealed class CacheOptions
{
    public int SizeLimit { get; set; }
}
