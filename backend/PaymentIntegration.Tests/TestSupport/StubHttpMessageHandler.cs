using System.Net;
using System.Text;

namespace PaymentIntegration.Tests.TestSupport;

public sealed record RecordedRequest(HttpMethod Method, string Path, string? MediaType, string Body)
{
    /// <summary>The last path segment, e.g. "paymentToken".</summary>
    public string Endpoint => Path[(Path.LastIndexOf('/') + 1)..];
}

/// <summary>
/// Answers HTTP calls from a queue of canned responses per endpoint (the last path segment)
/// and records every request, so tests can check what was sent and how often.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public StubHttpMessageHandler RespondJson(
        string endpoint,
        string json,
        HttpStatusCode status = HttpStatusCode.OK)
        => Respond(endpoint, () => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    public StubHttpMessageHandler Throw(string endpoint, Exception exception)
        => Respond(endpoint, () => throw exception);

    public StubHttpMessageHandler Respond(string endpoint, Func<HttpResponseMessage> response)
    {
        if (!_responses.TryGetValue(endpoint, out var queue))
        {
            _responses[endpoint] = queue = new Queue<Func<HttpResponseMessage>>();
        }

        queue.Enqueue(response);
        return this;
    }

    public int CallsTo(string endpoint) => Requests.Count(r => r.Endpoint == endpoint);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!.AbsolutePath,
            request.Content?.Headers.ContentType?.MediaType,
            body);

        Requests.Add(recorded);

        if (!_responses.TryGetValue(recorded.Endpoint, out var queue) || queue.Count == 0)
        {
            throw new InvalidOperationException($"No stubbed response left for '{recorded.Endpoint}'.");
        }

        return queue.Dequeue()();
    }
}
