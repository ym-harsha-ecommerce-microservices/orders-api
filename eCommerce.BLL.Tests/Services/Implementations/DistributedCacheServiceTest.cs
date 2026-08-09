using AutoFixture;
using AutoFixture.AutoMoq;
using eCommerce.BLL.Services.Implementations;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Misc;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace eCommerce.Tests.ServicesTests;

public class DistributedCacheServiceTest
{
    private readonly IFixture _fixture;
    private readonly Mock<IDistributedCache> _distributedCacheMock;
    private readonly DistributedCacheService _cacheService;

    public DistributedCacheServiceTest()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());

        _distributedCacheMock = _fixture.Freeze<Mock<IDistributedCache>>();
        var loggerMock = _fixture.Freeze<Mock<ILogger<DistributedCacheService>>>();

        _cacheService = new DistributedCacheService(_distributedCacheMock.Object, loggerMock.Object);
    }

    // IMPORTANT: GetStringAsync/SetStringAsync/RemoveAsync (the string-based
    // helpers DistributedCacheService calls) are STATIC EXTENSION METHODS on
    // IDistributedCache - Moq cannot mock extension methods. The extensions
    // internally call the real interface members (byte[]-based GetAsync/
    // SetAsync/RemoveAsync), so those are what we mock here instead.

    private record TestPayload(string Name, int Value);

    #region GetAsync

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        string key = _fixture.Create<string>();
        var payload = _fixture.Create<TestPayload>();
        byte[] cachedBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        _distributedCacheMock
            .Setup(temp => temp.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        // Act
        TestPayload? result = await _cacheService.GetAsync<TestPayload>(key);

        // Assert
        result.Should().Be(payload);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ReturnsDefault()
    {
        // Arrange
        string key = _fixture.Create<string>();

        _distributedCacheMock
            .Setup(temp => temp.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);

        // Act
        TestPayload? result = await _cacheService.GetAsync<TestPayload>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_RedisThrows_ReturnsDefault_DoesNotPropagate()
    {
        // Arrange - Redis being briefly unavailable should degrade gracefully,
        // not take down whatever code called into the cache.
        string key = _fixture.Create<string>();

        _distributedCacheMock
            .Setup(temp => temp.GetAsync(key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection timed out"));

        // Act
        Func<Task> action = async () => await _cacheService.GetAsync<TestPayload>(key);

        // Assert
        await action.Should().NotThrowAsync();
        (await _cacheService.GetAsync<TestPayload>(key)).Should().BeNull();
    }

    #endregion

    #region SetAsync

    [Fact]
    public async Task SetAsync_StoresSerializedValue_WithExpirationOptions()
    {
        // Arrange
        string key = _fixture.Create<string>();
        var payload = _fixture.Create<TestPayload>();
        var absolute = TimeSpan.FromMinutes(10);
        var sliding = TimeSpan.FromMinutes(3);

        byte[]? capturedBytes = null;
        DistributedCacheEntryOptions? capturedOptions = null;

        _distributedCacheMock
            .Setup(temp => temp.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, bytes, options, _) =>
            {
                capturedBytes = bytes;
                capturedOptions = options;
            })
            .Returns(Task.CompletedTask);

        // Act
        await _cacheService.SetAsync(key, payload, absolute, sliding);

        // Assert
        Encoding.UTF8.GetString(capturedBytes!).Should().Be(JsonSerializer.Serialize(payload));
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(absolute);
        capturedOptions.SlidingExpiration.Should().Be(sliding);
    }

    [Fact]
    public async Task SetAsync_RedisThrows_DoesNotPropagate()
    {
        // Arrange
        string key = _fixture.Create<string>();

        _distributedCacheMock
            .Setup(temp => temp.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection timed out"));

        // Act
        Func<Task> action = async () => await _cacheService.SetAsync(key, _fixture.Create<TestPayload>());

        // Assert
        await action.Should().NotThrowAsync();
    }

    #endregion

    #region GetBulkAsync

    [Fact]
    public async Task GetBulkAsync_MixOfHitsAndMisses_ToBeSuccessful()
    {
        // Arrange
        string hitKey = "hit-key";
        string missKey = "miss-key";
        var payload = _fixture.Create<TestPayload>();

        _distributedCacheMock
            .Setup(temp => temp.GetAsync(hitKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));

        _distributedCacheMock
            .Setup(temp => temp.GetAsync(missKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);

        // Act
        var result = await _cacheService.GetBulkAsync<TestPayload>(new[] { hitKey, missKey });

        // Assert
        result[hitKey].Should().Be(payload);
        result[missKey].Should().BeNull();
    }

    #endregion

    #region RemoveAsync

    [Fact]
    public async Task RemoveAsync_CallsUnderlyingRemove()
    {
        // Arrange
        string key = _fixture.Create<string>();
        _distributedCacheMock
            .Setup(temp => temp.RemoveAsync(key, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert
        _distributedCacheMock.Verify(temp => temp.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RedisThrows_DoesNotPropagate()
    {
        // Arrange
        string key = _fixture.Create<string>();
        _distributedCacheMock
            .Setup(temp => temp.RemoveAsync(key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection timed out"));

        // Act
        Func<Task> action = async () => await _cacheService.RemoveAsync(key);

        // Assert
        await action.Should().NotThrowAsync();
    }

    #endregion
}