using Polly;
using System.Net;

namespace eCommerce.BLL.Policies.Interfaces;

public interface IPolicyService
{
    IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(int handledEventsAllowedBeforeBreaking, TimeSpan durationOfBreak);

    IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy(HttpStatusCode fallbackStatusCode, string fallbackContent);

    IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount, double backoffBaseSeconds = 2);

    IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(TimeSpan timeout);
    IAsyncPolicy<HttpResponseMessage> GetBulkheadIsolationPolicy(int maxParallelization, int maxQueuingActions);
}