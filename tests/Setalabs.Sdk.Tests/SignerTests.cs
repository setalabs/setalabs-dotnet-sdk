namespace Setalabs.Sdk.Tests;

public class SignerTests
{
    [Fact]
    public void Sign_uses_fixed_timestamp_and_gateway_message()
    {
        const string apiKey = "key-1";
        const string secret = "secret-1";
        const string timestamp = "1700000000";
        const string pathWithQuery = "/v1/api/uuid/items?q=1";
        const string expected = "abce2f73336f4fcc3b493ad44fbc622d7f0c5375d67e5f1f0dae414fa9dbcf80";

        var signature = Signer.Sign(apiKey, secret, timestamp, pathWithQuery);

        Assert.Equal(expected, signature);
        Assert.Equal(64, signature.Length);
        Assert.Equal(signature.ToLowerInvariant(), signature);
    }
}
