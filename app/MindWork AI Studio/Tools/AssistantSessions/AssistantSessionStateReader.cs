using System.Text;

namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Restores typed assistant session state values from a snapshot.
/// </summary>
/// <param name="fields">The captured snapshot fields.</param>
/// <param name="assistantTitle">The user-visible assistant title.</param>
public sealed class AssistantSessionStateReader(IReadOnlyDictionary<string, IAssistantSessionSnapshotField> fields, string assistantTitle)
{
    private static readonly ILogger<AssistantSessionStateReader> LOG = Program.LOGGER_FACTORY.CreateLogger<AssistantSessionStateReader>();

    /// <summary>
    /// Restores a typed value when it exists in the snapshot.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="apply">The action that applies the restored value.</param>
    public void Restore<T>(AssistantSessionStateKey<T> key, Action<T> apply)
    {
        if (this.TryRead(key, out var value))
            apply(value!);
    }

    /// <summary>
    /// Restores a list into an existing list instance.
    /// </summary>
    /// <typeparam name="T">The list item type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="target">The existing list to update.</param>
    public void RestoreList<T>(AssistantSessionStateKey<List<T>> key, List<T> target)
    {
        this.Restore(key, values =>
        {
            target.Clear();
            target.AddRange(values);
        });
    }

    /// <summary>
    /// Restores a hash set into an existing hash set instance.
    /// </summary>
    /// <typeparam name="T">The set item type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="target">The existing hash set to update.</param>
    public void RestoreHashSet<T>(AssistantSessionStateKey<HashSet<T>> key, HashSet<T> target)
    {
        this.Restore(key, values =>
        {
            target.Clear();
            target.UnionWith(values);
        });
    }

    /// <summary>
    /// Restores a dictionary into an existing dictionary instance.
    /// </summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="target">The existing dictionary to update.</param>
    public void RestoreDictionary<TKey, TValue>(AssistantSessionStateKey<Dictionary<TKey, TValue>> key, Dictionary<TKey, TValue> target) where TKey : notnull
    {
        this.Restore(key, values =>
        {
            target.Clear();
            foreach (var (itemKey, itemValue) in values)
                target[itemKey] = itemValue;
        });
    }

    /// <summary>
    /// Restores text into an existing string builder instance.
    /// </summary>
    /// <param name="key">The typed state key.</param>
    /// <param name="target">The existing string builder to update.</param>
    public void RestoreStringBuilder(AssistantSessionStateKey<string> key, StringBuilder target)
    {
        this.Restore(key, value =>
        {
            target.Clear();
            target.Append(value);
        });
    }

    /// <summary>
    /// Tries to read a typed value from the snapshot.
    /// </summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="key">The typed state key.</param>
    /// <param name="value">The restored value when reading succeeds.</param>
    /// <returns><c>true</c> when a value exists and matches the requested type; otherwise, <c>false</c>.</returns>
    private bool TryRead<T>(AssistantSessionStateKey<T> key, out T? value)
    {
        value = default;
        if (!fields.TryGetValue(key.Name, out var field))
            return false;

        if (field.TryRead(out value))
            return true;

        LOG.LogWarning(
            "Could not restore assistant session field '{FieldStateKey}' for assistant '{AssistantTitle}'. ExpectedType='{ExpectedType}', CapturedValueType='{CapturedValueType}'. The current value is kept.",
            key.Name,
            assistantTitle,
            typeof(T).FullName,
            field.ValueType.FullName);
        return false;
    }
}