using eCommerce.BLL.DTO.ProductsMicroservice;
using eCommerce.BLL.HttpClients;
using eCommerce.BLL.Services.Contracts;
using eCommerce.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace eCommerce.Tests.ServicesTests;

public class ProductsMicroserviceHttpClientTest
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<ProductsMicroserviceHttpClient>> _loggerMock;

    public ProductsMicroserviceHttpClientTest()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<ProductsMicroserviceHttpClient>>();
    }

    private ProductsMicroserviceHttpClient BuildClient(Func<HttpRequestMessage, HttpResponseMessage> responder, out FakeHttpMessageHandler handler)
    {
        var httpClient = FakeHttpMessageHandler.CreateClient(responder, out handler);
        return new ProductsMicroserviceHttpClient(httpClient, _cacheServiceMock.Object, _loggerMock.Object);
    }

    #region GetProductByProductIDAsync

    [Fact]
    public async Task GetProductByProductIDAsync_CacheHit_ReturnsCached_SkipsHttpCall()
    {
        // Arrange
        var cachedProduct = new ProductDTO { ProductID = Guid.NewGuid(), ProductName = "Cached Widget" };

        _cacheServiceMock
            .Setup(temp => temp.GetAsync<ProductDTO>(It.IsAny<string>()))
            .ReturnsAsync(cachedProduct);

        var client = BuildClient(_ => throw new InvalidOperationException("HTTP should not be called on a cache hit"), out var handler);

        // Act
        var result = await client.GetProductByProductIDAsync(cachedProduct.ProductID);

        // Assert
        result.Should().Be(cachedProduct);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductByProductIDAsync_CacheMiss_Success_CachesAndReturnsProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new ProductDTO { ProductID = productId, ProductName = "Fresh Widget" };

        _cacheServiceMock.Setup(temp => temp.GetAsync<ProductDTO>(It.IsAny<string>())).ReturnsAsync(null as ProductDTO);

        var client = BuildClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(product)
        }, out var handler);

        // Act
        var result = await client.GetProductByProductIDAsync(productId);

        // Assert
        result.Should().BeEquivalentTo(product);
        handler.Requests.Should().ContainSingle(r => r.RequestUri!.ToString().Contains(productId.ToString()));

        _cacheServiceMock.Verify(
            temp => temp.SetAsync(It.IsAny<string>(), It.IsAny<ProductDTO>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProductByProductIDAsync_NotFound_ReturnsNull_DoesNotCache()
    {
        // Arrange
        _cacheServiceMock.Setup(temp => temp.GetAsync<ProductDTO>(It.IsAny<string>())).ReturnsAsync(null as ProductDTO);
        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        // Act
        var result = await client.GetProductByProductIDAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
        _cacheServiceMock.Verify(
            temp => temp.SetAsync(It.IsAny<string>(), It.IsAny<ProductDTO>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIDAsync_ServiceUnavailable_ReturnsDummyFallback()
    {
        // Arrange - when the Products API is down (behind the Polly fallback
        // policy returning 503), the client degrades to a placeholder rather
        // than bubbling up an error to the caller.
        var productId = Guid.NewGuid();
        _cacheServiceMock.Setup(temp => temp.GetAsync<ProductDTO>(It.IsAny<string>())).ReturnsAsync(null as ProductDTO);
        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), out _);

        // Act
        var result = await client.GetProductByProductIDAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result!.ProductID.Should().Be(productId);
        result.ProductName.Should().Be("Temporarily Unavailable");
    }

    #endregion

    #region GetProductsByIdsAsync

    [Fact]
    public async Task GetProductsByIdsAsync_EmptyList_ReturnsEmpty_NoCacheOrHttpCalls()
    {
        // Arrange
        var client = BuildClient(_ => throw new InvalidOperationException("should not be called"), out var handler);

        // Act
        var result = await client.GetProductsByIdsAsync(new List<Guid>());

        // Assert
        result.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
        _cacheServiceMock.Verify(temp => temp.GetBulkAsync<ProductDTO>(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetProductsByIdsAsync_AllCached_SkipsHttpCall()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var product1 = new ProductDTO { ProductID = id1 };
        var product2 = new ProductDTO { ProductID = id2 };

        // NOTE: the real cache keys come from CacheKeys.ProductDetails(id), whose
        // exact format we don't know here - so rather than guessing a literal
        // string, we capture whatever keys the client actually passes in and
        // build the returned dictionary from those (in the same order as the
        // requested IDs: id1 first, id2 second).
        _cacheServiceMock
            .Setup(temp => temp.GetBulkAsync<ProductDTO>(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(keys =>
            {
                var keyList = keys.ToList();
                return Task.FromResult<IDictionary<string, ProductDTO?>>(new Dictionary<string, ProductDTO?>
                {
                    [keyList[0]] = product1,
                    [keyList[1]] = product2
                });
            });

        var client = BuildClient(_ => throw new InvalidOperationException("should not be called"), out var handler);

        // Act
        var result = await client.GetProductsByIdsAsync(new List<Guid> { id1, id2 });

        // Assert
        result.Should().BeEquivalentTo(new[] { product1, product2 });
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductsByIdsAsync_PartialCacheMiss_FetchesOnlyMissing_MergesAndCaches()
    {
        // Arrange - id1 is cached, id2 is not, so only id2 should go over HTTP.
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var cachedProduct = new ProductDTO { ProductID = id1 };
        var fetchedProduct = new ProductDTO { ProductID = id2 };

        _cacheServiceMock
            .Setup(temp => temp.GetBulkAsync<ProductDTO>(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(keys =>
            {
                var keyList = keys.ToList();
                return Task.FromResult<IDictionary<string, ProductDTO?>>(new Dictionary<string, ProductDTO?>
                {
                    [keyList[0]] = cachedProduct,
                    [keyList[1]] = null
                });
            });

        var client = BuildClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<ProductDTO> { fetchedProduct })
        }, out var handler);

        // Act
        var result = await client.GetProductsByIdsAsync(new List<Guid> { id1, id2 });

        // Assert
        result.Should().BeEquivalentTo(new[] { cachedProduct, fetchedProduct });
        handler.Requests.Should().ContainSingle();

        _cacheServiceMock.Verify(
            temp => temp.SetBulkAsync(It.IsAny<IDictionary<string, ProductDTO>>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProductsByIdsAsync_ServiceUnavailableOnFetch_ReturnsPartialCachedResults()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var cachedProduct = new ProductDTO { ProductID = id1 };

        _cacheServiceMock
            .Setup(temp => temp.GetBulkAsync<ProductDTO>(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(keys =>
            {
                var keyList = keys.ToList();
                return Task.FromResult<IDictionary<string, ProductDTO?>>(new Dictionary<string, ProductDTO?>
                {
                    [keyList[0]] = cachedProduct,
                    [keyList[1]] = null
                });
            });

        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), out _);

        // Act
        var result = await client.GetProductsByIdsAsync(new List<Guid> { id1, id2 });

        // Assert - only the cached item comes back; the missing one is
        // silently dropped rather than the whole call failing.
        result.Should().BeEquivalentTo(new[] { cachedProduct });
    }

    #endregion
}