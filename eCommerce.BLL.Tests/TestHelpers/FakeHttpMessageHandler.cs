using System.Net;

namespace eCommerce.Tests.TestHelpers;

/// <summary>
/// A DelegatingHandler stand-in for the network. Instead of mocking HttpClient
/// itself (its send method isn't mockable), we swap out the HANDLER underneath
/// it - HttpClient will call SendAsync on this fake instead of hitting the wire.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Always returns the same fixed response, regardless of the request.</summary>
    public static FakeHttpMessageHandler ReturningStatus(HttpStatusCode statusCode, string content = "")
    {
        return new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }

    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler(responder);
        return new HttpClient(handler) { BaseAddress = new Uri("https://fake-gateway.local/") };
    }
}