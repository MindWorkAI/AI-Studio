namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Represents a tool used by the AI model.
/// </summary>
/// <remarks>
/// Right now, only our OpenAI provider is using tools. Thus, this class is located in the
/// OpenAI namespace. In the future, when other providers also support tools, this class can
/// be moved into the provider namespace.
/// </remarks>
/// <param name="Type">The type of the tool.</param>
public record Tool(string Type);