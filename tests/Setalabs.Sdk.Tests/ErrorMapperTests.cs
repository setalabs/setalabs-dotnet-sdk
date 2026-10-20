using System.Text.Json;

namespace Setalabs.Sdk.Tests;

public class ErrorMapperTests
{
    public static IEnumerable<object[]> GatewayErrors()
    {
        yield return Row(400, ErrorCodes.BadRequest, "API has no base URL.", typeof(BadRequestException), "API has no base URL.");
        yield return Row(401, ErrorCodes.Unauthorized, "Missing API key.", typeof(UnauthorizedException), "Missing API key.");
        yield return Row(401, ErrorCodes.Unauthorized, "Invalid API key.", typeof(UnauthorizedException), "Invalid API key.");
        yield return Row(401, ErrorCodes.Unauthorized, "Missing API signature.", typeof(UnauthorizedException), "Missing API signature.");
        yield return Row(401, ErrorCodes.Unauthorized, "Missing API timestamp.", typeof(UnauthorizedException), "Missing API timestamp.");
        yield return Row(401, ErrorCodes.Unauthorized, "Invalid API signature.", typeof(UnauthorizedException), "Invalid API signature.");
        yield return Row(403, ErrorCodes.Forbidden, "API key is blocked.", typeof(ForbiddenException), "API key is blocked.");
        yield return Row(403, ErrorCodes.Forbidden, "API key subscription has expired.", typeof(ForbiddenException), "API key subscription has expired.");
        yield return Row(403, ErrorCodes.Forbidden, "API key has no active tariff.", typeof(ForbiddenException), "API key has no active tariff.");
        yield return Row(403, ErrorCodes.Forbidden, "API key is not valid for this API.", typeof(ForbiddenException), "API key is not valid for this API.");
        yield return Row(404, ErrorCodes.NotFound, "API not found.", typeof(NotFoundException), "API not found.");
        yield return Row(404, ErrorCodes.NotFound, "API endpoint not found.", typeof(NotFoundException), "API endpoint not found.");
        yield return Row(404, ErrorCodes.NotFound, null, typeof(NotFoundException), ErrorCodes.NotFound);
        yield return Row(502, ErrorCodes.BadGateway, "Upstream API is unavailable.", typeof(BadGatewayException), "Upstream API is unavailable.");
        yield return Row(500, ErrorCodes.InternalError, "", typeof(ServerException), ErrorCodes.InternalError);
    }

    [Theory]
    [MemberData(nameof(GatewayErrors))]
    public void Maps_gateway_error(int statusCode, string body, Type expectedType, string expectedMessage)
    {
        var exception = ErrorMapper.FromResponse(Response(statusCode, body));

        Assert.IsType(expectedType, exception);
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal("abc123", exception.RequestId);
    }

    [Fact]
    public void Validation_error_includes_missing_fields()
    {
        var body = JsonSerializer.Serialize(new
        {
            error = ErrorCodes.ValidationError,
            reason = "Required fields are missing.",
            missing_fields = new[] { "page", "limit" },
        });

        var exception = ErrorMapper.FromResponse(Response(422, body));

        var validation = Assert.IsType<ValidationException>(exception);
        Assert.Equal(new[] { "page", "limit" }, validation.MissingFields);
        Assert.Equal("Required fields are missing. Missing: page, limit.", validation.Message);
    }

    [Fact]
    public void Rate_limit_reads_retry_after()
    {
        var body = JsonSerializer.Serialize(new
        {
            error = ErrorCodes.RateLimited,
            retry_after = 3,
        });
        var exception = ErrorMapper.FromResponse(new ApiResponse(429, new Dictionary<string, IReadOnlyList<string>>
        {
            ["Retry-After"] = new[] { "3" },
            ["X-RateLimit-Limit"] = new[] { "10" },
        }, body));

        var rate = Assert.IsType<RateLimitException>(exception);
        Assert.IsAssignableFrom<TooManyRequestsException>(rate);
        Assert.Equal(3, rate.RetryAfter);
    }

    [Fact]
    public void Usage_limit()
    {
        var body = JsonSerializer.Serialize(new
        {
            error = ErrorCodes.UsageLimitExceeded,
            reason = "Request limit exceeded.",
        });

        var exception = ErrorMapper.FromResponse(Response(429, body));

        var usage = Assert.IsType<UsageLimitException>(exception);
        Assert.Equal("Request limit exceeded.", usage.Message);
        Assert.Null(usage.RetryAfter);
    }

    [Fact]
    public void Plain_upstream_error_uses_status()
    {
        var exception = ErrorMapper.FromResponse(Response(418, "nope"));

        Assert.IsType<ApiException>(exception);
        Assert.Equal("HTTP 418", exception.Message);
        Assert.Equal("", exception.ErrorCode);
        Assert.Equal("nope", exception.Response!.Body);
    }

    [Fact]
    public void Status_fallback_maps_503_and_429()
    {
        Assert.IsType<BadGatewayException>(ErrorMapper.FromResponse(Response(503, "nope")));
        Assert.IsType<TooManyRequestsException>(ErrorMapper.FromResponse(Response(429, "nope")));
    }

    private static object[] Row(int status, string error, string? reason, Type type, string message)
    {
        return new object[] { status, Payload(error, reason), type, message };
    }

    private static string Payload(string error, string? reason)
    {
        var payload = new Dictionary<string, string> { ["error"] = error };
        if (reason is not null)
        {
            payload["reason"] = reason;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static ApiResponse Response(int statusCode, string body)
    {
        return new ApiResponse(statusCode, new Dictionary<string, IReadOnlyList<string>>
        {
            ["X-Request-Id"] = new[] { "abc123" },
        }, body);
    }
}
