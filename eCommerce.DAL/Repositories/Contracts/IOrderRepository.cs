using eCommerce.DAL.Entities;
using MongoDB.Driver;

namespace eCommerce.DAL.Repositories.Contracts;

/// <summary>
/// Defines the data access operations for managing orders in MongoDB.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Retrieves all orders.
    /// </summary>
    /// <returns>A list of all orders.</returns>
    Task<IEnumerable<Order>> GetAllOrdersAsync();

    /// <summary>
    /// Retrieves all orders matching the specified filter condition.
    /// </summary>
    /// <param name="filter">The MongoDB filter used to match orders.</param>
    /// <returns>A list of orders matching the filter.</returns>
    Task<IEnumerable<Order>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter);

    /// <summary>
    /// Retrieves a single order matching the specified filter condition.
    /// </summary>
    /// <param name="filter">The MongoDB filter used to match the order.</param>
    /// <returns>The matching order, or null if none is found.</returns>
    Task<Order?> GetOrderByConditionAsync(FilterDefinition<Order> filter);

    /// <summary>
    /// Inserts a new order into the database.
    /// </summary>
    /// <param name="order">The order to create.</param>
    /// <returns>The created order.</returns>
    Task<Order?> CreateOrderAsync(Order order);

    /// <summary>
    /// Updates an existing order in the database.
    /// </summary>
    /// <param name="order">The order with updated values.</param>
    /// <returns>The updated order, or null if no matching order was found.</returns>
    Task<Order?> UpdateOrderAsync(Order order);

    /// <summary>
    /// Deletes an order by its unique Order ID.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order to delete.</param>
    /// <returns>True if an order was deleted; otherwise, false.</returns>
    Task<bool> DeleteOrderAsync(Guid orderId);
}