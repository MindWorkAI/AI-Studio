// ReSharper disable MemberCanBePrivate.Global
using System.Collections;

namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Stores one typed value in an assistant session snapshot.
/// </summary>
/// <typeparam name="T">The captured value type.</typeparam>
public sealed record AssistantSessionSnapshotField<T> : IAssistantSessionSnapshotField
{
    /// <summary>
    /// Initializes a new typed snapshot field.
    /// </summary>
    /// <param name="value">The captured value.</param>
    public AssistantSessionSnapshotField(T value)
    {
        this.Value = value;
    }

    /// <summary>
    /// Gets the captured value.
    /// </summary>
    public T Value { get; }

    /// <inheritdoc />
    public Type ValueType => typeof(T);

    /// <inheritdoc />
    public bool TryRead<TValue>(out TValue? value)
    {
        if (this.Value is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        if (this.Value is null && default(TValue) is null)
        {
            value = default;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool HasSameValueAs(IAssistantSessionSnapshotField other) => other is AssistantSessionSnapshotField<T> otherField && AreSame(this.Value, otherField.Value);

    private static bool AreSame(object? value, object? otherValue)
    {
        if (ReferenceEquals(value, otherValue))
            return true;

        if (value is null || otherValue is null)
            return false;

        // Nobody can change a string or a copied value type behind our back:
        if (value is string || value.GetType().IsValueType)
            return value.Equals(otherValue);

        //
        // The writer copies lists, sets, and dictionaries for every snapshot, so these never share a
        // reference. Their items, though, are those of the assistant. Any other object may change
        // without a new reference, e.g., a record holding a list, so only its reference tells:
        //
        if (value is IDictionary dictionary && otherValue is IDictionary otherDictionary)
            return dictionary.Count == otherDictionary.Count && dictionary.Keys.Cast<object>().All(key => otherDictionary.Contains(key) && AreSame(dictionary[key], otherDictionary[key]));

        if (value is IEnumerable items && otherValue is IEnumerable otherItems)
        {
            var itemList = items.Cast<object?>().ToList();
            var otherItemList = otherItems.Cast<object?>().ToList();
            return itemList.Count == otherItemList.Count && itemList.Zip(otherItemList).All(pair => AreSame(pair.First, pair.Second));
        }

        return false;
    }
}