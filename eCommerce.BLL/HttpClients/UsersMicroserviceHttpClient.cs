using eCommerce.BLL.DTO.UsersMicroservice;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace eCommerce.BLL.HttpClients;

public class UsersMicroserviceHttpClient(
    HttpClient _httpClient,
    IDistributedCache _distributedCache,
    ILogger<UsersMicroserviceHttpClient> _logger)
{
    public async Task<UserDTO?> GetUserByUserIDAsync(Guid userID)
    {
        string cacheKey = $"user_details_{userID}";

        try
        {
            string? cachedUserJson = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrWhiteSpace(cachedUserJson))
            {
                return JsonSerializer.Deserialize<UserDTO?>(cachedUserJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Cache is unavailable while reading user {UserID}. Bypassing cache and falling back to Users API.", userID);
        }

        var response = await _httpClient.GetAsync($"api/users/{userID}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("User {UserID} not found in Users API (404).", userID);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Users API is unavailable (503). Returning dummy fallback data for user {UserID}.", userID);
            return GetDummyUser(userID);
        }

        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserDTO>();

        if (user != null)
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                };

                var userJson = JsonSerializer.Serialize(user);
                await _distributedCache.SetStringAsync(cacheKey, userJson, cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save user {UserID} in Redis Cache.", userID);
            }
        }
        else
        {
            _logger.LogWarning("Received successful response from Users API for {UserID}, but the parsed content was null.", userID);
        }

        return user;
    }

    public async Task<List<UserDTO>> GetUsersByIdsAsync(List<Guid> allUserIds)
    {
        if (allUserIds == null || !allUserIds.Any())
        {
            return [];
        }

        var orderedIds = allUserIds.OrderBy(id => id).ToList();
        string cacheKey = $"users_details_{JsonSerializer.Serialize(orderedIds)}";

        try
        {
            string? cachedUsersJson = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrWhiteSpace(cachedUsersJson))
            {
                return JsonSerializer.Deserialize<List<UserDTO>>(cachedUsersJson) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Cache is unavailable while fetching user list. Bypassing cache and falling back to Users API.");
        }

        var response = await _httpClient.PostAsJsonAsync("api/users/search/user-ids", allUserIds);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Users API is unavailable (503) while fetching user list. Returning an empty list as a fallback.");
            return [];
        }

        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<UserDTO>>() ?? [];

        if (users.Any())
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                };

                await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(users), cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save the list of users in Redis Cache.");
            }
        }

        return users;
    }

    private static UserDTO GetDummyUser(Guid userID)
    {
        return new UserDTO
        {
            UserID = userID,
            PersonName = "Temporarily Unavailable",
            Email = "temporarily.unavailable@placeholder.com",
            Gender = "Temporarily Unavailable"
        };
    }
}