using System.Security.Cryptography;
using System.Text;

namespace Setalabs.Sdk;

/// <summary>
/// Request signature expected by the gateway.
/// </summary>
/// <remarks>
/// <c>X-Api-Sign</c> is HMAC-SHA256 of <c>apiKey + secret + timestamp + pathWithQuery</c>,
/// keyed by the secret, as lowercase hex. <c>pathWithQuery</c> is the decoded request
/// path plus the raw query string, for example <c>/v1/api/{uuid}/items?page=1</c>.
/// </remarks>
public static class Signer
{
    /// <summary>Returns the <c>X-Api-Sign</c> value for one request.</summary>
    /// <param name="apiKey">API key that belongs to the target API.</param>
    /// <param name="secret">Key secret. It is the HMAC key and part of the signed text. It is never sent.</param>
    /// <param name="timestamp">Unix time in seconds, the same value sent in <c>X-Api-Timestamp</c>.</param>
    /// <param name="pathWithQuery">Decoded gateway path and raw query string.</param>
    public static string Sign(string apiKey, string secret, string timestamp, string pathWithQuery)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(pathWithQuery);

        var key = Encoding.UTF8.GetBytes(secret);
        var message = Encoding.UTF8.GetBytes(string.Concat(apiKey, secret, timestamp, pathWithQuery));
        return Convert.ToHexString(HMACSHA256.HashData(key, message)).ToLowerInvariant();
    }
}
