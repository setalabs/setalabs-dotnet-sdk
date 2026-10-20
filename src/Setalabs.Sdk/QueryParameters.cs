using System.Collections;

namespace Setalabs.Sdk;

/// <summary>
/// Ordered query parameters. Order is preserved in the query string and in the signature.
/// </summary>
public sealed class QueryParameters : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly List<KeyValuePair<string, object?>> _items = new();

    /// <summary>Adds a parameter. A null value is omitted. Nested lists keep their indexes.</summary>
    public void Add(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _items.Add(new KeyValuePair<string, object?>(name, value));
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
