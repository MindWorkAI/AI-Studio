namespace AIStudio.Assistants;

/// <summary>
/// What an assistant asks a model to do with each text of a document, as the form stands when the run starts.
/// </summary>
/// <param name="SystemPrompt">The system prompt of every request, in English.</param>
/// <param name="Instructions">What the model does with each text, e.g., "Translate each text into German.", in English.</param>
/// <param name="LanguageTag">The IETF tag of the language of the new texts, e.g., "de-DE"; empty to leave the marks of the language as they are.</param>
/// <param name="Label">What sets the new document apart, e.g., its target language, for the user and for the suggested file name.</param>
public sealed record DocumentRequest(string SystemPrompt, string Instructions, string LanguageTag, string Label);