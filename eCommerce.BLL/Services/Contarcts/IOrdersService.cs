using eCommerce.BLL.DTO.Order;
using eCommerce.DAL.Entities;
using MongoDB.Driver;

namespace eCommerce.BLL.Services.Contarcts;

public interface IOrdersService
{
    Task<List<OrderResponse>> GetAllOrdersAsync();
    Task<List<OrderResponse>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter);
    Task<OrderResponse> GetOrderByConditionAsync(FilterDefinition<Order> filter);
    Task<OrderResponse> CreateOrderAsync(OrderAddRequest orderAddRequest);
    Task<OrderResponse> UpdateOrderAsync(OrderUpdateRequest orderUpdateRequest);
    Task DeleteOrderAsync(Guid orderID);

}
