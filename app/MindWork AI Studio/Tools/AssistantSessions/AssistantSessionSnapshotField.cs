// ReSharper disable MemberCanBePrivate.Global
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
}