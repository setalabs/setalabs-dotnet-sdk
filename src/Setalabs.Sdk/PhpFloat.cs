using System.Globalization;
using System.Text;

namespace Setalabs.Sdk;

/// <summary>
/// String cast of a float, using PHP's default <c>precision=14</c> (<c>zend_gcvt</c>).
/// The gateway signs the query text, so the format has to match the other Setalabs SDKs.
/// </summary>
internal static class PhpFloat
{
    private const int Precision = 14;

    public static string Format(double value)
    {
        if (double.IsNaN(value))
        {
            return "NAN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "INF";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-INF";
        }

        var negative = double.IsNegative(value);
        if (value == 0d)
        {
            return negative ? "-0" : "0";
        }

        var (digits, decpt) = SignificantDigits(Math.Abs(value), Precision);
        return Gcvt(negative, digits, decpt, Precision);
    }

    private static (string Digits, int Decpt) SignificantDigits(double value, int precision)
    {
        var text = value.ToString("e" + (precision - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var separator = text.IndexOf('e');
        if (separator < 0)
        {
            separator = text.IndexOf('E');
        }

        var mantissa = text[..separator];
        var exponent = int.Parse(text[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.');
        var whole = dot < 0 ? mantissa : mantissa[..dot];
        var fraction = dot < 0 ? "" : mantissa[(dot + 1)..];
        var digits = (whole + fraction).TrimEnd('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }

        return (digits, exponent + 1);
    }

    private static string Gcvt(bool negative, string digits, int decpt, int ndigit)
    {
        var result = new StringBuilder();
        if (negative)
        {
            result.Append('-');
        }

        if ((decpt >= 0 && decpt > ndigit) || decpt < -3)
        {
            var exponent = decpt - 1;
            result.Append(digits[0]);
            result.Append('.');
            result.Append(digits.Length > 1 ? digits[1..] : "0");
            result.Append('E');
            result.Append(exponent >= 0 ? '+' : '-');
            result.Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture));
            return result.ToString();
        }

        if (decpt <= 0)
        {
            result.Append("0.");
            result.Append('0', -decpt);
            result.Append(digits);
            return result.ToString();
        }

        if (digits.Length <= decpt)
        {
            result.Append(digits);
            result.Append('0', decpt - digits.Length);
            return result.ToString();
        }

        result.Append(digits[..decpt]);
        result.Append('.');
        result.Append(digits[decpt..]);
        return result.ToString();
    }
}
