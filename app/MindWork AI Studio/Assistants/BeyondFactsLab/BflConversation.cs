using AIStudio.Chat;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The chat of a step of the BeyondFacts Lab, as BflConversationService keeps it while the app runs.
/// </summary>
/// <remarks>
/// The service keeps it beyond the page, so that an answer goes on while the person looks at another page, and the
/// page shows it again when it comes back.
/// </remarks>
public sealed class BflConversation
{
    /// <summary>
    /// Gets the step of the preparation the chat belongs to.
    /// </summary>
    public required BflThreadLocation Location { get; init; }

    /// <summary>
    /// Gets or sets the chat, or null when this version cannot open the stored one.
    /// </summary>
    public ChatThread? Thread { get; set; }

    /// <summary>
    /// Gets or sets whether the model answers right now.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets whether the chat can be shown and continued.
    /// </summary>
    public bool IsAvailable => this.Thread is not null;

    /// <summary>
    /// Gets whether the chat has a message the person sees.
    /// </summary>
    public bool HasVisibleMessages => this.Thread?.Blocks.Any(block => !block.HideFromUser) ?? false;
}