using AutoMapper;
using eCommerce.BLL.DTO.Order;
using eCommerce.BLL.DTO.ProductsMicroservice;
using eCommerce.BLL.DTO.UsersMicroservice;
using eCommerce.BLL.Exceptions;
using eCommerce.BLL.HttpClients;
using eCommerce.BLL.Services.Contarcts;
using eCommerce.DAL.Entities;
using eCommerce.DAL.Repositories.Contracts;
using MongoDB.Driver;

namespace eCommerce.BLL.Services.Implementations;

public class OrderService(IOrderRepository _orderRepository, IMapper _mapper,
    UsersMicroserviceHttpClient _usersMicroserviceHttpClient,
    ProductsMicroserviceHttpClient _productsMicroserviceHttpClient) : IOrdersService
{
    /// <inheritdoc/>
    public async Task<OrderResponse?> CreateOrderAsync(OrderAddRequest orderAddRequest)
    {
        var user = await ValidateUserAsync(orderAddRequest.UserID);

        var productsList = await ValidateProductsAsync(orderAddRequest.OrderItems.Select(i => i.ProductID).ToList());

        var order = _mapper.Map<Order>(orderAddRequest);
        order.OrderID = Guid.NewGuid();

        RecalculateOrderTotals(order);

        await _orderRepository.CreateOrderAsync(order);

        return GetOrderResponse(order, productsList, [user]);
    }

    /// <inheritdoc/>
    public async Task DeleteOrderAsync(Guid orderID)
    {
        var deleted = await _orderRepository.DeleteOrderAsync(orderID);
        if (!deleted)
            throw new NotFoundException($"Order with ID {orderID} was not found.");
    }

    /// <inheritdoc/>
    public async Task<List<OrderResponse>> GetAllOrdersAsync()
    {
        var orders = await _orderRepository.GetAllOrdersAsync();
        return await GetOrderResponsesAsync(orders);
    }

    /// <inheritdoc/>
    public async Task<List<OrderResponse>> GetAllOrdersByConditionAsync(FilterDefinition<Order> filter)
    {
        var orders = await _orderRepository.GetAllOrdersByConditionAsync(filter);
        return await GetOrderResponsesAsync(orders);
    }

    /// <inheritdoc/>
    public async Task<OrderResponse> GetOrderByConditionAsync(FilterDefinition<Order> filter)
    {
        var order = await _orderRepository.GetOrderByConditionAsync(filter);

        if (order == null) throw new NotFoundException();

        var productIds = order.OrderItems?.Select(i => i.ProductID).ToList() ?? new List<Guid>();
        var productsList = await _productsMicroserviceHttpClient.GetProductsByIdsAsync(productIds);

        var user = await _usersMicroserviceHttpClient.GetUserByUserIDAsync(order.UserID);

        var userList = user != null ? [user] : new List<UserDTO>();

        return GetOrderResponse(order, productsList, userList);
    }

    /// <inheritdoc/>
    public async Task<OrderResponse?> UpdateOrderAsync(OrderUpdateRequest orderUpdateRequest)
    {
        var user = await ValidateUserAsync(orderUpdateRequest.UserID);

        var productsList = await ValidateProductsAsync(orderUpdateRequest.OrderItems.Select(i => i.ProductID).ToList());

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

        return GetOrderResponse(order, productsList, [user]);
    }


    // =====================================================================
    // Helper Methods
    // =====================================================================

    private async Task<UserDTO> ValidateUserAsync(Guid userId)
    {
        var user = await _usersMicroserviceHttpClient.GetUserByUserIDAsync(userId);

        if (user == null)
        {
            throw new BadRequestException($"User with ID {userId} does not exist.");
        }

        return user;
    }
    private async Task<List<ProductDTO>> ValidateProductsAsync(List<Guid> productIds)
    {
        var productsList = await _productsMicroserviceHttpClient.GetProductsByIdsAsync(productIds);

        if (productsList.Count != productIds.Count)
        {
            throw new BadRequestException("One or more products do not exist.");
        }

        return productsList;
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
    private OrderResponse GetOrderResponse(Order order, List<ProductDTO> productsList, List<UserDTO> users)
    {
        var orderResponse = _mapper.Map<OrderResponse>(order);

        if (users != null && users.Any())
        {

            var userDto = users.FirstOrDefault(u => u != null && u.UserID == order.UserID);

            if (userDto != null)
                _mapper.Map(userDto, orderResponse);
        }

        if (orderResponse.OrderItems != null)
        {
            foreach (var itemResponse in orderResponse.OrderItems)
            {
                var productDto = productsList.FirstOrDefault(p => p != null && p.ProductID == itemResponse.ProductID);

                if (productDto != null)
                {
                    _mapper.Map(productDto, itemResponse);
                }
            }
        }

        return orderResponse;
    }

    private async Task<List<OrderResponse>> GetOrderResponsesAsync(IEnumerable<Order> orders)
    {
        if (!orders.Any()) return new List<OrderResponse>();

        var allProductIds = orders
            .Where(o => o.OrderItems != null)
            .SelectMany(o => o.OrderItems!)
            .Select(i => i.ProductID)
            .Distinct()
            .ToList();

        var allProductsList = await _productsMicroserviceHttpClient.GetProductsByIdsAsync(allProductIds);

        var allUserIds = orders
                .Select(o => o.UserID)
                .Distinct()
                .ToList();

        var allUsersList = await _usersMicroserviceHttpClient.GetUsersByIdsAsync(allUserIds);

        var orderResponses = new List<OrderResponse>();

        foreach (var order in orders)
        {
            var orderResponse = GetOrderResponse(order, allProductsList, allUsersList);
            orderResponses.Add(orderResponse);
        }

        return orderResponses;
    }

}