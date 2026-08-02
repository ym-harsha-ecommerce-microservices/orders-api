using eCommerce.BLL.DTO.Order;
using eCommerce.DAL.Entities;
using MongoDB.Driver;

namespace eCommerce.BLL.Services.Contarcts;

/// <summary>
/// Defines the business logic operations for managing customer orders.
/// </summary>
public interface IOrdersService
{
    /// <summary>
    /// Retrieves all orders.
    /// </summary>
    /// <returns>A list of all orders.</returns>
    Task<List<OrderResponse>> GetAllOrdersAsync();

    /// <summary>
    /// Retrieves all orders matching the specified filter condition.
    /// </summary>
    /// <param name="filter">The MongoDB filter used to match orders.</param>
    /// <returns>A list of orders matching the filter.</returns>
    Task<List<OrderResponse>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter);

    /// <summary>
    /// Retrieves a single order matching the specified filter condition.
    /// </summary>
    /// <param name="filter">The MongoDB filter used to match the order.</param>
    /// <returns>The matching order.</returns>
    Task<OrderResponse> GetOrderByConditionAsync(FilterDefinition<Order> filter);

    /// <summary>
    /// Creates a new order.
    /// </summary>
    /// <param name="orderAddRequest">The order details to create.</param>
    /// <returns>The newly created order.</returns>
    Task<OrderResponse?> CreateOrderAsync(OrderAddRequest orderAddRequest);

    /// <summary>
    /// Updates an existing order.
    /// </summary>
    /// <param name="orderUpdateRequest">The updated order details.</param>
    /// <returns>The updated order.</returns>
    Task<OrderResponse?> UpdateOrderAsync(OrderUpdateRequest orderUpdateRequest);

    /// <summary>
    /// Deletes an order by its unique Order ID.
    /// </summary>
    /// <param name="orderID">The unique identifier of the order to delete.</param>
    Task DeleteOrderAsync(Guid orderID);
}