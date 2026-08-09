using eCommerce.BLL.DTO.ProductsMicroservice;

namespace eCommerce.BLL.HttpClients;

/// <summary>
/// Abstraction over <see cref="ProductsMicroserviceHttpClient"/> so consumers
/// (like OrderService) can be unit tested without a real HttpClient.
/// </summary>
public interface IProductsMicroserviceHttpClient
{
    Task<ProductDTO?> GetProductByProductIDAsync(Guid productID);
    Task<List<ProductDTO>> GetProductsByIdsAsync(List<Guid> productIds);
}