namespace eCommerce.BLL.DTO.OrderItem;

public class OrderItemAddRequest
{
    public Guid ProductID { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? Quantity { get; set; }
}
