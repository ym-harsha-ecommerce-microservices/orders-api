using eCommerce.BLL.Policies.Implementations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using Xunit;
using System.Threading;

namespace eCommerce.Tests.ServicesTests;

// NOTE ON APPROACH: PolicyService itself contains no branching logic worth
// unit testing in isolation - it just configures Polly builders. What
// actually matters is whether the POLICY IT PRODUCES behaves correctly under
// failure. So these tests build a real policy from PolicyService and execute
// it against a controllable fake delegate, rather than mocking Polly (which
// would just prove the mocks do what we told them to do).
public class PolicyServiceTest
{
    private readonly PolicyService _policyService;

    public PolicyServiceTest()
    {
        var loggerMock = new Mock<ILogger<PolicyService>>();
        _policyService = new PolicyService(loggerMock.Object);
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK);
    private static HttpResponseMessage Failure() => new(HttpStatusCode.InternalServerError);

    #region GetRetryPolicy

    [Fact]
    public async Task GetRetryPolicy_FailsThenSucceeds_RetriesUntilSuccess()
    {
        // Arrange - tiny backoff base so the exponential wait is effectively
        // instant and the test doesn't sit around sleeping for real seconds.
        var policy = _policyService.GetRetryPolicy(retryCount: 3, backoffBaseSeconds: 0.001);

        int attempts = 0;

        // Act - fails the first 2 attempts, succeeds on the 3rd.
        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(attempts < 3 ? Failure() : Success());
        });

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task GetRetryPolicy_AlwaysFails_GivesUpAfterRetryCountExhausted()
    {
        // Arrange
        var policy = _policyService.GetRetryPolicy(retryCount: 2, backoffBaseSeconds: 0.001);
        int attempts = 0;

        // Act - 1 initial attempt + 2 retries = 3 total attempts, all failing.
        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(Failure());
        });

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task GetRetryPolicy_404_IsNotTreatedAsTransient_DoesNotRetry()
    {
        // Arrange - IsTransientFailure() in PolicyService explicitly excludes
        // 404: a missing resource won't exist just because you asked again.
        var policy = _policyService.GetRetryPolicy(retryCount: 3, backoffBaseSeconds: 0.001);
        int attempts = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        // Assert
        attempts.Should().Be(1);
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetCircuitBreakerPolicy

    [Fact]
    public async Task GetCircuitBreakerPolicy_OpensAfterThreshold_ThenRejectsFastWithoutCallingDelegate()
    {
        // Arrange
        var policy = _policyService.GetCircuitBreakerPolicy(
            handledEventsAllowedBeforeBreaking: 2,
            durationOfBreak: TimeSpan.FromSeconds(30));

        int attempts = 0;
        Task<HttpResponseMessage> FailingCall()
        {
            attempts++;
            return Task.FromResult(Failure());
        }

        // Act - the 2 failures that reach the threshold still execute normally...
        await policy.ExecuteAsync(FailingCall);
        await policy.ExecuteAsync(FailingCall);

        // ...but the 3rd call should be short-circuited by the now-open breaker.
        Func<Task> thirdCall = async () => await policy.ExecuteAsync(FailingCall);

        // Assert
        await thirdCall.Should().ThrowAsync<BrokenCircuitException>();
        attempts.Should().Be(2); // the delegate itself was never invoked a 3rd time
    }

    #endregion

    #region GetFallbackPolicy

    [Fact]
    public async Task GetFallbackPolicy_UnderlyingCallThrows_ReturnsFallbackResponse()
    {
        // Arrange
        var policy = _policyService.GetFallbackPolicy(HttpStatusCode.ServiceUnavailable, "[]");

        // Act
        var result = await policy.ExecuteAsync(() => throw new HttpRequestException("downstream is down"));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await result.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task GetFallbackPolicy_UnderlyingCallFailsResult_ReturnsFallbackResponse()
    {
        // Arrange
        var policy = _policyService.GetFallbackPolicy(HttpStatusCode.ServiceUnavailable, "[]");

        // Act - a "failing" result (per IsTransientFailure), not an exception.
        var result = await policy.ExecuteAsync(() => Task.FromResult(Failure()));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetFallbackPolicy_SuccessfulCall_PassesThroughUnchanged()
    {
        // Arrange
        var policy = _policyService.GetFallbackPolicy(HttpStatusCode.ServiceUnavailable, "[]");

        // Act
        var result = await policy.ExecuteAsync(() => Task.FromResult(Success()));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetTimeoutPolicy

    [Fact]
    public async Task GetTimeoutPolicy_SlowCall_ThrowsTimeoutRejectedException()
    {
        // Arrange
        var policy = _policyService.GetTimeoutPolicy(TimeSpan.FromMilliseconds(50));

        // Act
        Func<Task> action = async () => await policy.ExecuteAsync(
            async (ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return Success();
            },
            CancellationToken.None 
        );

        // Assert
        await action.Should().ThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public async Task GetTimeoutPolicy_FastCall_CompletesNormally()
    {
        // Arrange
        var policy = _policyService.GetTimeoutPolicy(TimeSpan.FromSeconds(5));

        // Act
        var result = await policy.ExecuteAsync(() => Task.FromResult(Success()));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetBulkheadIsolationPolicy

    [Fact]
    public async Task GetBulkheadIsolationPolicy_ExceedsCapacity_RejectsExcessCalls()
    {
        // Arrange - only 1 concurrent call allowed, no queue at all, so a 2nd
        // concurrent call must be rejected immediately.
        var policy = _policyService.GetBulkheadIsolationPolicy(maxParallelization: 1, maxQueuingActions: 0);

        var firstCallStarted = new TaskCompletionSource();
        var releaseFirstCall = new TaskCompletionSource();

        // Act - occupy the only slot and hold it open.
        var firstCallTask = policy.ExecuteAsync(async () =>
        {
            firstCallStarted.SetResult();
            await releaseFirstCall.Task;
            return Success();
        });

        await firstCallStarted.Task;

        // The slot is occupied and there's no queue, so this should be rejected.
        Func<Task> secondCall = async () => await policy.ExecuteAsync(() => Task.FromResult(Success()));

        // Assert
        await secondCall.Should().ThrowAsync<Polly.Bulkhead.BulkheadRejectedException>();

        // Cleanup - release the first call so its Task completes.
        releaseFirstCall.SetResult();
        await firstCallTask;
    }

    #endregion
}