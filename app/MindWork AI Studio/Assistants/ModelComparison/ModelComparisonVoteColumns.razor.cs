using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Two columns to compare, with the button of each below it and the tie between them: the answers of a run in the
/// mode Answer Variance, and the chats of a task in the mode Everyday Tasks.
/// </summary>
/// <remarks>
/// Both modes share this layout, so that the user votes the same way in both: the button which chooses a column stands
/// below it, in a narrow window as well, where the second column moves below the first one.
/// </remarks>
public partial class ModelComparisonVoteColumns : MSGComponentBase
{
    /// <summary>
    /// Gets or sets what the left column shows, e.g., the first answer.
    /// </summary>
    [Parameter, EditorRequired]
    public RenderFragment ColumnA { get; set; } = null!;

    /// <summary>
    /// Gets or sets what the right column shows, e.g., the second answer.
    /// </summary>
    [Parameter, EditorRequired]
    public RenderFragment ColumnB { get; set; } = null!;

    /// <summary>
    /// Gets or sets the question above the buttons, or an empty text without one.
    /// </summary>
    [Parameter]
    public string Heading { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text of the button which chooses the left column.
    /// </summary>
    [Parameter, EditorRequired]
    public string ALabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text of the button which chooses the right column.
    /// </summary>
    [Parameter, EditorRequired]
    public string BLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text of the button which finds both columns equally good.
    /// </summary>
    [Parameter, EditorRequired]
    public string TieLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the buttons are disabled, e.g., while the vote is saved.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user votes: COLUMN_A for the left column, COLUMN_B for the right one.
    /// </summary>
    [Parameter]
    public EventCallback<ModelComparisonVote> Vote { get; set; }
}