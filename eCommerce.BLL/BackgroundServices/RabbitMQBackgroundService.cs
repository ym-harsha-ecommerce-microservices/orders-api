using eCommerce.BLL.Constants;
using eCommerce.BLL.DTO.ProductMessages;
using eCommerce.BLL.DTOs.RabbitMQMessages.ProductMessages;
using eCommerce.BLL.HttpClients;
using eCommerce.BLL.RabbitMQ;
using eCommerce.BLL.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eCommerce.BLL.BackgroundServices;

public class RabbitMQBackgroundService(
    IRabbitMQConsumer _rabbitMQConsumer,
    ILogger<RabbitMQBackgroundService> _logger,
    IOptions<RabbitMQOptions> _rabbitMQOptions,
    IServiceScopeFactory _scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RabbitMQ Background Service is starting.");

        // Keep trying as long as the application is running
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Start both consumers concurrently
                var updateTask = ConsumeProductUpdateNameEventAsync();
                var deleteTask = ConsumeProductDeleteEventAsync();

                // Wait for both to finish registering
                await Task.WhenAll(updateTask, deleteTask);

                // CRITICAL: If you don't see this log, your RabbitMQConsumer class has an infinite block!
                _logger.LogInformation("Successfully connected to RabbitMQ and registered all consumers!");

                // Pause here infinitely while listening for messages
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break; // Expected when shutting down
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ is not ready or an error occurred. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _rabbitMQConsumer.Dispose();
        _logger.LogInformation("RabbitMQ Background Service is stopping.");
    }

    private async Task ConsumeProductUpdateNameEventAsync()
    {
        var routingKey = _rabbitMQOptions.Value.RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY;
        var queueName = _rabbitMQOptions.Value.RABBITMQ_PRODUCT_UPDATE_QUEUE;

        await _rabbitMQConsumer.ConsumeAsync<ProductNameUpdateMessage>(
            queueName: queueName,
            routingKey: routingKey,
            onMessageReceived: async (message) =>
            {
                _logger.LogInformation($"Product name changed to '{message.ProductNewName}' for Product ID: {message.ProductId}");

                using var scope = _scopeFactory.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                var productsClient = scope.ServiceProvider.GetRequiredService<ProductsMicroserviceHttpClient>();

                string cacheKey = CacheKeys.ProductDetails(message.ProductId);
                await cacheService.RemoveAsync(cacheKey);

                await productsClient.GetProductByProductIDAsync(message.ProductId);
            });
    }

    private async Task ConsumeProductDeleteEventAsync()
    {
        var routingKey = _rabbitMQOptions.Value.RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY;
        var queueName = _rabbitMQOptions.Value.RABBITMQ_PRODUCT_DELETE_QUEUE;

        await _rabbitMQConsumer.ConsumeAsync<ProductDeleteMessage>(
            queueName: queueName,
            routingKey: routingKey,
            onMessageReceived: async (message) =>
            {
                _logger.LogInformation($"Product deletion requested for Product ID: {message.ProductId}, Name: '{message.ProductName}'");

                using var scope = _scopeFactory.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                string cacheKey = CacheKeys.ProductDetails(message.ProductId);
                await cacheService.RemoveAsync(cacheKey);
            });
    }
}