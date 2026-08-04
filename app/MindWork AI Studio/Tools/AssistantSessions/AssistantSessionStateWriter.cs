using System.Text;

namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Collects typed assistant session state values for a snapshot.
/// </summary>
public sealed class AssistantSessionStateWriter
{
    /// <summary>
    /// Stores captured fields by their stable dictionary names.
    /// </summary>
    private readonly Dictionary<string, IAssistantSessionSnapshotField> fields = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores a typed state value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="value">The captured value.</param>
    public void Set<T>(AssistantSessionStateKey<T> key, T value)
    {
        this.fields[key.Name] = new AssistantSessionSnapshotField<T>(value);
    }

    /// <summary>
    /// Stores a list copy.
    /// </summary>
    /// <typeparam name="T">The list item type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="values">The values to copy.</param>
    public void SetList<T>(AssistantSessionStateKey<List<T>> key, IEnumerable<T> values)
    {
        this.Set(key, values.ToList());
    }

    /// <summary>
    /// Stores a hash set copy.
    /// </summary>
    /// <typeparam name="T">The set item type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="values">The values to copy.</param>
    public void SetHashSet<T>(AssistantSessionStateKey<HashSet<T>> key, IEnumerable<T> values)
    {
        this.Set(key, values.ToHashSet());
    }

    /// <summary>
    /// Stores a dictionary copy.
    /// </summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="values">The values to copy.</param>
    public void SetDictionary<TKey, TValue>(AssistantSessionStateKey<Dictionary<TKey, TValue>> key, IDictionary<TKey, TValue> values) where TKey : notnull
    {
        this.Set(key, new Dictionary<TKey, TValue>(values));
    }

    /// <summary>
    /// Stores the current text from a string builder.
    /// </summary>
    /// <param name="key">The typed state key.</param>
    /// <param name="value">The string builder to read.</param>
    public void SetStringBuilder(AssistantSessionStateKey<string> key, StringBuilder value)
    {
        this.Set(key, value.ToString());
    }

    /// <summary>
    /// Returns the captured fields as a dictionary.
    /// </summary>
    /// <returns>A copied dictionary containing the captured fields.</returns>
    public Dictionary<string, IAssistantSessionSnapshotField> ToDictionary()
    {
        return new(this.fields, StringComparer.Ordinal);
    }
}