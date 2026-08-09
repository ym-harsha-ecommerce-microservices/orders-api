using eCommerce.BLL.DTO.UsersMicroservice;
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

public class UsersMicroserviceHttpClientTest
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<UsersMicroserviceHttpClient>> _loggerMock;

    public UsersMicroserviceHttpClientTest()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<UsersMicroserviceHttpClient>>();
    }

    private UsersMicroserviceHttpClient BuildClient(Func<HttpRequestMessage, HttpResponseMessage> responder, out FakeHttpMessageHandler handler)
    {
        var httpClient = FakeHttpMessageHandler.CreateClient(responder, out handler);
        return new UsersMicroserviceHttpClient(httpClient, _cacheServiceMock.Object, _loggerMock.Object);
    }

    #region GetUserByUserIDAsync

    [Fact]
    public async Task GetUserByUserIDAsync_CacheHit_ReturnsCached_SkipsHttpCall()
    {
        // Arrange
        var cachedUser = new UserDTO { UserID = Guid.NewGuid(), PersonName = "Cached User" };
        _cacheServiceMock.Setup(temp => temp.GetAsync<UserDTO>(It.IsAny<string>())).ReturnsAsync(cachedUser);

        var client = BuildClient(_ => throw new InvalidOperationException("HTTP should not be called on a cache hit"), out var handler);

        // Act
        var result = await client.GetUserByUserIDAsync(cachedUser.UserID);

        // Assert
        result.Should().Be(cachedUser);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserByUserIDAsync_CacheMiss_Success_CachesAndReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserDTO { UserID = userId, PersonName = "Fresh User" };

        _cacheServiceMock.Setup(temp => temp.GetAsync<UserDTO>(It.IsAny<string>())).ReturnsAsync(null as UserDTO);
        var client = BuildClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(user)
        }, out var handler);

        // Act
        var result = await client.GetUserByUserIDAsync(userId);

        // Assert
        result.Should().BeEquivalentTo(user);
        handler.Requests.Should().ContainSingle(r => r.RequestUri!.ToString().Contains(userId.ToString()));
        _cacheServiceMock.Verify(
            temp => temp.SetAsync(It.IsAny<string>(), It.IsAny<UserDTO>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByUserIDAsync_NotFound_ReturnsNull()
    {
        // Arrange
        _cacheServiceMock.Setup(temp => temp.GetAsync<UserDTO>(It.IsAny<string>())).ReturnsAsync(null as UserDTO);
        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        // Act
        var result = await client.GetUserByUserIDAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByUserIDAsync_ServiceUnavailable_ReturnsDummyFallback()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _cacheServiceMock.Setup(temp => temp.GetAsync<UserDTO>(It.IsAny<string>())).ReturnsAsync(null as UserDTO);
        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), out _);

        // Act
        var result = await client.GetUserByUserIDAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.UserID.Should().Be(userId);
        result.PersonName.Should().Be("Temporarily Unavailable");
    }

    #endregion

    #region GetUsersByIdsAsync

    [Fact]
    public async Task GetUsersByIdsAsync_EmptyList_ReturnsEmpty_NoCalls()
    {
        // Arrange
        var client = BuildClient(_ => throw new InvalidOperationException("should not be called"), out var handler);

        // Act
        var result = await client.GetUsersByIdsAsync(new List<Guid>());

        // Assert
        result.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersByIdsAsync_PartialCacheMiss_FetchesOnlyMissing()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var cachedUser = new UserDTO { UserID = id1 };
        var fetchedUser = new UserDTO { UserID = id2 };

        // NOTE: the real cache keys come from CacheKeys.UserDetails(id), whose
        // exact format we don't know here - so we capture whatever keys the
        // client actually passes in and build the returned dictionary from
        // those (same order as the requested IDs: id1 first, id2 second).
        _cacheServiceMock
            .Setup(temp => temp.GetBulkAsync<UserDTO>(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(keys =>
            {
                var keyList = keys.ToList();
                return Task.FromResult<IDictionary<string, UserDTO?>>(new Dictionary<string, UserDTO?>
                {
                    [keyList[0]] = cachedUser,
                    [keyList[1]] = null
                });
            });

        var client = BuildClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<UserDTO> { fetchedUser })
        }, out var handler);

        // Act
        var result = await client.GetUsersByIdsAsync(new List<Guid> { id1, id2 });

        // Assert
        result.Should().BeEquivalentTo(new[] { cachedUser, fetchedUser });
        handler.Requests.Should().ContainSingle();
        _cacheServiceMock.Verify(
            temp => temp.SetBulkAsync(It.IsAny<IDictionary<string, UserDTO>>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUsersByIdsAsync_ServiceUnavailableOnFetch_ReturnsPartialCachedResults()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var cachedUser = new UserDTO { UserID = id1 };

        _cacheServiceMock
            .Setup(temp => temp.GetBulkAsync<UserDTO>(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(keys =>
            {
                var keyList = keys.ToList();
                return Task.FromResult<IDictionary<string, UserDTO?>>(new Dictionary<string, UserDTO?>
                {
                    [keyList[0]] = cachedUser,
                    [keyList[1]] = null
                });
            });

        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), out _);

        // Act
        var result = await client.GetUsersByIdsAsync(new List<Guid> { id1, id2 });

        // Assert
        result.Should().BeEquivalentTo(new[] { cachedUser });
    }

    #endregion
}