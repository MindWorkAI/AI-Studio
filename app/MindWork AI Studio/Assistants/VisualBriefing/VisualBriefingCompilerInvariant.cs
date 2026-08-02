namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Guards parts compiled by AI Studio after the model-controlled contracts have been validated.
/// </summary>
internal static class VisualBriefingCompilerInvariant
{
    private const string USER_MESSAGE = "AI Studio could not assemble this briefing because its own compiler produced an invalid part. This is a defect in AI Studio, not in the model response.";

    /// <summary>
    /// Fails the build when compiled parts violate the artifact contract.
    /// </summary>
    /// <param name="stage">The stage running the compilation.</param>
    /// <param name="compilerIssue">The compiler issue, or an empty string when the parts are valid.</param>
    /// <exception cref="VisualBriefingBuildException">Thrown when the compiled parts are invalid.</exception>
    internal static void Guard(VisualBriefingBuildStage stage, string compilerIssue)
    {
        if (string.IsNullOrEmpty(compilerIssue))
            return;

        throw new VisualBriefingBuildException(
            VisualBriefingFailureCode.COMPILER_INVARIANT_VIOLATED,
            stage,
            USER_MESSAGE,
            $"Stage={stage}; CompilerIssue={compilerIssue}");
    }

    /// <summary>
    /// Runs a compilation and translates structural failures into a compiler invariant failure.
    /// </summary>
    /// <typeparam name="T">The compilation result type.</typeparam>
    /// <param name="stage">The stage running the compilation.</param>
    /// <param name="compile">The compilation to run.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="VisualBriefingBuildException">Thrown when the compilation fails structurally.</exception>
    internal static T Guard<T>(VisualBriefingBuildStage stage, Func<T> compile)
    {
        try
        {
            return compile();
        }
        catch (InvalidDataException exception)
        {
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.COMPILER_INVARIANT_VIOLATED,
                stage,
                USER_MESSAGE,
                $"Stage={stage}; CompilerIssue={exception.Message}");
        }
    }
}