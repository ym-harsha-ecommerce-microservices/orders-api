using eCommerce.BLL.DTO.OrderItem;

namespace eCommerce.BLL.DTO.Order;

public class OrderAddRequest
{
    public Guid UserID { get; set; }
    public DateTime OrderDate { get; set; }
    public List<OrderItemAddRequest> OrderItems { get; set; } = default!;
}
