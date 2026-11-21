using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The decision on a task of a comparison series: both chats side by side, read-only and without the names of the
/// models, and the buttons of the vote.
/// </summary>
/// <remarks>
/// The chats keep their places, the first chat on the left: the order in which the user met the models was drawn at
/// random already, see ModelComparisonSeriesTask.FirstModelIsChatOne, and a vote names a chat, not a model. The chats
/// stand side by side on wide windows and below each other on narrow ones, so that long answers stay readable, each
/// with its button below it, as the answers in the mode Answer Variance, see ModelComparisonVoteColumns.
/// </remarks>
public partial class ModelComparisonSeriesDecision : MSGComponentBase
{
    private const Tools.Components COMPONENT = Tools.Components.MODEL_COMPARISON_ASSISTANT;

    /// <summary>
    /// Gets or sets where the chats of the series live.
    /// </summary>
    [Parameter, EditorRequired]
    public IEmbeddedChatStore Store { get; set; } = null!;

    /// <summary>
    /// Gets or sets the task.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonSeriesTask SeriesTask { get; set; } = null!;

    /// <summary>
    /// Gets or sets how a chat of the series starts, for a chat which has no stored thread.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<ChatThread> CreateThread { get; set; } = () => new();

    /// <summary>
    /// Gets or sets whether the series is being saved right now, which holds the vote back.
    /// </summary>
    [Parameter]
    public bool IsSaving { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user votes: COLUMN_A names the first chat, COLUMN_B the second one.
    /// </summary>
    [Parameter]
    public EventCallback<ModelComparisonVote> Vote { get; set; }
}