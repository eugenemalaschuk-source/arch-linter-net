using TopologyReview.Domain;

namespace TopologyReview.Application;

public sealed class OrderService
{
    public Order Get(string orderId) => new(orderId, 42m);
}
