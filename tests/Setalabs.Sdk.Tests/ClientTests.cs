using System.Net;
using System.Text;

namespace Setalabs.Sdk.Tests;

public class ClientTests
{
    private const string Uuid = "05141812-7772-403e-9ca6-5e8e8bf22730";
    private const string Key = "key-1";
    private const string Secret = "secret-1";
    private const long Timestamp = 1_700_000_000;

    [Fact]
    public async Task Signed_get_uses_gateway_path_and_keeps_query_order()
    {
        var handler = new RecordingHandler
        {
            Responder = (_, _) => Task.FromResult(JsonResponse(200, """{"ok":true}""", ("X-Request-Id", "req-1"))),
        };
        using var client = Create(handler);

        var response = await client.GetAsync("/catalog", new QueryParameters
        {
            { "b", "2" },
            { "a", "1" },
        });

        Assert.True(response.IsSuccessful);
        Assert.True(response.ReadJson().GetProperty("ok").GetBoolean());
        Assert.Equal("req-1", response.RequestId);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"ok":true}""", response.Body);
        Assert.Equal("req-1", response.Header("X-Request-Id"));

        var call = handler.Requests[0];
        var pathWithQuery = "/v1/api/" + Uuid + "/catalog?b=2&a=1";
        Assert.Equal("GET", call.Method);
        Assert.Equal(Gateway.Url + pathWithQuery, call.Url);
        Assert.Equal(Key, call.Headers["X-Api-Key"]);
        Assert.False(call.Headers.ContainsKey("X-Api-Secret"));
        Assert.Equal(Timestamp.ToString(), call.Headers["X-Api-Timestamp"]);
        Assert.Equal(Signer.Sign(Key, Secret, call.Headers["X-Api-Timestamp"], pathWithQuery), call.Headers["X-Api-Sign"]);
        Assert.Equal("application/json", call.Headers["Accept"]);
        Assert.Equal("setalabs-dotnet-sdk/" + SetalabsClient.Version, call.Headers["User-Agent"]);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Signature_uses_decoded_path_and_encoded_query()
    {
        var handler = new RecordingHandler();
        using var client = Create(handler);

        await client.GetAsync("/product/a b", new QueryParameters { { "q", "x y" } });

        var call = handler.Requests[0];
        var signed = "/v1/api/" + Uuid + "/product/a b?q=x%20y";
        var wire = Gateway.Url + "/v1/api/" + Uuid + "/product/a%20b?q=x%20y";
        Assert.Equal(wire, call.Url);
        Assert.Equal(Signer.Sign(Key, Secret, Timestamp.ToString(), signed), call.Headers["X-Api-Sign"]);
    }

    [Fact]
    public async Task Key_without_secret_is_not_signed()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new SetalabsClient(Uuid, Key, "", Options(http));

        await client.GetAsync("/langs");

        var headers = handler.Requests[0].Headers;
        Assert.Equal(Key, headers["X-Api-Key"]);
        Assert.False(headers.ContainsKey("X-Api-Sign"));
        Assert.False(headers.ContainsKey("X-Api-Timestamp"));
        Assert.False(headers.ContainsKey("X-Api-Secret"));
        Assert.Equal(Gateway.Url + "/v1/api/" + Uuid + "/langs", handler.Requests[0].Url);
    }

    [Fact]
    public async Task Post_sends_json_body()
    {
        var handler = new RecordingHandler
        {
            Responder = (_, _) => Task.FromResult(JsonResponse(201, """{"id":"1"}""")),
        };
        using var client = Create(handler);

        var response = await client.PostAsync("/orders", new Dictionary<string, string> { ["sku"] = "a/b" });

        Assert.Equal(201, response.StatusCode);
        Assert.Equal("""{"sku":"a/b"}""", handler.Requests[0].Body);
        Assert.Equal("application/json", handler.Requests[0].Headers["Content-Type"]);
        Assert.Equal("POST", handler.Requests[0].Method);
    }

    [Fact]
    public async Task Gateway_error_is_thrown_with_status_headers_and_body()
    {
        var handler = new RecordingHandler
        {
            Responder = (_, _) => Task.FromResult(JsonResponse(
                401,
                """{"error":"unauthorized","reason":"Invalid API signature."}""",
                ("X-Request-Id", "req-9"))),
        };
        using var client = Create(handler);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => client.GetAsync("/catalog"));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("Invalid API signature.", exception.Message);
        Assert.Equal("req-9", exception.RequestId);
        Assert.Equal(401, exception.Response!.StatusCode);
        Assert.Contains("Invalid API signature.", exception.Response.Body);
    }

    [Fact]
    public async Task Reserved_secret_header_is_dropped()
    {
        var handler = new RecordingHandler();
        using var client = Create(handler);

        await client.GetAsync("/catalog", headers: new Dictionary<string, string>
        {
            ["X-Api-Secret"] = "do-not-send",
            ["X-Trace"] = "1",
        });

        var headers = handler.Requests[0].Headers;
        Assert.False(headers.ContainsKey("X-Api-Secret"));
        Assert.Equal("1", headers["X-Trace"]);
    }

    [Fact]
    public async Task WithApi_targets_another_uuid_on_the_same_handler()
    {
        var handler = new RecordingHandler();
        using var client = Create(handler);
        using var other = client.WithApi("another-api", "other-key", "other-secret");

        await client.GetAsync("/catalog");
        await other.GetAsync("/health");

        Assert.Equal(2, handler.Requests.Count);
        var call = handler.Requests[1];
        Assert.Contains("/v1/api/another-api/health", call.Url);
        Assert.Equal("other-key", call.Headers["X-Api-Key"]);
        const string path = "/v1/api/another-api/health";
        Assert.Equal(Signer.Sign("other-key", "other-secret", Timestamp.ToString(), path), call.Headers["X-Api-Sign"]);
    }

    [Fact]
    public async Task Gateway_defaults_to_production()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new SetalabsClient(Uuid, Key, Secret, new SetalabsClientOptions
        {
            HttpClient = http,
            TimeProvider = new FixedTimeProvider(Timestamp),
        });

        await client.GetAsync("/catalog");

        Assert.Equal(Gateway.Url + "/v1/api/" + Uuid + "/catalog", handler.Requests[0].Url);
    }

    [Fact]
    public async Task Gateway_can_be_replaced()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new SetalabsClient(Uuid, Key, Secret, new SetalabsClientOptions
        {
            Gateway = "https://gw.example.test/",
            HttpClient = http,
            TimeProvider = new FixedTimeProvider(Timestamp),
        });

        await client.GetAsync("/catalog");

        Assert.Equal("https://gw.example.test/v1/api/" + Uuid + "/catalog", handler.Requests[0].Url);
    }

    [Fact]
    public void Gateway_must_be_the_root_url()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new SetalabsClient(Uuid, Key, Secret, new SetalabsClientOptions
            {
                Gateway = Gateway.Url + "/v1/api/" + Uuid,
            }));

        Assert.Contains("gateway root", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_in_path_is_rejected()
    {
        var handler = new RecordingHandler();
        using var client = Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAsync("/catalog?language=en"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Send_puts_json_query_and_timeout_on_one_request()
    {
        var handler = new RecordingHandler();
        using var client = Create(handler);

        await client.SendAsync("put", "/orders/1", new ApiRequest
        {
            HasJson = true,
            Json = new Dictionary<string, string> { ["status"] = "paid" },
            Query = new QueryParameters { { "language", "en" } },
            Timeout = TimeSpan.FromSeconds(10),
        });

        var call = handler.Requests[0];
        Assert.Equal("PUT", call.Method);
        Assert.Equal("""{"status":"paid"}""", call.Body);
        var signed = "/v1/api/" + Uuid + "/orders/1?language=en";
        Assert.Equal(Gateway.Url + signed, call.Url);
        Assert.Equal(Signer.Sign(Key, Secret, Timestamp.ToString(), signed), call.Headers["X-Api-Sign"]);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync("POST", "/orders", new ApiRequest
        {
            HasJson = true,
            Json = new Dictionary<string, string> { ["sku"] = "A1" },
            Body = "raw",
        }));
    }

    [Fact]
    public async Task Two_calls_reuse_the_same_handler()
    {
        var handler = new RecordingHandler();
        using var client = Create(handler);

        await client.GetAsync("/catalog");
        await client.GetAsync("/langs");

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/catalog", handler.Requests[0].Url);
        Assert.EndsWith("/langs", handler.Requests[1].Url);
    }

    [Fact]
    public async Task Dispose_does_not_dispose_a_caller_supplied_http_client()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = Create(http);
        var other = client.WithApi("another-api", "other-key", "other-secret");

        client.Dispose();
        other.Dispose();

        var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, Gateway.Url + "/"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        http.Dispose();
    }

    [Fact]
    public async Task Caller_cancellation_is_not_wrapped()
    {
        var handler = new RecordingHandler { WaitForCancellation = true };
        using var client = Create(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("/catalog", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Timeout_is_a_transport_error()
    {
        var handler = new RecordingHandler { WaitForCancellation = true };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var client = new SetalabsClient(Uuid, Key, Secret, new SetalabsClientOptions
        {
            Gateway = Gateway.Url,
            Timeout = TimeSpan.FromMilliseconds(200),
            HttpClient = http,
            TimeProvider = new FixedTimeProvider(Timestamp),
        });

        var exception = await Assert.ThrowsAsync<TransportException>(() => client.GetAsync("/catalog"));
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task Redirect_status_is_returned_unchanged()
    {
        var handler = new RecordingHandler
        {
            Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
            {
                Headers = { Location = new Uri("https://gw.setalabs.com/other") },
            }),
        };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var client = new SetalabsClient(Uuid, Key, "", Options(http));

        var response = await client.GetAsync("/catalog");

        Assert.Equal(302, response.StatusCode);
        Assert.Single(handler.Requests);
    }

    private static SetalabsClient Create(RecordingHandler handler)
    {
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return Create(http);
    }

    private static SetalabsClient Create(HttpClient http)
    {
        return new SetalabsClient(Uuid, Key, Secret, Options(http));
    }

    private static SetalabsClientOptions Options(HttpClient http)
    {
        return new SetalabsClientOptions
        {
            Gateway = Gateway.Url,
            Timeout = TimeSpan.FromSeconds(30),
            HttpClient = http,
            TimeProvider = new FixedTimeProvider(Timestamp),
        };
    }

    private static HttpResponseMessage JsonResponse(int status, string body, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        foreach (var header in headers)
        {
            response.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        return response;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(long unixSeconds)
        {
            _utcNow = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
