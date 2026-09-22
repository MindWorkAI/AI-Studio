using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// One turn of the model, handed back unchanged so the conversation can continue.
/// </summary>
/// <remarks>
/// The content blocks are kept as raw JSON on purpose. A turn can carry text, tool uses, and
/// thinking blocks, and the thinking blocks have to return exactly as they arrived — reading and
/// rebuilding them would risk changing them.
/// </remarks>
public sealed record AnthropicMessage(IList<JsonElement> Content, string Role = "assistant") : IMessage<IList<JsonElement>>;