namespace Setalabs.Sdk.Tests;

public class QueryTests
{
    [Fact]
    public void Build_matches_php_http_build_query()
    {
        var query = QueryStringBuilder.Build(new QueryParameters
        {
            { "b", "2" },
            { "a", "1" },
            { "q", "x y" },
            { "flag", true },
            { "off", false },
            { "n", null },
            { "ids", new object?[] { 1, null, "x" } },
            { "f", new Dictionary<string, object> { ["x"] = "a/b" } },
            { "whole", 1.0 },
            { "frac", 1.5 },
            { "big", 1e16 },
        });

        Assert.Equal(
            "b=2&a=1&q=x%20y&flag=1&off=0&ids%5B0%5D=1&ids%5B2%5D=x" +
            "&f%5Bx%5D=a%2Fb&whole=1&frac=1.5&big=1.0E%2B16",
            query);
    }

    [Fact]
    public void Build_keeps_dictionary_insertion_order()
    {
        var query = QueryStringBuilder.Build(new Dictionary<string, object?>
        {
            ["b"] = "2",
            ["a"] = "1",
        });

        Assert.Equal("b=2&a=1", query);
    }

    [Fact]
    public void String_query_keeps_raw_text()
    {
        Assert.Equal("language=en&q=a b", QueryStringBuilder.Build("?language=en&q=a b"));
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(1e-5, "1.0E-5")]
    [InlineData(1e-7, "1.0E-7")]
    [InlineData(1.23e-8, "1.23E-8")]
    [InlineData(1.1, "1.1")]
    [InlineData(10.0, "10")]
    [InlineData(1e14, "1.0E+14")]
    [InlineData(1e16, "1.0E+16")]
    public void Float_string_matches_php(double value, string expected)
    {
        Assert.Equal(expected, PhpFloat.Format(value));
    }

    [Fact]
    public void Float_string_matches_php_special_values()
    {
        Assert.Equal("-0", PhpFloat.Format(double.NegativeZero));
        Assert.Equal("0.33333333333333", PhpFloat.Format(1d / 3d));
        Assert.Equal("INF", PhpFloat.Format(double.PositiveInfinity));
        Assert.Equal("-INF", PhpFloat.Format(double.NegativeInfinity));
        Assert.Equal("NAN", PhpFloat.Format(double.NaN));
    }

    [Fact]
    public void Escape_matches_rawurlencode_for_reserved_marks()
    {
        Assert.Equal("a%20b%2Fc~%21%27%28%29%2A", QueryStringBuilder.EscapeRfc3986("a b/c~!'()*"));
    }
}
