using eCommerce.BLL.DTO.OrderItem;

namespace eCommerce.BLL.DTO.Order;

public class OrderUpdateRequest
{
    public Guid OrderID { get; set; }
    public Guid UserID { get; set; }
    public DateTime OrderDate { get; set; }
    public List<OrderItemUpdateRequest> OrderItems { get; set; } = default!;
}
