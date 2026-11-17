namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where a result belongs: to a preparation, as one of its kinds of results.
/// </summary>
/// <param name="ProjectId">The ID of the preparation.</param>
/// <param name="Kind">The kind of result.</param>
public sealed record BflResultLocation(Guid ProjectId, BflResultKind Kind)
{
    /// <summary>
    /// Gets a key which tells the result apart from every other one, e.g., for a running request.
    /// </summary>
    public string Key => $"{this.ProjectId:D}/results/{this.Kind.FileName}";
}