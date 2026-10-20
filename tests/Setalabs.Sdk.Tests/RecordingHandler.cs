namespace Setalabs.Sdk.Tests;

internal sealed record CapturedRequest(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = new();

    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Responder { get; set; }

    public bool WaitForCancellation { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
        }

        Requests.Add(new CapturedRequest(
            request.Method.Method,
            request.RequestUri?.OriginalString ?? "",
            headers,
            body));

        if (WaitForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (Responder is not null)
        {
            return await Responder(request, cancellationToken);
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
