# Setalabs .NET SDK

Client for the Setalabs gateway. Each API has its own uuid, key, and set of endpoints. The gateway address is shared. The client is not tied to one marketplace: you pass the HTTP method, path, query, and body.

The key secret is used only to sign requests. It is never sent. Paid requests are not retried.

## Installation

```bash
dotnet add package Setalabs.Sdk
```

Until the package is published, pack it and add the local feed:

```bash
dotnet pack src/Setalabs.Sdk/Setalabs.Sdk.csproj -c Release
dotnet add package Setalabs.Sdk --source ../setalabs-dotnet-sdk/src/Setalabs.Sdk/bin/Release
```

Or reference the project:

```xml
<ProjectReference Include="..\setalabs-dotnet-sdk\src\Setalabs.Sdk\Setalabs.Sdk.csproj" />
```

The package targets .NET 8.

## Requests

The default gateway address is `Setalabs.Sdk.Gateway.Url` (`https://gw.setalabs.com`). Set `Gateway` to use another one. An OpenAPI server URL already includes the API uuid; this SDK takes the uuid separately.

```csharp
using System.Text.Json;
using Setalabs.Sdk;

using var client = new SetalabsClient(
    apiUuid: "05141812-7772-403e-9ca6-5e8e8bf22730",
    apiKey: "your-key",
    apiSecret: "your-secret");

using var local = new SetalabsClient(
    "05141812-7772-403e-9ca6-5e8e8bf22730",
    "your-key",
    "your-secret",
    new SetalabsClientOptions
    {
        Gateway = "http://localhost:8080",
        Timeout = TimeSpan.FromSeconds(30),
    });

var response = await client.GetAsync("/catalog", new QueryParameters
{
    { "language", "en" },
});

var data = response.ReadJson<JsonElement>();
```

The path is the endpoint path from the OpenAPI document (`/catalog`, `/product/{id}`, `/search`), without the `/v1/api/{uuid}` prefix. Substitute path parameters yourself: `/product/12345`.

Another API on the same gateway:

```csharp
using var other = client.WithApi("another-api-uuid", "other-key", "other-secret");
```

If the key has no secret, pass an empty string. The request is not signed.

```csharp
await client.PostAsync("/orders", new { sku = "A1" });

await client.SendAsync("PUT", "/orders/1", new ApiRequest
{
    HasJson = true,
    Json = new { status = "paid" },
    Query = new QueryParameters
    {
        { "language", "en" },
    },
    Timeout = TimeSpan.FromSeconds(10),
});
```

`QueryParameters` keeps parameter order. A dictionary is also accepted; its insertion order is preserved. A string is sent as the raw query, without the leading `?`. A string JSON value is sent unchanged. The signature covers the path and query, not the body.

A successful response is the upstream body and status, unchanged. The gateway returns the request id in `X-Request-Id`:

```csharp
response.StatusCode
response.Headers
response.Body
response.RequestId
response.ReadJson<JsonElement>()
```

## HttpClient

Pass an existing `HttpClient` when the application already owns one. The SDK reuses that instance and does not dispose it. When no client is passed, the SDK creates one, reuses it for every call, and disposes it after the last `SetalabsClient` that shares it is disposed. `WithApi` shares the same `HttpClient`.

```csharp
var http = new HttpClient();
using var client = new SetalabsClient(
    "05141812-7772-403e-9ca6-5e8e8bf22730",
    "your-key",
    "your-secret",
    new SetalabsClientOptions
    {
        HttpClient = http,
        Timeout = TimeSpan.FromSeconds(30),
    });
```

Every call is asynchronous and accepts a `CancellationToken`. The client timeout is 30 seconds unless `SetalabsClientOptions.Timeout` or `ApiRequest.Timeout` sets another positive value. A caller-supplied `HttpClient.Timeout` also applies. The default client does not follow redirects.

## Signature

When a secret is set, every request includes:

- `X-Api-Key`
- `X-Api-Timestamp` — Unix time in seconds
- `X-Api-Sign` — `HMAC-SHA256(key + secret + timestamp + path?query, secret)`, lowercase hex

`path?query` is the path the gateway sees, plus the raw query string. For example: `/v1/api/{uuid}/catalog?language=en`. Parameter order is preserved for `QueryParameters` and for dictionary insertion order. The query string uses the same encoding as PHP `http_build_query` with `PHP_QUERY_RFC3986`. The path in the signature is not percent-encoded; the URL on the wire is.

## Errors

A response with status 400 or higher throws. The message comes from the gateway `reason`. Every API error exposes `StatusCode`, `ErrorCode`, `Reason`, `RequestId`, `MissingFields`, `RetryAfter`, and the original `Response` (status, headers, and body).

| HTTP | `error` | Exception | When |
| --- | --- | --- | --- |
| 400 | `bad_request` | `BadRequestException` | The API has no base URL |
| 401 | `unauthorized` | `UnauthorizedException` | Missing or invalid key, missing or invalid signature, missing timestamp |
| 403 | `forbidden` | `ForbiddenException` | Key is blocked, subscription has expired, no active tariff, or the key belongs to another API |
| 404 | `not_found` | `NotFoundException` | API or endpoint was not found |
| 422 | `validation_error` | `ValidationException` | Required fields are missing; names are in `MissingFields` |
| 429 | `rate_limited` | `RateLimitException` | Plan RPS limit exceeded; wait `RetryAfter` seconds |
| 429 | `usage_limit_exceeded` | `UsageLimitException` | Call quota for the period is exhausted |
| 500 | `internal_error` | `ServerException` | Gateway internal error |
| 502 | `bad_gateway` | `BadGatewayException` | Upstream is unavailable |

A network failure throws `TransportException`. `SetalabsException` is the base type for gateway and transport errors. Invalid client arguments throw `ArgumentException`.

`RateLimitException` and `UsageLimitException` can also be caught as `TooManyRequestsException`. The SDK does not retry a failed call.

```csharp
try
{
    var data = await client.GetAsync("/search", new QueryParameters
    {
        { "text", "phone" },
        { "page", 1 },
        { "limit", 24 },
    });
}
catch (ValidationException exception)
{
    var fields = exception.MissingFields;
}
catch (RateLimitException exception)
{
    var wait = exception.RetryAfter ?? 1;
    await Task.Delay(TimeSpan.FromSeconds(wait));
}
catch (ApiException exception)
{
    var status = exception.StatusCode;
    var reason = exception.Reason;
    var requestId = exception.RequestId;
    var body = exception.Response?.Body;
}
```
