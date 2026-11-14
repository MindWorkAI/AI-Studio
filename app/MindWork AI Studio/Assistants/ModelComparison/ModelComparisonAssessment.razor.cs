using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How sure the results are that a model is better or worse, with an icon and a color, so that the assessment never
/// depends on the color alone.
/// </summary>
public partial class ModelComparisonAssessment : MSGComponentBase
{
    /// <summary>
    /// Gets or sets what the assessment says.
    /// </summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how sure the results are.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonSignificance Significance { get; set; }

    /// <summary>
    /// Gets or sets whether the assessment is about a pair: a pair which one model won clearly gets the icon and the
    /// color of the better one, since one of the two is always clearly worse then.
    /// </summary>
    [Parameter]
    public bool IsPair { get; set; }

    private string Icon => this.Significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => AppIcons.CLEARLY_BETTER,
        ModelComparisonSignificance.CLEARLY_WORSE => this.IsPair ? AppIcons.CLEARLY_BETTER : AppIcons.CLEARLY_WORSE,
        _ => AppIcons.NOT_YET_CLEAR,
    };

    private Color Color => this.Significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => Color.Success,
        ModelComparisonSignificance.CLEARLY_WORSE => this.IsPair ? Color.Success : Color.Error,
        _ => Color.Default,
    };
}