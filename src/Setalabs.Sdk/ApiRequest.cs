namespace Setalabs.Sdk;

/// <summary>One call made through <see cref="SetalabsClient.SendAsync"/>.</summary>
/// <remarks>
/// A null <see cref="Timeout"/> uses the client timeout. <see cref="Query"/> may be a
/// <see cref="QueryParameters"/> value, a dictionary, or a raw query string.
/// Set <see cref="HasJson"/> or <see cref="Body"/>, not both. A string <see cref="Json"/>
/// value is sent unchanged. Set <see cref="HasJson"/> with a null <see cref="Json"/> to send JSON null.
/// </remarks>
public sealed class ApiRequest
{
    /// <summary>Query parameters, a dictionary, or a raw query string without a required leading <c>?</c>.</summary>
    public object? Query { get; init; }

    /// <summary>JSON payload. A string is sent unchanged.</summary>
    public object? Json { get; init; }

    /// <summary>Sends <see cref="Json"/>, including JSON null when <see cref="Json"/> is null.</summary>
    public bool HasJson { get; init; }

    /// <summary>Raw request body. Mutually exclusive with <see cref="HasJson"/>.</summary>
    public string? Body { get; init; }

    /// <summary>Extra request headers. Authentication headers are ignored.</summary>
    public IEnumerable<KeyValuePair<string, string>>? Headers { get; init; }

    /// <summary>Request timeout. Null uses the client timeout. The value must be greater than zero.</summary>
    public TimeSpan? Timeout { get; init; }
}
