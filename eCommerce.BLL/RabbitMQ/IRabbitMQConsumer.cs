using System.Threading;
using System.Threading.Tasks;

namespace eCommerce.BLL.RabbitMQ;

public interface IRabbitMQConsumer
{
    Task ConsumeAsync<T>(string queueName, string routingKey, Func<T, Task> onMessageReceived) where T : class;
    void Dispose();
}