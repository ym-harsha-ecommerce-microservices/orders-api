using eCommerce.BLL.Constants;
using eCommerce.BLL.DTO.ProductsMicroservice;
using eCommerce.BLL.Services.Contracts;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace eCommerce.BLL.HttpClients;

public class ProductsMicroserviceHttpClient(
    HttpClient _httpClient,
    ICacheService _cacheService,
    ILogger<ProductsMicroserviceHttpClient> _logger)
{
    public async Task<ProductDTO?> GetProductByProductIDAsync(Guid productID)
    {
        string cacheKey = CacheKeys.ProductDetails(productID);

        var cachedProduct = await _cacheService.GetAsync<ProductDTO>(cacheKey);
        if (cachedProduct != null) return cachedProduct;

        var response = await _httpClient.GetAsync($"gateway/products/search/product-id/{productID}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Product {ProductID} not found in Products API (404).", productID);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Products API is unavailable (503). Returning dummy fallback.", productID);
            return GetDummyProduct(productID);
        }

        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductDTO>();

        if (product != null)
        {
            await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3));
        }

        return product;
    }

    public async Task<List<ProductDTO>> GetProductsByIdsAsync(List<Guid> productIds)
    {
        if (productIds == null || !productIds.Any()) return [];

        var distinctIds = productIds.Distinct().ToList();

        var cacheKeys = distinctIds.ToDictionary(id => id, id => CacheKeys.ProductDetails(id));

        var cachedResults = await _cacheService.GetBulkAsync<ProductDTO>(cacheKeys.Values);

        var cachedProducts = new List<ProductDTO>();
        var missingIds = new List<Guid>();

        foreach (var id in distinctIds)
        {
            var key = cacheKeys[id];
            if (cachedResults.TryGetValue(key, out var product) && product != null)
            {
                cachedProducts.Add(product);
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (!missingIds.Any()) return cachedProducts;

        var response = await _httpClient.PostAsJsonAsync("gateway/products/search/product-ids", missingIds);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Products API is unavailable (503) while fetching missing products.");
            return cachedProducts;
        }

        response.EnsureSuccessStatusCode();
        var fetchedProducts = await response.Content.ReadFromJsonAsync<List<ProductDTO>>() ?? [];

        if (fetchedProducts.Any())
        {
            var itemsToCache = fetchedProducts.ToDictionary(
                p => CacheKeys.ProductDetails(p.ProductID),
                p => p
            );

            await _cacheService.SetBulkAsync(itemsToCache, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3));
            cachedProducts.AddRange(fetchedProducts);
        }

        return cachedProducts;
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
}