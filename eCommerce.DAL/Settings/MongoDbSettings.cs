using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.DAL.Settings;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string OrdersCollectionName { get; set; } = null!;
}
