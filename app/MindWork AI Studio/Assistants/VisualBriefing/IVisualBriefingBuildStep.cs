namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Represents one independently tracked step in the visual briefing build pipeline.
/// </summary>
internal interface IVisualBriefingBuildStep
{
    /// <summary>
    /// Gets the durable stage represented by the step.
    /// </summary>
    VisualBriefingBuildStage Stage { get; }

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that completes when the step finishes.</returns>
    Task ExecuteAsync(CancellationToken token);
}

/// <summary>
/// Adapts a focused asynchronous operation to the build-step abstraction.
/// </summary>
/// <param name="stage">The durable stage.</param>
/// <param name="action">The stage action.</param>
internal sealed class VisualBriefingBuildStep(
    VisualBriefingBuildStage stage,
    Func<CancellationToken, Task> action) : IVisualBriefingBuildStep
{
    /// <inheritdoc />
    public VisualBriefingBuildStage Stage { get; } = stage;

    /// <inheritdoc />
    public Task ExecuteAsync(CancellationToken token) => action(token);
}