namespace Setalabs.Sdk;

/// <summary>Base type for gateway and transport errors.</summary>
public class SetalabsException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public SetalabsException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public SetalabsException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The request did not receive an HTTP response.</summary>
public sealed class TransportException : SetalabsException
{
    /// <summary>Creates a transport exception.</summary>
    public TransportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>A gateway or upstream response with status 400 or higher.</summary>
public class ApiException : SetalabsException
{
    /// <summary>Creates an API exception from a gateway error response.</summary>
    public ApiException(
        string message,
        int statusCode,
        string errorCode,
        string? reason = null,
        string? requestId = null,
        IReadOnlyList<string>? missingFields = null,
        int? retryAfter = null,
        ApiResponse? response = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Reason = reason;
        RequestId = requestId;
        MissingFields = missingFields ?? Array.Empty<string>();
        RetryAfter = retryAfter;
        Response = response;
    }

    /// <summary>HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Gateway <c>error</c> code. Empty when the body is not a gateway error object.</summary>
    public string ErrorCode { get; }

    /// <summary>Gateway <c>reason</c>. Null when the field is absent.</summary>
    public string? Reason { get; }

    /// <summary>Gateway <c>X-Request-Id</c>.</summary>
    public string? RequestId { get; }

    /// <summary>Field names reported with <c>validation_error</c>.</summary>
    public IReadOnlyList<string> MissingFields { get; }

    /// <summary>Seconds to wait before retrying, when the gateway reports a limit.</summary>
    public int? RetryAfter { get; }

    /// <summary>Original response, including status, headers, and body.</summary>
    public ApiResponse? Response { get; }
}

/// <summary>The API has no base URL, or the gateway rejected the request.</summary>
public sealed class BadRequestException : ApiException
{
    /// <summary>Creates a bad-request exception.</summary>
    public BadRequestException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>Missing or invalid key, signature, or timestamp.</summary>
public sealed class UnauthorizedException : ApiException
{
    /// <summary>Creates an unauthorized exception.</summary>
    public UnauthorizedException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>The key is blocked, expired, without a tariff, or belongs to another API.</summary>
public sealed class ForbiddenException : ApiException
{
    /// <summary>Creates a forbidden exception.</summary>
    public ForbiddenException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>The API or endpoint was not found.</summary>
public sealed class NotFoundException : ApiException
{
    /// <summary>Creates a not-found exception.</summary>
    public NotFoundException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>Required fields are missing. Their names are in <see cref="ApiException.MissingFields"/>.</summary>
public sealed class ValidationException : ApiException
{
    /// <summary>Creates a validation exception.</summary>
    public ValidationException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>The gateway refused the call because of a limit. Status is 429.</summary>
public class TooManyRequestsException : ApiException
{
    /// <summary>Creates a too-many-requests exception.</summary>
    public TooManyRequestsException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>Plan RPS limit exceeded. Wait <see cref="ApiException.RetryAfter"/> seconds.</summary>
public sealed class RateLimitException : TooManyRequestsException
{
    /// <summary>Creates a rate-limit exception.</summary>
    public RateLimitException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>Call quota for the period is exhausted.</summary>
public sealed class UsageLimitException : TooManyRequestsException
{
    /// <summary>Creates a usage-limit exception.</summary>
    public UsageLimitException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>Gateway internal error.</summary>
public sealed class ServerException : ApiException
{
    /// <summary>Creates a server exception.</summary>
    public ServerException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}

/// <summary>Upstream is unavailable.</summary>
public sealed class BadGatewayException : ApiException
{
    /// <summary>Creates a bad-gateway exception.</summary>
    public BadGatewayException(
        string message,
        int statusCode,
        string errorCode,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse? response)
        : base(message, statusCode, errorCode, reason, requestId, missingFields, retryAfter, response)
    {
    }
}
