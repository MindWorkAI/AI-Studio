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
    /// <param name="isKeptOutOfDrafts">Whether a draft leaves the value out, see <see cref="IsKeptOutOfDrafts"/>.</param>
    public AssistantSessionStateKey(string name, bool isKeptOutOfDrafts = false)
    {
        this.Name = name;
        this.IsKeptOutOfDrafts = isKeptOutOfDrafts;
    }

    /// <summary>
    /// Gets the stable dictionary name used in assistant session snapshots.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether an assistant restores the value from a run only, never from a draft.
    /// </summary>
    /// <remarks>
    /// This is meant for flags of work in progress, such as reading a web page. An assistant stores a draft
    /// when the user leaves it while no run goes on, and work outside of a run ends with the component which
    /// did it. A flag restored from a draft would therefore stay set and could keep the form locked. Left
    /// out, it keeps the value which a freshly opened assistant starts with.
    /// </remarks>
    public bool IsKeptOutOfDrafts { get; }

    /// <summary>
    /// Returns the stable dictionary name.
    /// </summary>
    /// <returns>The stable dictionary name.</returns>
    public override string ToString() => this.Name;
}