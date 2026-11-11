namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Standard text-based chat message model.
/// </summary>
/// <param name="Content">The text content of the message.</param>
/// <param name="Role">The role of the message.</param>
public record TextMessage(string Content, string Role) : IMessage<string>
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public string Content { get; set; } = Content;
    public string Role { get; set; } = Role;

    public TextMessage() : this(string.Empty, string.Empty)
    {
    }
}