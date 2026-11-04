using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;

namespace AIStudio.Components;

/// <summary>
/// What the icon of a chat in the workspace list shows: the plain chat icon, a spinner while work
/// on the chat is going on, or how its transcript ended.
/// </summary>
/// <param name="Icon">The icon, unless a spinner turns in its place.</param>
/// <param name="IsSpinning">Whether work on the chat is going on, which a spinner shows.</param>
/// <param name="Color">The color of the icon or the spinner.</param>
/// <param name="IsStatus">Whether the icon tells a status, rather than only marking a chat.</param>
/// <remarks>
/// Every state of a transcript and of an answer has an entry in one of the tables below, so a new
/// state needs nothing but an entry of its own. <c>ChatIndicatorTests</c> fails for a state without
/// one: it would otherwise fall back to the plain icon, and the work would no longer show.
/// </remarks>
internal readonly record struct ChatIndicator(string Icon, bool IsSpinning, Color Color, bool IsStatus)
{
    public static readonly ChatIndicator PLAIN = new(Icons.Material.Outlined.ChatBubbleOutline, IsSpinning: false, Color.Inherit, IsStatus: false);

    private static readonly ChatIndicator ANSWER_GOING_ON = new(string.Empty, IsSpinning: true, Color.Inherit, IsStatus: true);

    private static readonly ChatIndicator TRANSCRIPT_GOING_ON = new(string.Empty, IsSpinning: true, Color.Info, IsStatus: true);

    /// <summary>
    /// What each state of a transcript shows.
    /// </summary>
    /// <remarks>
    /// A transcript shows the spinner in all of its steps, so that one run shows one icon instead
    /// of flickering. How it ended gets an icon of its own, because that stays after the run.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<MediaImportStatus, ChatIndicator> TRANSCRIPT_STATES = new Dictionary<MediaImportStatus, ChatIndicator>
    {
        [MediaImportStatus.QUEUED] = TRANSCRIPT_GOING_ON,
        [MediaImportStatus.RUNNING] = TRANSCRIPT_GOING_ON,
        [MediaImportStatus.CANCELING] = TRANSCRIPT_GOING_ON,
        [MediaImportStatus.SUCCEEDED] = new(Icons.Material.Outlined.TaskAlt, IsSpinning: false, Color.Success, IsStatus: true),
        [MediaImportStatus.WARNING] = new(Icons.Material.Outlined.WarningAmber, IsSpinning: false, Color.Warning, IsStatus: true),
        [MediaImportStatus.FAILED] = new(Icons.Material.Outlined.ErrorOutline, IsSpinning: false, Color.Error, IsStatus: true),
        [MediaImportStatus.CANCELLED] = new(Icons.Material.Outlined.Cancel, IsSpinning: false, Color.Warning, IsStatus: true),
    };

    /// <summary>
    /// What each state of an answer shows.
    /// </summary>
    /// <remarks>
    /// An answer shows the spinner in all of its steps: while it waits in the queue, while it waits
    /// for the model's first words, and while it is being written. Afterwards, the chat shows its
    /// plain icon again, since the answer itself tells how it ended.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<AIJobStatus, ChatIndicator> ANSWER_STATES = new Dictionary<AIJobStatus, ChatIndicator>
    {
        [AIJobStatus.NONE] = PLAIN,
        [AIJobStatus.QUEUED] = ANSWER_GOING_ON,
        [AIJobStatus.WAITING_FOR_REMOTE] = ANSWER_GOING_ON,
        [AIJobStatus.RUNNING] = ANSWER_GOING_ON,
        [AIJobStatus.COMPLETED] = PLAIN,
        [AIJobStatus.CANCELED] = PLAIN,
        [AIJobStatus.FAILED] = PLAIN,
    };

    /// <summary>
    /// What the icon of a chat shows, given the state of its transcript and of its answer.
    /// </summary>
    /// <remarks>
    /// Work going on comes first, then how the last transcript ended. Otherwise, an answer being
    /// written after a finished transcript would hide behind the icon of that transcript.
    /// </remarks>
    /// <param name="transcriptState">The state of the chat's transcript, or null when there is none.</param>
    /// <param name="answerState">The state of the chat's answer, or null when there is none.</param>
    public static ChatIndicator Of(MediaImportStatus? transcriptState, AIJobStatus? answerState)
    {
        var transcript = transcriptState is { } transcriptValue ? TRANSCRIPT_STATES.GetValueOrDefault(transcriptValue, PLAIN) : PLAIN;
        if (transcript.IsSpinning)
            return transcript;

        var answer = answerState is { } answerValue ? ANSWER_STATES.GetValueOrDefault(answerValue, PLAIN) : PLAIN;
        return answer.IsSpinning ? answer : transcript;
    }
}