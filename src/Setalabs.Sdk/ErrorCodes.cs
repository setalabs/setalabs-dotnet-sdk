namespace Setalabs.Sdk;

/// <summary>Error codes returned by the Setalabs gateway.</summary>
public static class ErrorCodes
{
    /// <summary>The API has no base URL, or the gateway rejected the request.</summary>
    public const string BadRequest = "bad_request";

    /// <summary>Missing or invalid key, signature, or timestamp.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>The key is blocked, expired, without a tariff, or belongs to another API.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The API or endpoint was not found.</summary>
    public const string NotFound = "not_found";

    /// <summary>Required fields are missing.</summary>
    public const string ValidationError = "validation_error";

    /// <summary>Upstream is unavailable.</summary>
    public const string BadGateway = "bad_gateway";

    /// <summary>Gateway internal error.</summary>
    public const string InternalError = "internal_error";

    /// <summary>Plan RPS limit exceeded.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>Call quota for the period is exhausted.</summary>
    public const string UsageLimitExceeded = "usage_limit_exceeded";
}
