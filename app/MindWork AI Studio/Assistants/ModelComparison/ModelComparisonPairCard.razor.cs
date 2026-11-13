using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How two models did against each other, by the votes of the user and, when there are any, by the verdicts of the
/// judges.
/// </summary>
public partial class ModelComparisonPairCard : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the pair.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonPairing Pairing { get; set; } = null!;

    /// <summary>
    /// Says which model of the pair won clearly, if one did.
    /// </summary>
    /// <param name="winShareOfFirst">The share of wins of the first model, by the votes or by the judges.</param>
    private string SignificanceText(ModelComparisonWinShare winShareOfFirst) => winShareOfFirst.Significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => string.Format(T("{0} is clearly better"), this.Pairing.First.Label),
        ModelComparisonSignificance.CLEARLY_WORSE => string.Format(T("{0} is clearly better"), this.Pairing.Second.Label),
        _ => T("Not yet clear"),
    };
}