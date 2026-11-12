namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The results of the tools the model asked for, as one user turn.
/// </summary>
/// <remarks>
/// All results of one turn belong in a single message. Splitting them across several messages
/// teaches the model to stop asking for more than one tool at a time.
/// </remarks>
public sealed record AnthropicToolResultMessage(IList<AnthropicToolResultContent> Content, string Role = "user") : IMessage<IList<AnthropicToolResultContent>>
{
    //
    // The properties of the parameters, declared again to be settable, as the messages demand it. Keep them in
    // the order of the parameters, which is the order they get written in:
    //
    public IList<AnthropicToolResultContent> Content { get; set; } = Content;
    public string Role { get; set; } = Role;
}