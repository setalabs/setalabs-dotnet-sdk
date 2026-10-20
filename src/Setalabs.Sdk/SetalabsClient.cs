using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Setalabs.Sdk;

/// <summary>
/// Calls one API published on the Setalabs gateway.
/// </summary>
/// <remarks>
/// The gateway address, API uuid, key, and secret are separate: a key belongs to a single API,
/// and the secret is used only to sign the request. It is never sent.
/// Paid requests are not retried.
/// </remarks>
public sealed class SetalabsClient : IDisposable
{
    /// <summary>SDK release sent in the User-Agent header.</summary>
    public const string Version = "1.0.0";

    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "HEAD",
    };

    private static readonly HashSet<string> ReservedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-Api-Key",
        "X-Api-Secret",
        "X-Api-Sign",
        "X-Api-Timestamp",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _sync = new();
    private readonly string _gateway;
    private readonly string _apiUuid;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly HttpClientLease _lease;
    private bool _disposed;

    /// <summary>Creates a client for one API. <paramref name="apiSecret"/> may be empty when the key is not signed.</summary>
    public SetalabsClient(string apiUuid, string apiKey, string apiSecret = "", SetalabsClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(apiUuid);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(apiSecret);

        options ??= new SetalabsClientOptions();
        _gateway = NormalizeGateway(options.Gateway);
        _timeout = options.Timeout;
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Timeout must be greater than zero.");
        }

        _apiUuid = NormalizeUuid(apiUuid);
        _apiKey = NormalizeCredential(apiKey, "API key", required: true);
        _apiSecret = NormalizeCredential(apiSecret, "API secret", required: false);
        _timeProvider = options.TimeProvider ?? TimeProvider.System;

        var httpClient = options.HttpClient;
        var owned = httpClient is null;
        _lease = new HttpClientLease(httpClient ?? CreateHttpClient(), owned);
    }

    private SetalabsClient(
        string apiUuid,
        string apiKey,
        string apiSecret,
        string gateway,
        TimeSpan timeout,
        TimeProvider timeProvider,
        HttpClientLease lease)
    {
        try
        {
            _gateway = gateway;
            _timeout = timeout;
            _timeProvider = timeProvider;
            _apiUuid = NormalizeUuid(apiUuid);
            _apiKey = NormalizeCredential(apiKey, "API key", required: true);
            _apiSecret = NormalizeCredential(apiSecret, "API secret", required: false);
            _lease = lease;
        }
        catch
        {
            lease.Release();
            throw;
        }
    }

    /// <summary>
    /// Same gateway and timeout, another API.
    /// A key is valid only for the API it was issued for.
    /// </summary>
    public SetalabsClient WithApi(string apiUuid, string apiKey, string apiSecret = "")
    {
        ArgumentNullException.ThrowIfNull(apiUuid);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(apiSecret);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new SetalabsClient(apiUuid, apiKey, apiSecret, _gateway, _timeout, _timeProvider, _lease.Acquire());
        }
    }

    /// <summary>Calls an endpoint. <paramref name="query"/> may be null, <see cref="QueryParameters"/>, a dictionary, or a raw query string.</summary>
    public Task<ApiResponse> GetAsync(
        string path,
        object? query = null,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync("GET", path, new ApiRequest { Query = query, Headers = headers }, cancellationToken);
    }

    /// <summary>Sends <paramref name="json"/> when it is not null. A null value sends no body.</summary>
    public Task<ApiResponse> PostAsync(
        string path,
        object? json = null,
        object? query = null,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return SendWithJsonAsync("POST", path, json, query, headers, cancellationToken);
    }

    /// <summary>Sends <paramref name="json"/> when it is not null. A null value sends no body.</summary>
    public Task<ApiResponse> PutAsync(
        string path,
        object? json = null,
        object? query = null,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return SendWithJsonAsync("PUT", path, json, query, headers, cancellationToken);
    }

    /// <summary>Sends <paramref name="json"/> when it is not null. A null value sends no body.</summary>
    public Task<ApiResponse> PatchAsync(
        string path,
        object? json = null,
        object? query = null,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return SendWithJsonAsync("PATCH", path, json, query, headers, cancellationToken);
    }

    /// <summary>Sends <paramref name="json"/> when it is not null. A null value sends no body.</summary>
    public Task<ApiResponse> DeleteAsync(
        string path,
        object? json = null,
        object? query = null,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return SendWithJsonAsync("DELETE", path, json, query, headers, cancellationToken);
    }

    /// <summary>
    /// Sends one signed call. <paramref name="path"/> is the endpoint path from the OpenAPI document,
    /// without the <c>/v1/api/{uuid}</c> prefix.
    /// </summary>
    public async Task<ApiResponse> SendAsync(
        string method,
        string path,
        ApiRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        method = method.ToUpperInvariant();
        if (!Methods.Contains(method))
        {
            throw new ArgumentException($"Unsupported HTTP method \"{method}\".");
        }

        var headers = NormalizeHeaders(request?.Headers);
        var queryString = QueryStringBuilder.Build(request?.Query);
        var decodedPath = DecodedPath(path);
        var pathWithQuery = queryString.Length == 0 ? decodedPath : decodedPath + "?" + queryString;
        var body = CreateBody(request, headers);

        headers["X-Api-Key"] = _apiKey;
        if (_apiSecret.Length > 0)
        {
            var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            headers["X-Api-Timestamp"] = timestamp;
            headers["X-Api-Sign"] = Signer.Sign(_apiKey, _apiSecret, timestamp, pathWithQuery);
        }

        var url = _gateway + EncodePath(decodedPath);
        if (queryString.Length > 0)
        {
            url += "?" + queryString;
        }

        var timeout = _timeout;
        if (request?.Timeout is not null)
        {
            if (request.Timeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout must be greater than zero.");
            }

            timeout = request.Timeout.Value;
        }

        using var httpRequest = CreateHttpRequest(method, url, headers, body);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var httpResponse = await _lease.HttpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token).ConfigureAwait(false);
            var responseBody = httpResponse.Content is null
                ? ""
                : await httpResponse.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var response = new ApiResponse((int)httpResponse.StatusCode, CopyHeaders(httpResponse), responseBody);
            if (response.StatusCode >= 400)
            {
                throw ErrorMapper.FromResponse(response);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new TransportException(exception.Message.Length > 0 ? exception.Message : "Request failed.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new TransportException(exception.Message.Length > 0 ? exception.Message : "Request failed.", exception);
        }
    }

    /// <summary>
    /// Releases one use of the shared HTTP client.
    /// A caller-supplied <see cref="HttpClient"/> is left open.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lease.Release();
        }
    }

    private Task<ApiResponse> SendWithJsonAsync(
        string method,
        string path,
        object? json,
        object? query,
        IEnumerable<KeyValuePair<string, string>>? headers,
        CancellationToken cancellationToken)
    {
        return SendAsync(method, path, new ApiRequest
        {
            Query = query,
            Headers = headers,
            Json = json,
            HasJson = json is not null,
        }, cancellationToken);
    }

    private string DecodedPath(string path)
    {
        path = path.Trim();
        if (path.Contains('?', StringComparison.Ordinal) || path.Contains('#', StringComparison.Ordinal))
        {
            throw new ArgumentException("Pass query parameters separately from the path.");
        }

        var relative = path is "" or "/" ? "" : "/" + path.TrimStart('/');
        return Gateway.ApiPrefix + "/" + _apiUuid + relative;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.None,
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static string NormalizeGateway(string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            gateway = Gateway.Url;
        }

        gateway = gateway.Trim().TrimEnd('/');
        if (!Uri.TryCreate(gateway, UriKind.Absolute, out var uri) || !IsHttp(uri))
        {
            throw new ArgumentException("Gateway address must be an absolute URL, for example https://gw.setalabs.com.");
        }

        if (uri.AbsolutePath is not ("/" or ""))
        {
            throw new ArgumentException("Gateway address must be the gateway root, without /v1/api/{uuid}. Pass the API uuid separately.");
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new ArgumentException("Gateway address must not contain a query or fragment.");
        }

        return gateway;
    }

    private static bool IsHttp(Uri uri)
    {
        return uri.IdnHost.Length > 0
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeUuid(string apiUuid)
    {
        apiUuid = apiUuid.Trim().ToLowerInvariant();
        if (!IsApiUuid(apiUuid))
        {
            throw new ArgumentException("API uuid must contain only lowercase letters, digits, \"_\" and \"-\".");
        }

        return apiUuid;
    }

    private static bool IsApiUuid(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            var allowed = character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeCredential(string value, string label, bool required)
    {
        value = value.Trim();
        if (required && value.Length == 0)
        {
            throw new ArgumentException("API key is required.");
        }

        AssertHeaderSafe(label, value);
        return value;
    }

    private static void AssertHeaderSafe(string label, string value)
    {
        if (value.Contains('\r') || value.Contains('\n'))
        {
            throw new ArgumentException(label + " must not contain line breaks.");
        }
    }

    private static Dictionary<string, string> NormalizeHeaders(IEnumerable<KeyValuePair<string, string>>? headers)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    throw new ArgumentException("Header name must be a non-empty string.");
                }

                if (header.Value is null)
                {
                    throw new ArgumentException($"Header \"{header.Key}\" must be a scalar.");
                }

                if (ReservedHeaders.Contains(header.Key))
                {
                    continue;
                }

                AssertHeaderSafe($"Header \"{header.Key}\"", header.Value);
                normalized.Remove(header.Key);
                normalized[header.Key] = header.Value;
            }
        }

        if (!normalized.ContainsKey("Accept"))
        {
            normalized["Accept"] = "application/json";
        }

        if (!normalized.ContainsKey("User-Agent"))
        {
            normalized["User-Agent"] = "setalabs-dotnet-sdk/" + Version;
        }

        return normalized;
    }

    private static string? CreateBody(ApiRequest? request, Dictionary<string, string> headers)
    {
        var hasJson = request?.HasJson == true;
        var hasBody = request?.Body is not null;
        if (hasJson && hasBody)
        {
            throw new ArgumentException("Pass either json or body, not both.");
        }

        if (hasJson)
        {
            if (!headers.ContainsKey("Content-Type"))
            {
                headers["Content-Type"] = "application/json";
            }

            return EncodeJson(request!.Json);
        }

        return hasBody ? request!.Body : null;
    }

    private static string EncodeJson(object? value)
    {
        if (value is string text)
        {
            return text;
        }

        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException)
        {
            throw new ArgumentException("JSON body could not be encoded.", exception);
        }
    }

    private static string EncodePath(string decodedPath)
    {
        var segments = decodedPath.Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length > 0)
            {
                segments[index] = QueryStringBuilder.EscapeRfc3986(segments[index]);
            }
        }

        return string.Join('/', segments);
    }

    private static HttpRequestMessage CreateHttpRequest(
        string method,
        string url,
        Dictionary<string, string> headers,
        string? body)
    {
        var request = new HttpRequestMessage(
            new HttpMethod(method),
            new Uri(url, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true }));
        HttpContent? content = body is null ? null : new ByteArrayContent(Encoding.UTF8.GetBytes(body));

        foreach (var header in headers)
        {
            if (content is not null && content.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                content ??= new ByteArrayContent(Array.Empty<byte>());
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        request.Content = content;
        return request;
    }

    private static Dictionary<string, IReadOnlyList<string>> CopyHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Add(response.Headers);
        if (response.Content is not null)
        {
            Add(response.Content.Headers);
        }

        return headers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);

        void Add(HttpHeaders source)
        {
            foreach (var header in source)
            {
                if (!headers.TryGetValue(header.Key, out var values))
                {
                    values = new List<string>();
                    headers[header.Key] = values;
                }

                values.AddRange(header.Value);
            }
        }
    }

    private sealed class HttpClientLease
    {
        private readonly object _gate = new();
        private readonly bool _owned;
        private int _users;

        public HttpClientLease(HttpClient httpClient, bool owned)
        {
            HttpClient = httpClient;
            _owned = owned;
            _users = 1;
        }

        public HttpClient HttpClient { get; }

        public HttpClientLease Acquire()
        {
            if (!_owned)
            {
                return this;
            }

            lock (_gate)
            {
                if (_users == 0)
                {
                    throw new ObjectDisposedException(nameof(SetalabsClient));
                }

                _users++;
                return this;
            }
        }

        public void Release()
        {
            if (!_owned)
            {
                return;
            }

            HttpClient? disposable = null;
            lock (_gate)
            {
                if (_users == 0)
                {
                    return;
                }

                _users--;
                if (_users == 0)
                {
                    disposable = HttpClient;
                }
            }

            disposable?.Dispose();
        }
    }
}
