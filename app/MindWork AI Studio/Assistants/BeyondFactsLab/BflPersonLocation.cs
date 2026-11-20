namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The research on one person of a preparation of the BeyondFacts Lab.
/// </summary>
/// <param name="ProjectId">The ID of the preparation.</param>
/// <param name="PersonId">The ID of the person, see BflPersonId.</param>
public sealed record BflPersonLocation(Guid ProjectId, Guid PersonId)
{
    /// <summary>
    /// Gets the research on a person by the name of the person.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="name">The name of the person.</param>
    public static BflPersonLocation Of(Guid projectId, string name) => new(projectId, BflPersonId.Of(name));

    /// <summary>
    /// Gets the key under which the research registers as a running session, beside the research on the topics.
    /// </summary>
    public string Key => $"{this.ProjectId:D}/research/participants/{this.PersonId:D}";
}