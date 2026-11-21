using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.RAG;

namespace AIStudio.AIOperations.RetrievalContextValidation;

/// <summary>
/// The passages found for a prompt, for a model to judge whether they help with it.
/// </summary>
/// <param name="SystemPrompt">The system prompt of the chat.</param>
/// <param name="LastUserPrompt">The last prompt of the user.</param>
/// <param name="RetrievalContexts">The passages to judge, prepared for the AI and thus already filtered for prompt injections.</param>
/// <param name="RequiredConfidence">The confidence level the chat requires of every provider which reads its data.</param>
public sealed record RetrievalContextValidationRequest(string SystemPrompt, IContent LastUserPrompt, IReadOnlyList<PreparedRetrievalContext> RetrievalContexts, ConfidenceLevel RequiredConfidence);
