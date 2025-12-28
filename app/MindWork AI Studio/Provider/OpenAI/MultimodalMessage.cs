namespace AIStudio.Provider.OpenAI;

/// <summary>
/// A multimodal chat message model that can contain various types of content.
/// </summary>
/// <param name="Content">The list of sub-contents in the message.</param>
/// <param name="Role">The role of the message.</param>
public record MultimodalMessage(List<ISubContent> Content, string Role) : IMessage<List<ISubContent>>
{
    public MultimodalMessage() : this([], string.Empty)
    {
    }
}