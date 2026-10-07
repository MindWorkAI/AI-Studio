namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The results of the tools the model asked for, as one user turn.
/// </summary>
/// <remarks>
/// All results of one turn belong in a single message. Splitting them across several messages
/// teaches the model to stop asking for more than one tool at a time.
/// </remarks>
public sealed record AnthropicToolResultMessage(IList<AnthropicToolResultContent> Content, string Role = "user") : IMessage<IList<AnthropicToolResultContent>>;