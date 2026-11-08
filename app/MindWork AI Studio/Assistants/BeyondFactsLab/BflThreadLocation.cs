namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where the BeyondFacts Lab keeps a chat: the step of a preparation it belongs to.
/// </summary>
/// <param name="ProjectId">The ID of the preparation.</param>
/// <param name="Step">The step.</param>
public readonly record struct BflThreadLocation(Guid ProjectId, BflStep Step)
{
    /// <summary>
    /// Gets the ID of the chat, which follows from its place, see BflThreadId.
    /// </summary>
    public Guid ChatId => BflThreadId.Of(this.ProjectId, this.Step);
}