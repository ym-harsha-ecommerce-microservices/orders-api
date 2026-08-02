using eCommerce.BLL.Policies.Interfaces;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Bulkhead;
using System.Net;

namespace eCommerce.BLL.Policies.Implementations;

public class UsersMicroservicePolicies(IPolicyService _policyService, IConfiguration _configuration)
    : IUsersMicroservicePolicies
{
    public IAsyncPolicy<HttpResponseMessage> GetUsersPolicies()
    {
        var retryCount = _configuration.GetValue<int>("UsersMicroservice:RetryCount", 4);
        var retryBackoffBase = _configuration.GetValue<double>("UsersMicroservice:RetryBackoffBase", 2);
        var retryPolicy = _policyService.GetRetryPolicy(retryCount, retryBackoffBase);


        var breakingThreshold = _configuration.GetValue<int>("UsersMicroservice:BreakingThreshold", 3);
        var breakDuration = _configuration.GetValue<int>("UsersMicroservice:BreakDuration", 60);
        var circuitPolicy = _policyService.GetCircuitBreakerPolicy(breakingThreshold, TimeSpan.FromSeconds(breakDuration));

        var timeout = _configuration.GetValue<int>("UsersMicroservice:Timeout", 100);
        var timeoutPolicy = _policyService.GetTimeoutPolicy(TimeSpan.FromSeconds(timeout));

        var fallbackPolicy = _policyService.GetFallbackPolicy(HttpStatusCode.ServiceUnavailable, string.Empty);

        return Policy.WrapAsync(fallbackPolicy, retryPolicy, circuitPolicy, timeoutPolicy);
    }
}