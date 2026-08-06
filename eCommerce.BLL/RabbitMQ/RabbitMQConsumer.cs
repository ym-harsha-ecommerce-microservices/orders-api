using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace eCommerce.BLL.RabbitMQ;

public class RabbitMQConsumer : IRabbitMQConsumer, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _productExchangeName;

    // Inject IOptions just like we did for the Publisher
    public RabbitMQConsumer(IOptions<RabbitMQOptions> rabbitMQOptions)
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

    public async Task ConsumeAsync<T>(string queueName, string routingKey, Func<T, Task> onMessageReceived) where T : class
    {
        // 1. Ensure the exchange exists
        await _channel.ExchangeDeclareAsync(exchange: _productExchangeName, type: ExchangeType.Direct, durable: true);

        // 2. Declare the queue using the parameter passed in (queueName)
        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);

        // 3. Bind the queue to the exchange using the routing key
        await _channel.QueueBindAsync(queue: queueName, exchange: _productExchangeName, routingKey: routingKey);

        // 4. Set up the Async Consumer
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                // Extract the message body
                var body = ea.Body.ToArray();
                var messageString = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<T>(messageString);

                if (message != null)
                {
                    // Pass the message to the handler function provided by the caller
                    await onMessageReceived(message);
                }

                // 5. Acknowledge the message (tells RabbitMQ it was successfully processed)
                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        // 6. Start consuming from the correct queue
        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}