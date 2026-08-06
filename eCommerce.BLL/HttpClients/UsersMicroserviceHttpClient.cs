using eCommerce.BLL.Constants;
using eCommerce.BLL.DTO.UsersMicroservice;
using eCommerce.BLL.Services.Contracts;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace eCommerce.BLL.HttpClients;

public class UsersMicroserviceHttpClient(
    HttpClient _httpClient,
    ICacheService _cacheService,
    ILogger<UsersMicroserviceHttpClient> _logger)
{
    public async Task<UserDTO?> GetUserByUserIDAsync(Guid userID)
    {
        string cacheKey = CacheKeys.UserDetails(userID);

        var cachedUser = await _cacheService.GetAsync<UserDTO>(cacheKey);
        if (cachedUser != null) return cachedUser;

        var response = await _httpClient.GetAsync($"gateway/users/{userID}");

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
            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3));
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

        var distinctIds = allUserIds.Distinct().ToList();

        var cacheKeys = distinctIds.ToDictionary(id => id, id => CacheKeys.UserDetails(id));

        var cachedResults = await _cacheService.GetBulkAsync<UserDTO>(cacheKeys.Values);

        var cachedUsers = new List<UserDTO>();
        var missingIds = new List<Guid>();

        foreach (var id in distinctIds)
        {
            var key = cacheKeys[id];
            if (cachedResults.TryGetValue(key, out var user) && user != null)
            {
                cachedUsers.Add(user);
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (!missingIds.Any()) return cachedUsers;

        var response = await _httpClient.PostAsJsonAsync("gateway/users/search/user-ids", missingIds);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Users API is unavailable (503) while fetching missing users. Returning partial cached results.");
            return cachedUsers;
        }

        response.EnsureSuccessStatusCode();
        var fetchedUsers = await response.Content.ReadFromJsonAsync<List<UserDTO>>() ?? [];

        if (fetchedUsers.Any())
        {
            var itemsToCache = fetchedUsers.ToDictionary(
                u => CacheKeys.UserDetails(u.UserID),
                u => u
            );

            await _cacheService.SetBulkAsync(itemsToCache, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3));

            cachedUsers.AddRange(fetchedUsers);
        }

        return cachedUsers;
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