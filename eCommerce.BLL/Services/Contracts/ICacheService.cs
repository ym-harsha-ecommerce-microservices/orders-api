using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.BLL.Services.Contracts;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T data, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

    Task<IDictionary<string, T?>> GetBulkAsync<T>(IEnumerable<string> keys);
    Task SetBulkAsync<T>(IDictionary<string, T> items, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

    Task RemoveAsync(string key);
}