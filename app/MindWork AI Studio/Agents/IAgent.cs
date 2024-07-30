using AIStudio.Chat;

namespace AIStudio.Agents;

public interface IAgent
{
    /// <summary>
    /// Gets the name of the agent.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The provider to use for this agent.
    /// </summary>
    public AIStudio.Settings.Provider? ProviderSettings { get; set; }

    /// <summary>
    /// Processes a chat thread (i.e., context) and returns the updated thread.
    /// </summary>
    /// <param name="chatThread">The chat thread to process. The thread is the context for the agent.</param>
    /// <param name="additionalData">Additional data to use for processing the chat thread.</param>
    /// <returns>The updated chat thread. The last content block of the thread is the agent's response.</returns>
    public Task<ChatThread> ProcessContext(ChatThread chatThread, IDictionary<string, string> additionalData);

    /// <summary>
    /// Processes the input content block and returns the agent's response.
    /// </summary>
    /// <param name="input">The content block to process. It represents the input.</param>
    /// <param name="additionalData">Additional data to use for processing the input.</param>
    /// <returns>The content block representing the agent's response.</returns>
    public Task<ContentBlock> ProcessInput(ContentBlock input, IDictionary<string, string> additionalData);

    /// <summary>
    /// The agent makes a decision based on the input.
    /// </summary>
    /// <param name="input">The content block to process. Should be a question or a request.</param>
    /// <returns>
    /// True if a decision has been made based on the input, false otherwise.
    /// </returns>
    public Task<bool> MadeDecision(ContentBlock input);

    /// <summary>
    /// Retrieves the context of the agent.
    /// </summary>
    /// <returns>The collection of content blocks representing the agent's context. This includes the user's and the other agent's messages.</returns>
    public IReadOnlyCollection<ContentBlock> GetContext();

    /// <summary>
    /// Retrieves the answers from the agent's context.
    /// </summary>
    /// <returns>
    /// The collection of content blocks representing the answers provided by this agent.
    /// </returns>
    public IReadOnlyCollection<ContentBlock> GetAnswers();
}