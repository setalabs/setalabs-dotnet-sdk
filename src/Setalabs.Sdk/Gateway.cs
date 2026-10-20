namespace Setalabs.Sdk;

/// <summary>
/// Shared gateway address. Each API is selected by its own uuid.
/// </summary>
public static class Gateway
{
    /// <summary>Default gateway address.</summary>
    public const string Url = "https://gw.setalabs.com";

    /// <summary>Path prefix that comes before the API uuid.</summary>
    public const string ApiPrefix = "/v1/api";
}
