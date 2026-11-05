using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;

namespace AIStudio.Components;

/// <summary>
/// What the icon of a chat in the workspace list shows: the plain chat icon, a spinner while work
/// on the chat is going on, how an answer ended which the user has not seen yet, or how its
/// transcript ended.
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
    /// <summary>
    /// A small mark in the bottom-left corner of the icon, which tells the state of the chat while the
    /// icon itself tells something else, such as that a search found the chat.
    /// </summary>
    public ChatIndicatorBadge Badge { get; init; }

    /// <summary>
    /// Whether the search found words in the chat, which the list shows with the sign of the search, see
    /// SearchFoundIcon, rather than with <see cref="Icon"/>.
    /// </summary>
    public bool IsFoundBySearch { get; init; }

    public static readonly ChatIndicator PLAIN = new(Icons.Material.Outlined.ChatBubbleOutline, IsSpinning: false, Color.Inherit, IsStatus: false);

    /// <summary>
    /// A chat whose messages, attached files, or sources hold words of the search: the magnifier on a disc
    /// of the search's yellow. Its name may hold the words as well, which its marked words show.
    /// </summary>
    public static readonly ChatIndicator FOUND_IN_CHAT = new(Icons.Material.Outlined.Search, IsSpinning: false, Color.Inherit, IsStatus: true) { IsFoundBySearch = true };

    /// <summary>
    /// What the icon of a chat shows whose messages, attached files, or sources hold words of the search,
    /// given what it would show otherwise.
    /// </summary>
    /// <remarks>
    /// The search comes first while it runs: it is what the user looks for right now. What needs their
    /// attention stays visible as the badge, though: work going on, and work which failed. Everything else,
    /// such as an unread answer, shows again once the search ends.
    /// </remarks>
    /// <param name="state">What the icon of the chat would show without the search.</param>
    public static ChatIndicator FoundInChat(ChatIndicator state) => FOUND_IN_CHAT with
    {
        Badge = state switch
        {
            { IsSpinning: true } => ChatIndicatorBadge.WORK_GOING_ON,
            { Color: Color.Error } => ChatIndicatorBadge.FAILED,
            _ => ChatIndicatorBadge.NONE,
        },
    };

    private static readonly ChatIndicator ANSWER_GOING_ON = new(string.Empty, IsSpinning: true, Color.Inherit, IsStatus: true);

    private static readonly ChatIndicator TRANSCRIPT_GOING_ON = new(string.Empty, IsSpinning: true, Color.Info, IsStatus: true);

    /// <summary>
    /// An answer which was written while the chat was not open, and which the user has not read yet.
    /// </summary>
    /// <remarks>
    /// In the tertiary color, which marks nothing else in the list: the primary color marks the open
    /// chat, and the colors of states tell how a transcript went.
    /// </remarks>
    public static readonly ChatIndicator ANSWER_UNREAD = new(Icons.Material.Outlined.MarkChatUnread, IsSpinning: false, Color.Tertiary, IsStatus: true);

    /// <summary>
    /// An answer which failed while the chat was not open, before the user saw it.
    /// </summary>
    public static readonly ChatIndicator ANSWER_FAILED = new(Icons.Material.Outlined.SmsFailed, IsSpinning: false, Color.Error, IsStatus: true);

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
    /// What each state of an answer shows once the user has seen how it ended, or while it runs.
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
    /// What each state of an answer shows while the user has not seen how it ended.
    /// </summary>
    /// <remarks>
    /// An answer written or failed while its chat was not open stays marked until the chat opens,
    /// see <see cref="AIJobSnapshot.IsOutcomeSeen"/>. An answer the user canceled needs no mark,
    /// and one which still runs shows the spinner, as with <see cref="ANSWER_STATES"/>.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<AIJobStatus, ChatIndicator> UNSEEN_ANSWER_STATES = new Dictionary<AIJobStatus, ChatIndicator>
    {
        [AIJobStatus.NONE] = PLAIN,
        [AIJobStatus.QUEUED] = ANSWER_GOING_ON,
        [AIJobStatus.WAITING_FOR_REMOTE] = ANSWER_GOING_ON,
        [AIJobStatus.RUNNING] = ANSWER_GOING_ON,
        [AIJobStatus.COMPLETED] = ANSWER_UNREAD,
        [AIJobStatus.CANCELED] = PLAIN,
        [AIJobStatus.FAILED] = ANSWER_FAILED,
    };

    /// <summary>
    /// What the icon of a chat shows, given the state of its transcript and of its answer.
    /// </summary>
    /// <remarks>
    /// Work going on comes first, then how an answer ended which the user has not seen yet, then
    /// how the last transcript ended. Otherwise, an answer being written after a finished transcript
    /// would hide behind the icon of that transcript, and so would the news of an answer.
    /// </remarks>
    /// <param name="transcriptState">The state of the chat's transcript, or null when there is none.</param>
    /// <param name="answerState">The state of the chat's answer, or null when there is none.</param>
    /// <param name="isAnswerOutcomeSeen">Whether the user has seen how the answer ended, see <see cref="AIJobSnapshot.IsOutcomeSeen"/>.</param>
    public static ChatIndicator Of(MediaImportStatus? transcriptState, AIJobStatus? answerState, bool isAnswerOutcomeSeen = true)
    {
        var transcript = transcriptState is { } transcriptValue ? TRANSCRIPT_STATES.GetValueOrDefault(transcriptValue, PLAIN) : PLAIN;
        if (transcript.IsSpinning)
            return transcript;

        var answerStates = isAnswerOutcomeSeen ? ANSWER_STATES : UNSEEN_ANSWER_STATES;
        var answer = answerState is { } answerValue ? answerStates.GetValueOrDefault(answerValue, PLAIN) : PLAIN;
        return answer.IsSpinning || answer.IsStatus ? answer : transcript;
    }
}