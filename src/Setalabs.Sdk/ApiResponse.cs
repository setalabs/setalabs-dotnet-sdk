using System.Text.Json;

namespace Setalabs.Sdk;

/// <summary>
/// Upstream status, headers, and body returned by the gateway.
/// Header names are matched without case sensitivity.
/// </summary>
public sealed class ApiResponse
{
    /// <summary>Creates a response from an HTTP status, headers, and body.</summary>
    public ApiResponse(int statusCode, IReadOnlyDictionary<string, IReadOnlyList<string>>? headers, string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        StatusCode = statusCode;
        Body = body;

        var normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                var values = new List<string>();
                if (normalized.TryGetValue(header.Key, out var existing))
                {
                    values.AddRange(existing);
                }

                values.AddRange(header.Value);
                normalized[header.Key] = values;
            }
        }

        Headers = normalized;
    }

    /// <summary>HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Response headers. Lookup is case-insensitive.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    /// <summary>Raw response body.</summary>
    public string Body { get; }

    /// <summary>Reports whether the status is in the 2xx range.</summary>
    public bool IsSuccessful => StatusCode is >= 200 and < 300;

    /// <summary>Returns the first value of a header, or null when the header is absent.</summary>
    public string? Header(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Headers.TryGetValue(name, out var values) && values.Count > 0 ? values[0] : null;
    }

    /// <summary>Gateway <c>X-Request-Id</c> header.</summary>
    public string? RequestId => Header("X-Request-Id");

    /// <summary>Decodes the response body as JSON.</summary>
    public JsonElement ReadJson()
    {
        if (Body.Length == 0)
        {
            throw new SetalabsException("Response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(Body);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new SetalabsException("Response body is not valid JSON.", exception);
        }
    }

    /// <summary>Decodes the response body as <typeparamref name="T"/>.</summary>
    public T? ReadJson<T>()
    {
        if (Body.Length == 0)
        {
            throw new SetalabsException("Response body is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(Body);
        }
        catch (JsonException exception)
        {
            throw new SetalabsException("Response body is not valid JSON.", exception);
        }
    }
}
