using System.Threading;
using System.Threading.Tasks;

namespace eCommerce.BLL.RabbitMQ;

public interface IRabbitMQPublisher
{
    /// <summary>
    /// Publishes a message asynchronously to a specific RabbitMQ exchange.
    /// </summary>
    /// <param name="message">The generic message payload.</param>
    /// <param name="routingKey">The routing key / queue name.</param>
    Task PublishAsync<T>(
        T message,
        string routingKey) where T : class;
}