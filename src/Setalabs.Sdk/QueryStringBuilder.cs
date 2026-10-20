using System.Collections;
using System.Globalization;

namespace Setalabs.Sdk;

/// <summary>
/// Query strings compatible with PHP <c>http_build_query(..., PHP_QUERY_RFC3986)</c>.
/// Parameter order follows <see cref="QueryParameters"/> or dictionary insertion order.
/// </summary>
internal static class QueryStringBuilder
{
    public static string Build(object? query)
    {
        if (query is null)
        {
            return "";
        }

        if (query is string text)
        {
            return text.TrimStart('?');
        }

        var pairs = new List<(string Key, string Value)>();
        switch (query)
        {
            case QueryParameters parameters:
                foreach (var parameter in parameters)
                {
                    AppendTop(pairs, parameter.Key, parameter.Value);
                }

                break;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    AppendTop(pairs, ParameterName(entry.Key), entry.Value);
                }

                break;
            default:
                throw new ArgumentException("Query must be a parameter list, a map, or a string.");
        }

        return Join(pairs);
    }

    private static void AppendTop(List<(string Key, string Value)> pairs, string name, object? value)
    {
        if (name.Length == 0)
        {
            throw new ArgumentException("Query parameter name must not be empty.");
        }

        if (value is null)
        {
            return;
        }

        if (!IsQueryValue(value))
        {
            throw new ArgumentException($"Query parameter \"{name}\" must be a scalar or a list.");
        }

        Append(pairs, name, value);
    }

    private static void Append(List<(string Key, string Value)> pairs, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (TryFormatScalar(value, out var text))
        {
            pairs.Add((key, text));
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                Append(pairs, key + "[" + ParameterName(entry.Key) + "]", entry.Value);
            }

            return;
        }

        if (value is IEnumerable enumerable and not string)
        {
            if (value is byte[])
            {
                throw new ArgumentException($"Query parameter \"{key}\" must be a scalar or a list.");
            }

            var index = 0;
            foreach (var item in enumerable)
            {
                Append(pairs, $"{key}[{index}]", item);
                index++;
            }

            return;
        }

        throw new ArgumentException($"Query parameter \"{key}\" must be a scalar or a list.");
    }

    private static bool IsQueryValue(object value)
    {
        if (TryFormatScalar(value, out _))
        {
            return true;
        }

        if (value is IDictionary)
        {
            return true;
        }

        return value is IEnumerable and not string and not byte[];
    }

    private static bool TryFormatScalar(object value, out string text)
    {
        switch (value)
        {
            case string scalar:
                text = scalar;
                return true;
            case bool flag:
                text = flag ? "1" : "0";
                return true;
            case char character:
                text = character.ToString();
                return true;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                text = Convert.ToString(value, CultureInfo.InvariantCulture)!;
                return true;
            case float number:
                text = PhpFloat.Format(number);
                return true;
            case double number:
                text = PhpFloat.Format(number);
                return true;
            case decimal number:
                text = number.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                text = "";
                return false;
        }
    }

    private static string ParameterName(object? key)
    {
        switch (key)
        {
            case string name when name.Length > 0:
                return name;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                return Convert.ToString(key, CultureInfo.InvariantCulture)!;
            default:
                throw new ArgumentException("Query parameter name must not be empty.");
        }
    }

    private static string Join(List<(string Key, string Value)> pairs)
    {
        if (pairs.Count == 0)
        {
            return "";
        }

        return string.Join("&", pairs.Select(pair => $"{EscapeRfc3986(pair.Key)}={EscapeRfc3986(pair.Value)}"));
    }

    internal static string EscapeRfc3986(string value)
    {
        var escaped = Uri.EscapeDataString(value);
        return escaped
            .Replace("!", "%21", StringComparison.Ordinal)
            .Replace("'", "%27", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal)
            .Replace("*", "%2A", StringComparison.Ordinal);
    }
}
