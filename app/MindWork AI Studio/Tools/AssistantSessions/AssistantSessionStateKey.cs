namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Identifies a typed assistant session state value.
/// </summary>
/// <typeparam name="T">The value type stored for this key.</typeparam>
public readonly record struct AssistantSessionStateKey<T>
{
    /// <summary>
    /// Initializes a new assistant session state key.
    /// </summary>
    /// <param name="name">The stable dictionary name used in assistant session snapshots.</param>
    public AssistantSessionStateKey(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Gets the stable dictionary name used in assistant session snapshots.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Returns the stable dictionary name.
    /// </summary>
    /// <returns>The stable dictionary name.</returns>
    public override string ToString() => this.Name;
}