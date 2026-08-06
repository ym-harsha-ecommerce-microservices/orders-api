namespace eCommerce.BLL.RabbitMQ;


public class RabbitMQOptions
{
    public string RABBITMQ_HOST { get; set; } = string.Empty;
    public string RABBITMQ_PORT { get; set; } = string.Empty;
    public string RABBITMQ_USERNAME { get; set; } = string.Empty;
    public string RABBITMQ_PASSWORD { get; set; } = string.Empty;
    public string RABBITMQ_PRODUCT_EXCHANGE { get; set; } = string.Empty;
    public string RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY { get; set; } = string.Empty;
    public string RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY { get; set; } = string.Empty;
    public string RABBITMQ_PRODUCT_DELETE_QUEUE { get; set; } = string.Empty;
    public string RABBITMQ_PRODUCT_UPDATE_QUEUE { get; set; } = string.Empty;
}
