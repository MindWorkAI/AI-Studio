namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How the summary of a batch shows what the user and the judge chose in a run.
/// </summary>
public static class ModelComparisonOutcome
{
    /// <summary>
    /// Makes the card of an answer as high as its column, so that both cards of a run are equally high.
    /// </summary>
    public const string FILL_THE_COLUMN = "flex-grow-1";

    /// <summary>
    /// Gets how a model did in the vote of the user, as a color: green when its answer was preferred, red when the
    /// other one was, orange when the user found them equally good.
    /// </summary>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    /// <param name="model">The model whose outcome this is.</param>
    /// <returns>The color.</returns>
    public static Color ColorOf(ModelComparisonModelChoice? voteChoice, ModelComparisonModelChoice model) => voteChoice switch
    {
        null => Color.Default,
        ModelComparisonModelChoice.TIE => Color.Warning,
        { } chosen when chosen == model => Color.Success,

        _ => Color.Error,
    };

    /// <summary>
    /// Gets the icon which goes with ColorOf. The color alone would not tell the outcomes apart for readers who are
    /// color blind, so every outcome has a shape of its own as well.
    /// </summary>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    /// <param name="model">The model whose outcome this is.</param>
    /// <returns>The icon.</returns>
    public static string IconOf(ModelComparisonModelChoice? voteChoice, ModelComparisonModelChoice model) => voteChoice switch
    {
        null => AppIcons.VOTE_MISSING,
        ModelComparisonModelChoice.TIE => AppIcons.VOTE_TIE,
        { } chosen when chosen == model => AppIcons.VOTE_WON,

        _ => AppIcons.VOTE_LOST,
    };

    /// <summary>
    /// Gets the classes of the card around an answer: it fills the height of its column, and with a vote of the user,
    /// it has a border in the color which goes with ColorOf.
    /// </summary>
    /// <remarks>
    /// The classes are the ones of MudBlazor: a border two pixels wide, in the color of the outcome. They override the
    /// thin gray line of the card, which is why they are told to win. While the user votes, and for a run without a
    /// vote, the answers stand in the plain frame of the chat.
    /// </remarks>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    /// <param name="model">The model whose answer the card shows.</param>
    /// <returns>The classes.</returns>
    public static string CardClassOf(ModelComparisonModelChoice? voteChoice, ModelComparisonModelChoice model)
    {
        if (voteChoice is null)
            return FILL_THE_COLUMN;

        var color = ColorOf(voteChoice, model) switch
        {
            Color.Success => "mud-border-success",
            Color.Warning => "mud-border-warning",

            _ => "mud-border-error",
        };

        return $"{FILL_THE_COLUMN} border-2 border-solid {color}";
    }

    /// <summary>
    /// Gets whether the badge of the judge belongs on the answer of a model: either the judge preferred it, or the judge
    /// saw a tie, in which case both answers carry the badge.
    /// </summary>
    /// <param name="judgeChoice">The model the judge preferred, or null without a usable verdict.</param>
    /// <param name="model">The model whose answer this is.</param>
    /// <returns>True when the answer carries the badge.</returns>
    public static bool JudgeFavors(ModelComparisonModelChoice? judgeChoice, ModelComparisonModelChoice model) => judgeChoice is ModelComparisonModelChoice.TIE || judgeChoice == model;
}