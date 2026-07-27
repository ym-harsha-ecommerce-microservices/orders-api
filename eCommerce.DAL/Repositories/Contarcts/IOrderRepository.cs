using eCommerce.DAL.Entities;
using MongoDB.Driver;

namespace eCommerce.DAL.Repositories.Contarcts;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetAllOrdersAsync();
    Task<IEnumerable<Order>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter);
    Task<Order?> GetOrderByConditionAsync(FilterDefinition<Order> filter);
    Task<Order?> CreateOrderAsync(Order order);
    Task<Order?> UpdateOrderAsync(Order order);
    Task<bool> DeleteOrderAsync(Guid orderId);

}
