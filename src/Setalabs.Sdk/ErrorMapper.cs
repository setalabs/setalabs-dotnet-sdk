using System.Globalization;
using System.Text.Json;

namespace Setalabs.Sdk;

internal static class ErrorMapper
{
    public static ApiException FromResponse(ApiResponse response)
    {
        string error = "";
        string? reason = null;
        var missingFields = new List<string>();
        int? retryAfter = null;
        var hasRetryAfter = false;

        if (TryReadPayload(response.Body, out var payload))
        {
            if (payload.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
            {
                error = AsText(errorElement);
            }

            if (payload.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind != JsonValueKind.Null)
            {
                reason = AsText(reasonElement);
            }

            if (payload.TryGetProperty("missing_fields", out var missingElement))
            {
                missingFields = MissingFields(missingElement);
            }

            if (payload.TryGetProperty("retry_after", out var retryElement) && TryCoerceInt(retryElement, out var parsed))
            {
                retryAfter = parsed;
                hasRetryAfter = true;
            }
        }

        if (!hasRetryAfter && TryCoerceInt(response.Header("Retry-After"), out var headerRetry))
        {
            retryAfter = headerRetry;
        }

        var message = !string.IsNullOrEmpty(reason)
            ? reason
            : (error.Length > 0 ? error : "HTTP " + response.StatusCode.ToString(CultureInfo.InvariantCulture));
        if (missingFields.Count > 0)
        {
            message += " Missing: " + string.Join(", ", missingFields) + ".";
        }

        return Create(error, response.StatusCode, message, reason, response.RequestId, missingFields, retryAfter, response);
    }

    private static ApiException Create(
        string error,
        int statusCode,
        string message,
        string? reason,
        string? requestId,
        IReadOnlyList<string> missingFields,
        int? retryAfter,
        ApiResponse response)
    {
        return error switch
        {
            ErrorCodes.BadRequest => new BadRequestException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.Unauthorized => new UnauthorizedException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.Forbidden => new ForbiddenException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.NotFound => new NotFoundException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.ValidationError => new ValidationException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.RateLimited => new RateLimitException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.UsageLimitExceeded => new UsageLimitException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.BadGateway => new BadGatewayException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            ErrorCodes.InternalError => new ServerException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            _ => statusCode switch
            {
                400 => new BadRequestException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                401 => new UnauthorizedException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                403 => new ForbiddenException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                404 => new NotFoundException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                422 => new ValidationException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                429 => new TooManyRequestsException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                500 => new ServerException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                502 or 503 or 504 => new BadGatewayException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
                _ => new ApiException(message, statusCode, error, reason, requestId, missingFields, retryAfter, response),
            },
        };
    }

    private static bool TryReadPayload(string body, out JsonElement payload)
    {
        payload = default;
        if (body.Length == 0)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            payload = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<string> MissingFields(JsonElement value)
    {
        IEnumerable<JsonElement> items;
        if (value.ValueKind == JsonValueKind.Array)
        {
            items = value.EnumerateArray();
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            items = value.EnumerateObject().Select(property => property.Value);
        }
        else
        {
            return new List<string>();
        }

        var fields = new List<string>();
        foreach (var item in items)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var field = item.GetString();
                if (!string.IsNullOrEmpty(field))
                {
                    fields.Add(field);
                }
            }
        }

        return fields;
    }

    private static string AsText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.True => "1",
            JsonValueKind.False or JsonValueKind.Null => "",
            _ => value.GetRawText(),
        };
    }

    private static bool TryCoerceInt(JsonElement value, out int parsed)
    {
        parsed = 0;
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return false;
            case JsonValueKind.Number:
                if (value.TryGetInt32(out parsed))
                {
                    return true;
                }

                return value.TryGetDouble(out var number) && TryTruncate(number, out parsed);
            case JsonValueKind.String:
                return TryCoerceInt(value.GetString(), out parsed);
            default:
                return false;
        }
    }

    private static bool TryCoerceInt(string? value, out int parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        return TryTruncate(number, out parsed);
    }

    private static bool TryTruncate(double number, out int parsed)
    {
        parsed = 0;
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        parsed = (int)truncated;
        return true;
    }
}
