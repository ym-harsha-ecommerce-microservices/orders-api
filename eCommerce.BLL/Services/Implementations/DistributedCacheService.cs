using eCommerce.BLL.Services.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace eCommerce.BLL.Services.Implementations;

public class DistributedCacheService(
    IDistributedCache _distributedCache,
    ILogger<DistributedCacheService> _logger) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var cachedJson = await _distributedCache.GetStringAsync(key);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                return JsonSerializer.Deserialize<T>(cachedJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Cache is unavailable while reading key: {CacheKey}", key);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T data, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        try
        {
            var options = CreateCacheOptions(absoluteExpiration, slidingExpiration);
            var json = JsonSerializer.Serialize(data);
            await _distributedCache.SetStringAsync(key, json, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save key {CacheKey} in Redis Cache.", key);
        }
    }

    public async Task<IDictionary<string, T?>> GetBulkAsync<T>(IEnumerable<string> keys)
    {
        var results = new Dictionary<string, T?>();

        var tasks = keys.Select(async key =>
        {
            var value = await GetAsync<T>(key);
            return new KeyValuePair<string, T?>(key, value);
        }).ToList();

        var cacheResults = await Task.WhenAll(tasks);

        foreach (var result in cacheResults)
        {
            results[result.Key] = result.Value;
        }

        return results;
    }

    public async Task SetBulkAsync<T>(IDictionary<string, T> items, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        var options = CreateCacheOptions(absoluteExpiration, slidingExpiration);

        var tasks = items.Select(async item =>
        {
            try
            {
                var json = JsonSerializer.Serialize(item.Value);
                await _distributedCache.SetStringAsync(item.Key, json, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save key {CacheKey} in Redis Cache.", item.Key);
            }
        });

        await Task.WhenAll(tasks);
    }
    private static DistributedCacheEntryOptions CreateCacheOptions(TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration)
    {
        var options = new DistributedCacheEntryOptions();

        if (absoluteExpiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration;

        if (slidingExpiration.HasValue)
            options.SlidingExpiration = slidingExpiration;

        return options;
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _distributedCache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove key {CacheKey} from Redis Cache.", key);
        }
    }
}