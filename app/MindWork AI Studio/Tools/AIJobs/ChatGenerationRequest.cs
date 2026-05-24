using AIStudio.Chat;

namespace AIStudio.Tools.AIJobs;

public sealed record ChatGenerationRequest
{
    public required ChatThread ChatThread { get; set; }

    public required ContentText AIText { get; init; }

    public IContent? LastUserPrompt { get; init; }

    public required AIStudio.Settings.Provider ProviderSettings { get; init; }

    public Guid? ParentJobId { get; init; }

    public int Priority { get; init; }

    public bool IsForeground { get; init; } = true;
}