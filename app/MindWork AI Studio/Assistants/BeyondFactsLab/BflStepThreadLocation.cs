namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The place of the chat of a step of a preparation.
/// </summary>
/// <param name="ProjectId">The ID of the preparation.</param>
/// <param name="Step">The step.</param>
public sealed record BflStepThreadLocation(Guid ProjectId, BflStep Step) : BflThreadLocation
{
    /// <inheritdoc />
    public override Guid ChatId => BflThreadId.Of(this.ProjectId, this.Step);

    /// <inheritdoc />
    public override string Key => $"{this.ProjectId:D}/{this.Step.FolderName}";

    /// <inheritdoc />
    public override string ChatName => this.Step.FolderName;
}