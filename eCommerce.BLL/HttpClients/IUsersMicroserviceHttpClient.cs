using eCommerce.BLL.DTO.UsersMicroservice;

namespace eCommerce.BLL.HttpClients;

/// <summary>
/// Abstraction over <see cref="UsersMicroserviceHttpClient"/> so consumers
/// (like OrderService) can be unit tested without a real HttpClient.
/// </summary>
public interface IUsersMicroserviceHttpClient
{
    Task<UserDTO?> GetUserByUserIDAsync(Guid userID);
    Task<List<UserDTO>> GetUsersByIdsAsync(List<Guid> allUserIds);
}