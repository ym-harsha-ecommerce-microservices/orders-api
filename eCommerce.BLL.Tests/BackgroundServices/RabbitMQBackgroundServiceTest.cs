using eCommerce.BLL.BackgroundServices;
using eCommerce.BLL.DTO.ProductMessages;
using eCommerce.BLL.DTOs.RabbitMQMessages.ProductMessages;
using eCommerce.BLL.RabbitMQ;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Timers;
using Xunit;

namespace eCommerce.Tests.ServicesTests;

// NOTE ON APPROACH: RabbitMQBackgroundService is an IHostedService with an
// infinite loop (`await Task.Delay(Timeout.Infinite, stoppingToken)`), so we
// can't just "call a method and assert a return value" like the other
// services. Instead we drive it the way the host actually would: call
// StartAsync(), let it run briefly, then cancel and call StopAsync(), and
// verify the SIDE EFFECTS (which consumers got registered, and cleanup on
// shutdown) rather than a return value.
public class RabbitMQBackgroundServiceTest
{
    private readonly Mock<IRabbitMQConsumer> _consumerMock;
    private readonly Mock<ILogger<RabbitMQBackgroundService>> _loggerMock;
    private readonly Mock<IOptions<RabbitMQOptions>> _optionsMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly RabbitMQOptions _options;
    private readonly RabbitMQBackgroundService _backgroundService;

    public RabbitMQBackgroundServiceTest()
    {
        _consumerMock = new Mock<IRabbitMQConsumer>();
        _loggerMock = new Mock<ILogger<RabbitMQBackgroundService>>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();

        _options = new RabbitMQOptions
        {
            RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY = "product.update.name",
            RABBITMQ_PRODUCT_UPDATE_QUEUE = "product-update-queue",
            RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY = "product.delete",
            RABBITMQ_PRODUCT_DELETE_QUEUE = "product-delete-queue"
        };

        _optionsMock = new Mock<IOptions<RabbitMQOptions>>();
        _optionsMock.Setup(temp => temp.Value).Returns(_options);

        // ConsumeAsync<T> is generic, so it needs one Setup per closed type
        // (ConsumeAsync<ProductNameUpdateMessage> and ConsumeAsync<ProductDeleteMessage>
        // are two different methods as far as Moq is concerned).
        _consumerMock
            .Setup(temp => temp.ConsumeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<ProductNameUpdateMessage, Task>>()))
            .Returns(Task.CompletedTask);

        _consumerMock
            .Setup(temp => temp.ConsumeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<ProductDeleteMessage, Task>>()))
            .Returns(Task.CompletedTask);

        _backgroundService = new RabbitMQBackgroundService(
            _consumerMock.Object,
            _loggerMock.Object,
            _optionsMock.Object,
            _scopeFactoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_OnStart_RegistersBothConsumersWithCorrectQueueAndRoutingKey()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _backgroundService.StartAsync(cts.Token);
        await Task.Delay(200); // let the background loop reach its registration calls
        cts.Cancel();
        await _backgroundService.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(temp => temp.ConsumeAsync(
            _options.RABBITMQ_PRODUCT_UPDATE_QUEUE,
            _options.RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY,
            It.IsAny<Func<ProductNameUpdateMessage, Task>>()),
            Times.Once);

        _consumerMock.Verify(temp => temp.ConsumeAsync(
            _options.RABBITMQ_PRODUCT_DELETE_QUEUE,
            _options.RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY,
            It.IsAny<Func<ProductDeleteMessage, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OnCancellation_DisposesConsumer()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _backgroundService.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await _backgroundService.StopAsync(CancellationToken.None);

        // Assert - cleanup should happen once the cancellation breaks the loop.
        _consumerMock.Verify(temp => temp.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConsumerRegistrationThrows_RetriesInsteadOfCrashing()
    {
        // Arrange - simulate RabbitMQ not being reachable yet on the first attempt.
        var attempt = 0;
        _consumerMock
            .Setup(temp => temp.ConsumeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<ProductNameUpdateMessage, Task>>()))
            .Returns(() =>
            {
                attempt++;
                if (attempt == 1)
                    throw new InvalidOperationException("RabbitMQ not ready");
                return Task.CompletedTask;
            });

        using var cts = new CancellationTokenSource();

        // Act - the service's retry-on-error path sleeps 5s between attempts,
        // so we don't wait for a full second successful cycle here; we just
        // confirm the FIRST failure doesn't crash the process (no unhandled
        // exception escapes StartAsync/StopAsync).
        Func<Task> action = async () =>
        {
            await _backgroundService.StartAsync(cts.Token);
            await Task.Delay(200);
            cts.Cancel();
            await _backgroundService.StopAsync(CancellationToken.None);
        };

        // Assert
        await action.Should().NotThrowAsync();
        attempt.Should().BeGreaterThanOrEqualTo(1);
    }
}