namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where a research belongs: to a preparation, as one of the topics of its research.
/// </summary>
/// <param name="ProjectId">The ID of the preparation.</param>
/// <param name="Topic">The topic of the research.</param>
public sealed record BflResearchLocation(Guid ProjectId, BflResearchTopic Topic)
{
    /// <summary>
    /// Gets a key which tells the research apart from every other one, e.g., for a running request.
    /// </summary>
    public string Key => $"{this.ProjectId:D}/research/{this.Topic.FileStem}";
}