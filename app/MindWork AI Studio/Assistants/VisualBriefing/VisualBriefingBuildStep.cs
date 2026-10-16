namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Pairs one independently tracked pipeline operation with the durable stage it reports as.
/// </summary>
/// <param name="stage">The durable stage.</param>
/// <param name="action">The stage action.</param>
internal sealed class VisualBriefingBuildStep(
    VisualBriefingBuildStage stage,
    Func<CancellationToken, Task> action)
{
    /// <summary>
    /// Gets the durable stage represented by the step.
    /// </summary>
    public VisualBriefingBuildStage Stage { get; } = stage;

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that completes when the step finishes.</returns>
    public Task ExecuteAsync(CancellationToken token) => action(token);
}