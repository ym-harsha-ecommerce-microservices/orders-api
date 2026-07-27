using eCommerce.BLL.DTO.Order;
using eCommerce.BLL.Services.Contarcts;
using eCommerce.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace eCommerce.API.Contollers;

/// <summary>
/// Handles CRUD operations and search queries for customer orders.
/// </summary>
public class OrdersController(IOrdersService _ordersService) : ApiBaseController
{
    /// <summary>
    /// Retrieves all orders.
    /// </summary>
    /// <returns>A list of all orders.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _ordersService.GetAllOrdersAsync();
        return Ok(orders);
    }

    /// <summary>
    /// Retrieves a single order by its unique Order ID.
    /// </summary>
    /// <param name="orderID">The unique identifier of the order.</param>
    /// <returns>The matching order, or 404 if not found.</returns>
    [HttpGet("search/orderid/{orderID:guid}")]
    public async Task<IActionResult> GetOrderByID([FromRoute] Guid orderID)
    {
        var filter = Builders<Order>.Filter.Eq(o => o.OrderID, orderID);
        var order = await _ordersService.GetOrderByConditionAsync(filter);
        return Ok(order);
    }

    /// <summary>
    /// Retrieves all orders that contain a specific product.
    /// </summary>
    /// <param name="productID">The unique identifier of the product to search for.</param>
    /// <returns>A list of orders containing the specified product.</returns>
    [HttpGet("search/productid/{productID:guid}")]
    public async Task<IActionResult> GetAllOrdersContainsThisProduct([FromRoute] Guid productID)
    {
        var filter = Builders<Order>.Filter.ElemMatch(o => o.OrderItems, i => i.ProductID == productID);
        var orders = await _ordersService.GetAllOrdersByConditionAsync(filter);
        return Ok(orders);
    }

    /// <summary>
    /// Retrieves all orders placed on a specific date.
    /// </summary>
    /// <param name="orderDate">The order date to filter by.</param>
    /// <returns>A list of orders placed on the specified date.</returns>
    [HttpGet("search/orderDate/{orderDate:datetime}")]
    public async Task<IActionResult> GetAllOrdersByDate([FromRoute] DateTime orderDate)
    {
        var startOfDay = orderDate.Date;
        var endOfDay = startOfDay.AddDays(1);

        var filter = Builders<Order>.Filter.And(
            Builders<Order>.Filter.Gte(o => o.OrderDate, startOfDay),
            Builders<Order>.Filter.Lt(o => o.OrderDate, endOfDay)
        );
        var orders = await _ordersService.GetAllOrdersByConditionAsync(filter);
        return Ok(orders);
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    /// <param name="orderAddRequest">The order details to create.</param>
    /// <returns>The newly created order.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderAddRequest orderAddRequest)
    {
        var order = await _ordersService.CreateOrderAsync(orderAddRequest);
        return CreatedAtAction(nameof(GetOrderByID), new { orderID = order.OrderID }, order);
    }

    /// <summary>
    /// Updates an existing order.
    /// </summary>
    /// <param name="orderID">The unique identifier of the order to update.</param>
    /// <param name="orderUpdateRequest">The updated order details.</param>
    /// <returns>The updated order.</returns>
    [HttpPut("{orderID:guid}")]
    public async Task<IActionResult> UpdateOrder([FromRoute] Guid orderID, [FromBody] OrderUpdateRequest orderUpdateRequest)
    {
        if (orderID != orderUpdateRequest.OrderID)
            return BadRequest("The order ID in the route does not match the order ID in the request body.");

        var order = await _ordersService.UpdateOrderAsync(orderUpdateRequest);
        return Ok(order);
    }

    /// <summary>
    /// Deletes an order by its unique Order ID.
    /// </summary>
    /// <param name="orderID">The unique identifier of the order to delete.</param>
    /// <returns>204 No Content if deletion succeeds.</returns>
    [HttpDelete("{orderID:guid}")]
    public async Task<IActionResult> DeleteOrder([FromRoute] Guid orderID)
    {
        await _ordersService.DeleteOrderAsync(orderID);
        return NoContent();
    }
}