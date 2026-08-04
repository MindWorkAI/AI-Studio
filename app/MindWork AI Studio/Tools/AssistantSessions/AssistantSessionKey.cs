namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Identifies one logical assistant session slot.
/// </summary>
public readonly record struct AssistantSessionKey
{
    /// <summary>
    /// Initializes a new assistant session key.
    /// </summary>
    /// <param name="component">The application component the assistant belongs to.</param>
    /// <param name="instanceId">The assistant-specific instance ID, such as a component type or plugin ID.</param>
    public AssistantSessionKey(Components component, string instanceId)
    {
        this.Component = component;
        this.InstanceId = instanceId;
    }

    /// <summary>
    /// Gets the application component the assistant belongs to.
    /// </summary>
    public Components Component { get; init; }

    /// <summary>
    /// Gets the assistant-specific instance ID, such as a component type or plugin ID.
    /// </summary>
    public string InstanceId { get; init; }

    /// <summary>
    /// Converts the key into a compact diagnostic string.
    /// </summary>
    /// <returns>The component and instance ID joined by a colon.</returns>
    public override string ToString() => $"{this.Component}:{this.InstanceId}";
}