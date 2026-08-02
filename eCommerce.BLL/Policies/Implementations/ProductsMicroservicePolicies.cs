using eCommerce.BLL.Policies.Interfaces;
using Microsoft.Extensions.Configuration;
using Polly;
using System.Net;

namespace eCommerce.BLL.Policies.Implementations;

public class ProductsMicroservicePolicies(IPolicyService _policyService, IConfiguration _configuration)
    : IProductsMicroservicePolicies
{
    public IAsyncPolicy<HttpResponseMessage> GetProductsPolicies()
    {
        var retryCount = _configuration.GetValue<int>("ProductsMicroservice:RetryCount", 4);
        var retryBackoffBase = _configuration.GetValue<double>("ProductsMicroservice:RetryBackoffBase", 2);
        var retryPolicy = _policyService.GetRetryPolicy(retryCount, retryBackoffBase);

        var breakingThreshold = _configuration.GetValue<int>("ProductsMicroservice:BreakingThreshold", 3);
        var breakDuration = _configuration.GetValue<int>("ProductsMicroservice:BreakDuration", 60);
        var circuitPolicy = _policyService.GetCircuitBreakerPolicy(breakingThreshold, TimeSpan.FromSeconds(breakDuration));

        var timeout = _configuration.GetValue<int>("ProductsMicroservice:Timeout", 100);
        var timeoutPolicy = _policyService.GetTimeoutPolicy(TimeSpan.FromSeconds(timeout));

        var maxParallelization = _configuration.GetValue<int>("ProductsMicroservice:MaxParallelization", 10);
        var maxQueuingActions = _configuration.GetValue<int>("ProductsMicroservice:MaxQueuingActions", 20);
        var bulkheadPolicy = _policyService.GetBulkheadIsolationPolicy(maxParallelization, maxQueuingActions);

        var fallbackPolicy = _policyService.GetFallbackPolicy(HttpStatusCode.ServiceUnavailable, "[]");

        return Policy.WrapAsync(fallbackPolicy, retryPolicy, circuitPolicy, bulkheadPolicy, timeoutPolicy);
    }
}