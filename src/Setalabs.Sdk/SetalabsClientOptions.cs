namespace Setalabs.Sdk;

/// <summary>Options for <see cref="SetalabsClient"/>.</summary>
public sealed class SetalabsClientOptions
{
    /// <summary>
    /// Gateway root, for example <c>https://gw.setalabs.com</c>.
    /// Null or whitespace uses <see cref="Gateway.Url"/>. The value must not include <c>/v1/api/{uuid}</c>.
    /// </summary>
    public string? Gateway { get; set; }

    /// <summary>Default request timeout. Must be greater than zero.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// HTTP client used for every call. When null, the SDK creates one client and reuses it.
    /// A caller-supplied instance is not disposed.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>Clock used for <c>X-Api-Timestamp</c>. Null uses <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? TimeProvider { get; set; }
}
