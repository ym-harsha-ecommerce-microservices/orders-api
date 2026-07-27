using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.DAL.Entities;

public class Order
{
    [BsonId]
    public Guid OrderID { get; set; }
    public Guid UserID { get; set; }
    public DateTime? OrderDate { get; set; }
    public decimal? TotalBill { get; set; }
    public List<OrderItem>? OrderItems { get; set; } = default;
}
