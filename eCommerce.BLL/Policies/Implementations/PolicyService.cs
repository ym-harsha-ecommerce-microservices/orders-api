using eCommerce.BLL.Policies.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using System.Text;

namespace eCommerce.BLL.Policies.Implementations;

public class PolicyService(ILogger<PolicyService> _logger) : IPolicyService
{
    private static bool IsTransientFailure(HttpResponseMessage response) =>
        !response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound;

    public IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(int handledEventsAllowedBeforeBreaking, TimeSpan durationOfBreak)
    {
        var policy = Policy.HandleResult<HttpResponseMessage>(IsTransientFailure)
            .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: handledEventsAllowedBeforeBreaking,
            durationOfBreak: durationOfBreak,
            onBreak: (outcome, timespan, context) =>
            {
                if (outcome.Result != null)
                {
                    _logger.LogWarning(
                        "[Circuit Breaker] OPEN for {BreakDuration}s. Reason: Status Code {StatusCode}",
                        timespan.TotalSeconds, outcome.Result.StatusCode);
                }
                else if (outcome.Exception != null)
                {
                    _logger.LogWarning(
                        outcome.Exception,
                        "[Circuit Breaker] OPEN for {BreakDuration}s. Reason: Exception {ExceptionMessage}",
                        timespan.TotalSeconds, outcome.Exception.Message);
                }
            },
            onReset: (context) =>
            {
                _logger.LogInformation("[Circuit Breaker] CLOSED. Service is operating normally.");
            },
            onHalfOpen: () =>
            {
                _logger.LogInformation("[Circuit Breaker] HALF-OPEN. Testing the connection...");
            });

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy(HttpStatusCode fallbackStatusCode, string fallbackContent)
    {
        var policy = Policy.HandleResult<HttpResponseMessage>(IsTransientFailure)
            .Or<BrokenCircuitException>()
            .Or<TimeoutRejectedException>()
            .Or<HttpRequestException>()
            .FallbackAsync(
                fallbackAction: (outcome, context, cancellationToken) =>
                {
                    var fallbackResponse = new HttpResponseMessage(fallbackStatusCode)
                    {
                        Content = new StringContent(fallbackContent, Encoding.UTF8, "application/json")
                    };

                    return Task.FromResult(fallbackResponse);
                },
                onFallbackAsync: (outcome, context) =>
                {
                    _logger.LogWarning(
                        "[Fallback] Triggered. Reason: {Reason}",
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                    return Task.CompletedTask;
                });

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount, double backoffBaseSeconds = 2)
    {
        var policy = Policy.HandleResult<HttpResponseMessage>(IsTransientFailure)
            .WaitAndRetryAsync(
                retryCount: retryCount,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(backoffBaseSeconds, retryAttempt)),
                onRetry: (outcome, timespan, retryNumber, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode;
                    _logger.LogWarning(
                        "[WaitAndRetry] Delaying for {Delay}s. Attempt {RetryNumber}. Status: {StatusCode}",
                        timespan.TotalSeconds, retryNumber, statusCode);
                });

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(TimeSpan timeout)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeout);
    }

    public IAsyncPolicy<HttpResponseMessage> GetBulkheadIsolationPolicy(int maxParallelization, int maxQueuingActions)
    {
        var policy = Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: maxParallelization,
            maxQueuingActions: maxQueuingActions,
            onBulkheadRejectedAsync: context =>
            {
                _logger.LogWarning(
                    "[Bulkhead] Rejected. Max parallelization ({MaxParallelization}) and queue ({MaxQueuingActions}) reached.",
                    maxParallelization, maxQueuingActions);
                return Task.CompletedTask;
            });

        return policy;
    }
}