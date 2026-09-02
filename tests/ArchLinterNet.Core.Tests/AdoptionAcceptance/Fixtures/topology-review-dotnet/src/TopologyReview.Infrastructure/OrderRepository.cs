using TopologyReview.Domain;

namespace TopologyReview.Infrastructure;

public sealed class OrderRepository
{
    public Order Load(string orderId) => new(orderId, 42m);
}
