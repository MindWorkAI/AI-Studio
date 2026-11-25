namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The place of the conversation in which the lab tells whether it fits a task and how to work with it.
/// </summary>
/// <remarks>
/// The lab keeps one such conversation, apart from all preparations: it comes before the first one.
/// </remarks>
public sealed record BflOnboardingThreadLocation : BflThreadLocation
{
    /// <summary>
    /// The only place of this kind.
    /// </summary>
    public static readonly BflOnboardingThreadLocation INSTANCE = new();

    /// <summary>
    /// The name of the conversation, the same on every computer.
    /// </summary>
    private const string NAME = "onboarding";

    private BflOnboardingThreadLocation()
    {
    }

    /// <inheritdoc />
    public override Guid ChatId => BflThreadId.ONBOARDING;

    /// <inheritdoc />
    public override string Key => NAME;

    /// <inheritdoc />
    public override string ChatName => NAME;
}