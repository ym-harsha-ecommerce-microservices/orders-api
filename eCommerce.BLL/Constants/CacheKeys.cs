using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.BLL.Constants;

public static class CacheKeys
{
    public static string ProductDetails(Guid productId) => $"product_details_{productId}";

    public static string UserDetails(Guid userId) => $"user_details_{userId}";
}