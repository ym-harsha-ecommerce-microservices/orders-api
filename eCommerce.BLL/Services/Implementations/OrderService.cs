using AutoMapper;
using eCommerce.BLL.DTO.Order;
using eCommerce.BLL.Exceptions;
using eCommerce.BLL.Services.Contarcts;
using eCommerce.DAL.Entities;
using eCommerce.DAL.Repositories.Contarcts;
using MongoDB.Driver;

namespace eCommerce.BLL.Services.Implementations;

public class OrderService(IOrderRepository _orderRepository, IMapper _mapper) : IOrdersService
{
    public async Task<OrderResponse?> CreateOrderAsync(OrderAddRequest orderAddRequest)
    {
        var order = _mapper.Map<Order>(orderAddRequest);
        order.OrderID = Guid.NewGuid();

        RecalculateOrderTotals(order);

        await _orderRepository.CreateOrderAsync(order);

        return _mapper.Map<OrderResponse>(order);
    }

    public async Task DeleteOrderAsync(Guid orderID)
    {
        var deleted = await _orderRepository.DeleteOrderAsync(orderID);
        if (!deleted)
            throw new NotFoundException($"Order with ID {orderID} was not found.");

    }

    public async Task<List<OrderResponse>> GetAllOrdersAsync()
    {
        var orders = await _orderRepository.GetAllOrdersAsync();
        return _mapper.Map<List<OrderResponse>>(orders);
    }

    public async Task<List<OrderResponse>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter)
    {
        var orders = await _orderRepository.GetAllOrdersByConditionAsync(filter);
        return _mapper.Map<List<OrderResponse>>(orders);

    }

    public async Task<OrderResponse> GetOrderByConditionAsync(FilterDefinition<Order> filter)
    {
        var order = await _orderRepository.GetOrderByConditionAsync(filter);

        if (order == null) throw new NotFoundException();

        return _mapper.Map<OrderResponse>(order);

    }

    public async Task<OrderResponse> UpdateOrderAsync(OrderUpdateRequest orderUpdateRequest)
    {
        var builder = Builders<Order>.Filter;

        var filter = builder.Eq(o => o.OrderID, orderUpdateRequest.OrderID);

        var order = await _orderRepository.GetOrderByConditionAsync(filter);

        if (order == null) throw new NotFoundException();

        if (order.UserID != orderUpdateRequest.UserID)
            throw new BadRequestException("Cannot change the user associated with an existing order.");

        _mapper.Map(orderUpdateRequest, order);

        RecalculateOrderTotals(order);

        order = await _orderRepository.UpdateOrderAsync(order)
        ?? throw new NotFoundException($"Order with ID {orderUpdateRequest.OrderID} was not found.");

        return _mapper.Map<OrderResponse>(order);
    }

    private static void RecalculateOrderTotals(Order order)
    {
        if (order.OrderItems == null)
        {
            order.TotalBill = 0;
            return;
        }

        foreach (var item in order.OrderItems)
        {
            item.TotalPrice = (item.UnitPrice ?? 0) * (item.Quantity ?? 0);
        }

        order.TotalBill = order.OrderItems.Sum(i => i.TotalPrice ?? 0);
    }
}
