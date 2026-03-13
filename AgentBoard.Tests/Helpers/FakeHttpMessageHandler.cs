using System.Net;

namespace AgentBoard.Tests.Helpers;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> that returns preset responses for testing.
/// Records all requests sent through it.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _requests = new();

    /// <summary>All requests that were sent through this handler.</summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    /// <summary>Enqueues a response that will be returned for the next request.</summary>
    public void EnqueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

    /// <summary>Enqueues a JSON response with status 200.</summary>
    public void EnqueueJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        EnqueueResponse(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (_responses.TryDequeue(out var response))
            return Task.FromResult(response);

        // Default: 200 OK with empty body
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
    }
}

/// <summary>
/// An <see cref="IHttpClientFactory"/> that always returns an <see cref="HttpClient"/>
/// backed by a given <see cref="FakeHttpMessageHandler"/>.
/// </summary>
public class FakeHttpClientFactory(FakeHttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler);
}
