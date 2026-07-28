using eCommerce.DAL.Contexts;
using eCommerce.DAL.Entities;
using eCommerce.DAL.Repositories.Contracts;
using MongoDB.Driver;

namespace eCommerce.DAL.Repositories.Implementations;

public class OrderRepository : IOrderRepository
{
    private readonly MongoDB.Driver.IMongoCollection<Order> _orders;

    public OrderRepository(MongoDbContext context)
    {
        _orders = context.Orders;
    }
    /// <inheritdoc/>
    public async Task<Order?> CreateOrderAsync(Order order)
    {
        await _orders.InsertOneAsync(order);
        return order;
    }

    /// <inheritdoc/>


    public async Task<bool> DeleteOrderAsync(Guid orderId)
    {
        var res = await _orders.DeleteOneAsync(o => o.OrderID == orderId);
        if (res.DeletedCount > 0)
            return true;
        return false;
    }

    /// <inheritdoc/>

    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        return await _orders.Find(FilterDefinition<Order>.Empty).ToListAsync();
    }
    /// <inheritdoc/>

    public async Task<IEnumerable<Order>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter)
    {
        return await _orders.Find(filter).ToListAsync();
    }
    /// <inheritdoc/>

    public async Task<Order?> GetOrderByConditionAsync(FilterDefinition<Order> filter)
    {
        return await _orders.Find(filter).FirstOrDefaultAsync();
    }
    /// <inheritdoc/>

    public async Task<Order?> UpdateOrderAsync(Order order)
    {
        var result = await _orders.ReplaceOneAsync(o => o.OrderID == order.OrderID, order);
        return result.MatchedCount > 0 ? order : null;
    }
}
