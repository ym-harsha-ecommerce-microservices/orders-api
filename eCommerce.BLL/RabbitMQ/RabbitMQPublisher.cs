using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace eCommerce.BLL.RabbitMQ;

public class RabbitMQPublisher : IRabbitMQPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _productExchangeName;

    // Injected IOptions to access the strongly-typed configuration
    public RabbitMQPublisher(IOptions<RabbitMQOptions> rabbitMQOptions)
    {
        var options = rabbitMQOptions.Value;

        _productExchangeName = options.RABBITMQ_PRODUCT_EXCHANGE;

        var connectionFactory = new ConnectionFactory()
        {
            HostName = options.RABBITMQ_HOST,
            Password = options.RABBITMQ_PASSWORD,
            Port = Convert.ToInt32(options.RABBITMQ_PORT),
            UserName = options.RABBITMQ_USERNAME
        };

        _connection = connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T message, string routingKey) where T : class
    {
        var messageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var exchangeName = _productExchangeName;

        // Ensure the exchange exists before publishing
        await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: true);

        // Publish the message using the simpler 3-argument overload
        await _channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: messageBytes);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}