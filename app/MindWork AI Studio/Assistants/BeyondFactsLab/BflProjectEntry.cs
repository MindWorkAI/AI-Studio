namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A preparation in the list of the BeyondFacts Lab, also when it cannot be opened.
/// </summary>
/// <param name="ProjectId">The ID of the preparation.</param>
/// <param name="Name">The name to show; empty when even the name cannot be read.</param>
/// <param name="ModifiedAtUtc">When the preparation was changed last.</param>
/// <param name="Status">Whether the preparation can be opened.</param>
/// <param name="Project">The preparation, when it can be opened.</param>
public sealed record BflProjectEntry(Guid ProjectId, string Name, DateTimeOffset ModifiedAtUtc, BflProjectLoadStatus Status, BflProject? Project)
{
    /// <summary>
    /// Gets whether the preparation can be opened.
    /// </summary>
    public bool IsAvailable => this.Status is BflProjectLoadStatus.AVAILABLE && this.Project is not null;

    /// <summary>
    /// Creates the entry of a preparation which can be opened.
    /// </summary>
    /// <param name="project">The preparation.</param>
    public static BflProjectEntry FromProject(BflProject project) => new(project.ProjectId, project.Name, project.ModifiedAtUtc, BflProjectLoadStatus.AVAILABLE, project);
}