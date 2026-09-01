using TopologyReview.Application;

namespace TopologyReview.Server;

public static class Program
{
    public static OrderService Services { get; } = new();

    public static void Main() => _ = Services.Get("sample-order");
}
