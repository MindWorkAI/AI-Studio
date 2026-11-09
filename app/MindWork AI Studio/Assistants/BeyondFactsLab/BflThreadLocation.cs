namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where the BeyondFacts Lab keeps a chat, e.g., in a step of a preparation.
/// </summary>
/// <remarks>
/// Each place holds one chat at most, and the ID of the chat follows from the place, see BflThreadId. Two places of
/// the same kind with the same values are equal, so a place serves as a key.
/// </remarks>
public abstract record BflThreadLocation
{
    /// <summary>
    /// Gets the ID of the chat, which follows from its place.
    /// </summary>
    public abstract Guid ChatId { get; }

    /// <summary>
    /// Gets a key which no other place has, e.g., for the registry of running assistant sessions and for the log.
    /// </summary>
    public abstract string Key { get; }

    /// <summary>
    /// Gets the name the chat stores, which tells people who look into its file where it belongs.
    /// </summary>
    public abstract string ChatName { get; }
}