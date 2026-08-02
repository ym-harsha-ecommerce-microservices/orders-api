using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.BLL.DTO.ProductsMicroservice;

public class ProductDTO
{
    public Guid ProductID { get; set; }
    public string? ProductName { get; set; }
    public string? Category { get; set; }
    public double? UnitPrice { get; set; }
    public int? QuantityInStock { get; set; }
}
