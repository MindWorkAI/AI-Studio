namespace AIStudio.Provider.OpenAI;

/// <summary>
/// A multimodal chat message model that can contain various types of content.
/// </summary>
/// <param name="Content">The list of sub-contents in the message.</param>
/// <param name="Role">The role of the message.</param>
public record MultimodalMessage(List<ISubContent> Content, string Role) : IMessage<List<ISubContent>>
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public List<ISubContent> Content { get; set; } = Content;
    public string Role { get; set; } = Role;

    public MultimodalMessage() : this([], string.Empty)
    {
    }
}