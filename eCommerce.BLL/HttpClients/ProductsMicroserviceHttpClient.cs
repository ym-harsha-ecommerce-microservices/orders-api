using eCommerce.BLL.DTO.ProductsMicroservice;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace eCommerce.BLL.HttpClients;

public class ProductsMicroserviceHttpClient(
    HttpClient _httpClient,
    IDistributedCache _distributedCache,
    ILogger<ProductsMicroserviceHttpClient> _logger)
{
    public async Task<ProductDTO?> GetProductByProductIDAsync(Guid productID)
    {
        var cacheKey = $"product_details_{productID}";

        try
        {
            var productJson = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrWhiteSpace(productJson))
            {
                return JsonSerializer.Deserialize<ProductDTO?>(productJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Cache is unavailable while reading product {ProductID}. Bypassing cache and falling back to Products API.", productID);
        }

        var response = await _httpClient.GetAsync($"api/products/search/product-id/{productID}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Product {ProductID} not found in Products API (404).", productID);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Products API is unavailable (503). Returning dummy fallback data for product {ProductID}.", productID);
            return GetDummyProduct(productID);
        }

        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<ProductDTO>();

        if (product != null)
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                };

                await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save product {ProductID} in Redis Cache.", productID);
            }
        }
        else
        {
            _logger.LogWarning("Received successful response from Products API for {ProductID}, but the parsed content was null.", productID);
        }

        return product;
    }

    private ProductDTO GetDummyProduct(Guid productID)
    {
        return new ProductDTO
        {
            ProductID = productID,
            Category = "Temporarily Unavailable",
            ProductName = "Temporarily Unavailable",
            QuantityInStock = 0,
            UnitPrice = 0,
        };
    }

    public async Task<List<ProductDTO>> GetProductsByIdsAsync(List<Guid> productIds)
    {
        if (productIds == null || !productIds.Any())
        {
            return [];
        }

        var orderedIds = productIds.OrderBy(id => id).ToList();
        var cacheKey = $"product_details_{JsonSerializer.Serialize(orderedIds)}";

        try
        {
            var productsJson = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrWhiteSpace(productsJson))
            {
                return JsonSerializer.Deserialize<List<ProductDTO>>(productsJson) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Cache is unavailable while fetching product list. Bypassing cache and falling back to Products API.");
        }

        var response = await _httpClient.PostAsJsonAsync("api/products/search/product-ids", productIds);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Products API is unavailable (503) while fetching product list. Returning an empty list as a fallback.");
            return [];
        }

        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<List<ProductDTO>>() ?? [];

        if (products.Any())
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                };

                await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(products), cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save the list of products in Redis Cache.");
            }
        }

        return products;
    }
}