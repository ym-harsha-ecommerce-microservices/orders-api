using AutoFixture;
using AutoFixture.AutoMoq;
using AutoMapper;
using eCommerce.BLL.DTO.Order;
using eCommerce.BLL.DTO.OrderItem;
using eCommerce.BLL.DTO.ProductsMicroservice;
using eCommerce.BLL.DTO.UsersMicroservice;
using eCommerce.BLL.Exceptions;
using eCommerce.BLL.HttpClients;
using eCommerce.BLL.Services.Implementations;
using eCommerce.DAL.Entities;
using eCommerce.DAL.Repositories.Contracts;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using Xunit;

namespace eCommerce.Tests.ServicesTests;

public class OrderServiceTest
{
    private readonly IFixture _fixture;

    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IUsersMicroserviceHttpClient> _usersHttpClientMock;
    private readonly Mock<IProductsMicroserviceHttpClient> _productsHttpClientMock;

    private readonly OrderService _orderService;

    public OrderServiceTest()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());

        _orderRepositoryMock = _fixture.Freeze<Mock<IOrderRepository>>();
        _mapperMock = _fixture.Freeze<Mock<IMapper>>();
        _usersHttpClientMock = _fixture.Freeze<Mock<IUsersMicroserviceHttpClient>>();
        _productsHttpClientMock = _fixture.Freeze<Mock<IProductsMicroserviceHttpClient>>();

        // NOTE: OrderService currently depends on the *concrete*
        // UsersMicroserviceHttpClient / ProductsMicroserviceHttpClient classes.
        // These tests assume its constructor has been changed to depend on
        // IUsersMicroserviceHttpClient / IProductsMicroserviceHttpClient instead
        // (see IUsersMicroserviceHttpClient.cs / IProductsMicroserviceHttpClient.cs) -
        // concrete classes without virtual members can't be mocked by Moq.
        _orderService = new OrderService(
            _orderRepositoryMock.Object,
            _mapperMock.Object,
            _usersHttpClientMock.Object,
            _productsHttpClientMock.Object);
    }

    private static OrderAddRequest BuildOrderAddRequest(Guid userId, List<Guid> productIds)
    {
        return new OrderAddRequest
        {
            UserID = userId,
            OrderDate = DateTime.UtcNow,
            OrderItems = productIds.Select(id => new OrderItemAddRequest
            {
                ProductID = id,
                UnitPrice = 10,
                Quantity = 2
            }).ToList()
        };
    }

    #region CreateOrderAsync

    [Fact]
    public async Task CreateOrderAsync_UserNotFound_ThrowsBadRequestException()
    {
        // Arrange
        var productIds = _fixture.CreateMany<Guid>(2).ToList();
        var request = BuildOrderAddRequest(_fixture.Create<Guid>(), productIds);

        _usersHttpClientMock
            .Setup(temp => temp.GetUserByUserIDAsync(request.UserID))
            .ReturnsAsync(null as UserDTO);

        // Act
        Func<Task> action = async () => await _orderService.CreateOrderAsync(request);

        // Assert
        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CreateOrderAsync_ProductCountMismatch_ThrowsBadRequestException()
    {
        // Arrange
        var productIds = _fixture.CreateMany<Guid>(2).ToList();
        var request = BuildOrderAddRequest(_fixture.Create<Guid>(), productIds);
        var user = _fixture.Create<UserDTO>();

        _usersHttpClientMock
            .Setup(temp => temp.GetUserByUserIDAsync(request.UserID))
            .ReturnsAsync(user);

        // Only 1 product comes back even though 2 were requested -> mismatch.
        _productsHttpClientMock
            .Setup(temp => temp.GetProductsByIdsAsync(productIds))
            .ReturnsAsync(_fixture.CreateMany<ProductDTO>(1).ToList());

        // Act
        Func<Task> action = async () => await _orderService.CreateOrderAsync(request);

        // Assert
        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CreateOrderAsync_ValidRequest_RecalculatesTotalsAndSaves()
    {
        // Arrange
        var productIds = _fixture.CreateMany<Guid>(2).ToList();
        var request = BuildOrderAddRequest(_fixture.Create<Guid>(), productIds);

        var user = _fixture.Build<UserDTO>().With(u => u.UserID, request.UserID).Create();
        var products = productIds.Select(id => _fixture.Build<ProductDTO>().With(p => p.ProductID, id).Create()).ToList();

        // This is the entity the (mocked) mapper "produces" from the request.
        // UnitPrice=10, Quantity=2 per item (matching BuildOrderAddRequest) so we
        // can assert the recalculated totals below.
        var mappedOrder = new Order
        {
            UserID = request.UserID,
            OrderDate = request.OrderDate,
            OrderItems = productIds.Select(id => new OrderItem
            {
                ProductID = id,
                UnitPrice = 10,
                Quantity = 2
            }).ToList()
        };

        var orderResponse = _fixture.Create<OrderResponse>();

        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(request.UserID)).ReturnsAsync(user);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(productIds)).ReturnsAsync(products);
        _mapperMock.Setup(temp => temp.Map<Order>(request)).Returns(mappedOrder);
        _mapperMock.Setup(temp => temp.Map<OrderResponse>(mappedOrder)).Returns(orderResponse);

        // Act
        OrderResponse? result = await _orderService.CreateOrderAsync(request);

        // Assert - the response returned is the one the mapper produced
        result.Should().Be(orderResponse);

        // Assert - RecalculateOrderTotals() did its job on the mapped entity
        // before it was persisted: each item's TotalPrice = UnitPrice * Quantity,
        // and the order's TotalBill is the sum across items (2 items * 10 * 2 = 40).
        mappedOrder.OrderItems!.Should().OnlyContain(i => i.TotalPrice == 20);
        mappedOrder.TotalBill.Should().Be(40);

        // Assert - a fresh OrderID was assigned rather than reusing whatever
        // the mapper happened to produce.
        mappedOrder.OrderID.Should().NotBe(Guid.Empty);

        _orderRepositoryMock.Verify(temp => temp.CreateOrderAsync(mappedOrder), Times.Once);

        // Assert - the found user's data was merged onto the response.
        _mapperMock.Verify(temp => temp.Map(user, orderResponse), Times.Once);
    }

    #endregion

    #region DeleteOrderAsync

    [Fact]
    public async Task DeleteOrderAsync_NotDeleted_ThrowsNotFoundException()
    {
        // Arrange
        Guid orderId = _fixture.Create<Guid>();
        _orderRepositoryMock.Setup(temp => temp.DeleteOrderAsync(orderId)).ReturnsAsync(false);

        // Act
        Func<Task> action = async () => await _orderService.DeleteOrderAsync(orderId);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteOrderAsync_Deleted_CompletesSuccessfully()
    {
        // Arrange
        Guid orderId = _fixture.Create<Guid>();
        _orderRepositoryMock.Setup(temp => temp.DeleteOrderAsync(orderId)).ReturnsAsync(true);

        // Act
        Func<Task> action = async () => await _orderService.DeleteOrderAsync(orderId);

        // Assert
        await action.Should().NotThrowAsync();
        _orderRepositoryMock.Verify(temp => temp.DeleteOrderAsync(orderId), Times.Once);
    }

    #endregion

    #region GetAllOrdersAsync

    [Fact]
    public async Task GetAllOrdersAsync_NoOrders_ToBeEmpty_SkipsHttpCalls()
    {
        // Arrange
        _orderRepositoryMock.Setup(temp => temp.GetAllOrdersAsync()).ReturnsAsync(new List<Order>());

        // Act
        List<OrderResponse> result = await _orderService.GetAllOrdersAsync();

        // Assert
        result.Should().BeEmpty();

        // The empty-list short-circuit should mean no product/user lookups happen at all.
        _productsHttpClientMock.Verify(temp => temp.GetProductsByIdsAsync(It.IsAny<List<Guid>>()), Times.Never);
        _usersHttpClientMock.Verify(temp => temp.GetUsersByIdsAsync(It.IsAny<List<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task GetAllOrdersAsync_WithOrders_ToBeSuccessful()
    {
        // Arrange
        List<Order> orders = _fixture.CreateMany<Order>(2).ToList();

        var allProductIds = orders.SelectMany(o => o.OrderItems!).Select(i => i.ProductID).Distinct().ToList();
        var allUserIds = orders.Select(o => o.UserID).Distinct().ToList();

        _orderRepositoryMock.Setup(temp => temp.GetAllOrdersAsync()).ReturnsAsync(orders);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(allProductIds)).ReturnsAsync(new List<ProductDTO>());
        _usersHttpClientMock.Setup(temp => temp.GetUsersByIdsAsync(allUserIds)).ReturnsAsync(new List<UserDTO>());

        foreach (var order in orders)
        {
            _mapperMock.Setup(temp => temp.Map<OrderResponse>(order)).Returns(_fixture.Create<OrderResponse>());
        }

        // Act
        List<OrderResponse> result = await _orderService.GetAllOrdersAsync();

        // Assert
        result.Should().HaveCount(2);

        // Bulk lookups (not per-order lookups) should be used for efficiency.
        _productsHttpClientMock.Verify(temp => temp.GetProductsByIdsAsync(allProductIds), Times.Once);
        _usersHttpClientMock.Verify(temp => temp.GetUsersByIdsAsync(allUserIds), Times.Once);
    }

    #endregion

    #region GetAllOrdersByConditionAsync

    [Fact]
    public async Task GetAllOrdersByConditionAsync_NoMatches_ToBeEmpty()
    {
        // Arrange
        var filter = Builders<Order>.Filter.Eq(o => o.UserID, _fixture.Create<Guid>());
        _orderRepositoryMock.Setup(temp => temp.GetAllOrdersByConditionAsync(filter)).ReturnsAsync(new List<Order>());

        // Act
        List<OrderResponse> result = await _orderService.GetAllOrdersByConditionAsync(filter);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetOrderByConditionAsync

    [Fact]
    public async Task GetOrderByConditionAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        var filter = Builders<Order>.Filter.Eq(o => o.OrderID, _fixture.Create<Guid>());
        _orderRepositoryMock.Setup(temp => temp.GetOrderByConditionAsync(filter)).ReturnsAsync(null as Order);

        // Act
        Func<Task> action = async () => await _orderService.GetOrderByConditionAsync(filter);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetOrderByConditionAsync_Found_ToBeSuccessful()
    {
        // Arrange
        Order order = _fixture.Create<Order>();
        var productIds = order.OrderItems!.Select(i => i.ProductID).ToList();
        var filter = Builders<Order>.Filter.Eq(o => o.OrderID, order.OrderID);

        var user = _fixture.Build<UserDTO>().With(u => u.UserID, order.UserID).Create();
        var orderResponse = _fixture.Create<OrderResponse>();

        _orderRepositoryMock.Setup(temp => temp.GetOrderByConditionAsync(filter)).ReturnsAsync(order);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(productIds)).ReturnsAsync(new List<ProductDTO>());
        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(order.UserID)).ReturnsAsync(user);
        _mapperMock.Setup(temp => temp.Map<OrderResponse>(order)).Returns(orderResponse);

        // Act
        OrderResponse result = await _orderService.GetOrderByConditionAsync(filter);

        // Assert
        result.Should().Be(orderResponse);

        // Single-order lookup uses the singular user endpoint, not the bulk one.
        _usersHttpClientMock.Verify(temp => temp.GetUserByUserIDAsync(order.UserID), Times.Once);
        _usersHttpClientMock.Verify(temp => temp.GetUsersByIdsAsync(It.IsAny<List<Guid>>()), Times.Never);
    }

    #endregion

    #region UpdateOrderAsync

    private OrderUpdateRequest BuildOrderUpdateRequest(Guid orderId, Guid userId, List<Guid> productIds)
    {
        return new OrderUpdateRequest
        {
            OrderID = orderId,
            UserID = userId,
            OrderDate = DateTime.UtcNow,
            OrderItems = productIds.Select(id => new OrderItemUpdateRequest
            {
                ProductID = id,
                UnitPrice = 5,
                Quantity = 1
            }).ToList()
        };
    }

    [Fact]
    public async Task UpdateOrderAsync_UserNotFound_ThrowsBadRequestException()
    {
        // Arrange
        var request = BuildOrderUpdateRequest(_fixture.Create<Guid>(), _fixture.Create<Guid>(), _fixture.CreateMany<Guid>(1).ToList());
        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(request.UserID)).ReturnsAsync(null as UserDTO);

        // Act
        Func<Task> action = async () => await _orderService.UpdateOrderAsync(request);

        // Assert
        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task UpdateOrderAsync_OrderNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var productIds = _fixture.CreateMany<Guid>(1).ToList();
        var request = BuildOrderUpdateRequest(_fixture.Create<Guid>(), _fixture.Create<Guid>(), productIds);
        var user = _fixture.Create<UserDTO>();

        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(request.UserID)).ReturnsAsync(user);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(productIds)).ReturnsAsync(
            productIds.Select(id => _fixture.Build<ProductDTO>().With(p => p.ProductID, id).Create()).ToList());

        _orderRepositoryMock
            .Setup(temp => temp.GetOrderByConditionAsync(It.IsAny<FilterDefinition<Order>>()))
            .ReturnsAsync(null as Order);

        // Act
        Func<Task> action = async () => await _orderService.UpdateOrderAsync(request);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateOrderAsync_UserIdMismatch_ThrowsBadRequestException()
    {
        // Arrange - the order being updated belongs to a DIFFERENT user than
        // the one in the request, which the service should reject.
        var productIds = _fixture.CreateMany<Guid>(1).ToList();
        var request = BuildOrderUpdateRequest(_fixture.Create<Guid>(), _fixture.Create<Guid>(), productIds);
        var user = _fixture.Create<UserDTO>();
        var existingOrder = _fixture.Build<Order>().With(o => o.OrderID, request.OrderID).Create();
        // existingOrder.UserID is a random AutoFixture value, guaranteed (for
        // practical purposes) not to equal request.UserID.

        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(request.UserID)).ReturnsAsync(user);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(productIds)).ReturnsAsync(
            productIds.Select(id => _fixture.Build<ProductDTO>().With(p => p.ProductID, id).Create()).ToList());
        _orderRepositoryMock
            .Setup(temp => temp.GetOrderByConditionAsync(It.IsAny<FilterDefinition<Order>>()))
            .ReturnsAsync(existingOrder);

        // Act
        Func<Task> action = async () => await _orderService.UpdateOrderAsync(request);

        // Assert
        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task UpdateOrderAsync_RepositoryUpdateReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var productIds = _fixture.CreateMany<Guid>(1).ToList();
        Guid userId = _fixture.Create<Guid>();
        var request = BuildOrderUpdateRequest(_fixture.Create<Guid>(), userId, productIds);
        var user = _fixture.Build<UserDTO>().With(u => u.UserID, userId).Create();
        var existingOrder = _fixture.Build<Order>()
            .With(o => o.OrderID, request.OrderID)
            .With(o => o.UserID, userId)
            .Create();

        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(userId)).ReturnsAsync(user);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(productIds)).ReturnsAsync(
            productIds.Select(id => _fixture.Build<ProductDTO>().With(p => p.ProductID, id).Create()).ToList());
        _orderRepositoryMock
            .Setup(temp => temp.GetOrderByConditionAsync(It.IsAny<FilterDefinition<Order>>()))
            .ReturnsAsync(existingOrder);
        _orderRepositoryMock
            .Setup(temp => temp.UpdateOrderAsync(existingOrder))
            .ReturnsAsync(null as Order);

        // Act
        Func<Task> action = async () => await _orderService.UpdateOrderAsync(request);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateOrderAsync_ValidRequest_ToBeSuccessful()
    {
        // Arrange
        var productIds = _fixture.CreateMany<Guid>(1).ToList();
        Guid userId = _fixture.Create<Guid>();
        var request = BuildOrderUpdateRequest(_fixture.Create<Guid>(), userId, productIds);
        var user = _fixture.Build<UserDTO>().With(u => u.UserID, userId).Create();
        var existingOrder = _fixture.Build<Order>()
            .With(o => o.OrderID, request.OrderID)
            .With(o => o.UserID, userId)
            .Create();
        var orderResponse = _fixture.Create<OrderResponse>();

        _usersHttpClientMock.Setup(temp => temp.GetUserByUserIDAsync(userId)).ReturnsAsync(user);
        _productsHttpClientMock.Setup(temp => temp.GetProductsByIdsAsync(productIds)).ReturnsAsync(
            productIds.Select(id => _fixture.Build<ProductDTO>().With(p => p.ProductID, id).Create()).ToList());
        _orderRepositoryMock
            .Setup(temp => temp.GetOrderByConditionAsync(It.IsAny<FilterDefinition<Order>>()))
            .ReturnsAsync(existingOrder);
        _orderRepositoryMock
            .Setup(temp => temp.UpdateOrderAsync(existingOrder))
            .ReturnsAsync(existingOrder);
        _mapperMock.Setup(temp => temp.Map<OrderResponse>(existingOrder)).Returns(orderResponse);

        // Act
        OrderResponse? result = await _orderService.UpdateOrderAsync(request);

        // Assert
        result.Should().Be(orderResponse);

        // The request DTO should have been mapped onto the existing entity
        // (in-place update), not used to construct a brand-new one.
        _mapperMock.Verify(temp => temp.Map(request, existingOrder), Times.Once);
    }

    #endregion
}