using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Runs strict structured LLM stages with exactly one same-context repair attempt.
/// </summary>
internal interface IStructuredLlmStageRunner
{
    /// <summary>
    /// Runs one structured model stage.
    /// </summary>
    /// <typeparam name="T">The strict response type.</typeparam>
    /// <param name="provider">The selected provider configuration.</param>
    /// <param name="profile">The selected user profile.</param>
    /// <param name="systemContract">The stage-specific system contract.</param>
    /// <param name="prompt">The user prompt containing stage inputs.</param>
    /// <param name="attachments">The first-turn attachments.</param>
    /// <param name="stage">The build stage.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="buildId">The build identifier.</param>
    /// <param name="validate">Strict semantic validation for a parsed response.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The validated stage result.</returns>
    Task<StructuredLlmStageResult<T>> RunAsync<T>(
        Settings.Provider provider,
        Profile profile,
        string systemContract,
        string prompt,
        IReadOnlyList<FileAttachment> attachments,
        VisualBriefingBuildStage stage,
        Guid operationId,
        Guid buildId,
        Func<T, VisualBriefingContractIssue?> validate,
        CancellationToken token)
        where T : class;
}