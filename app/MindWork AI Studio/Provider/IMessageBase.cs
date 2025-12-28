namespace AIStudio.Provider;

/// <summary>
/// The none-generic base interface for messages exchanged with AI models.
/// </summary>
public interface IMessageBase
{
    /// <summary>
    /// Gets the role of the entity sending or receiving the message.
    /// This property typically identifies whether the entity is acting
    /// as a user, assistant, or system in the context of the interaction.
    /// </summary>
    public string Role { get; init; }
}