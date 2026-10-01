using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Says which tokenizer a model uses, next to the field which asks for one.
/// </summary>
/// <remarks>
/// The field takes a tokenizer.json file and nothing else, and for a long time it said nothing about
/// which file. That leaves two kinds of people stuck: the ones who could download the right one and
/// do not know its name, and the ones who go looking for Anthropic's tokenizer file, which was never
/// published.
///
/// One component rather than a sentence in each dialog, because both the LLM provider dialog and the
/// embedding provider dialog ask the same question and deserve the same answer. Two copies would be
/// two sets of translations of the same three sentences, and the second copy is the one which gets
/// forgotten when the wording changes.
/// </remarks>
public partial class TokenizerHint : ComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(TokenizerHint).Namespace, nameof(TokenizerHint));

    /// <summary>
    /// Which provider the model is served by.
    /// </summary>
    [Parameter]
    public LLMProviders LLMProvider { get; set; } = LLMProviders.NONE;

    /// <summary>
    /// The model whose tokenizer is in question.
    /// </summary>
    [Parameter]
    public Model Model { get; set; }

    /// <summary>
    /// The classes of the text, so a dialog can keep its own spacing.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = "mb-3";

    /// <summary>
    /// What there is to say, or nothing at all.
    /// </summary>
    /// <remarks>
    /// Empty for a model nobody named a tokenizer for, which is most of them. Saying "unknown"
    /// would fill the dialog with a line which helps nobody; saying nothing leaves it as it was.
    /// </remarks>
    private string Text
    {
        get
        {
            var tokenizer = this.LLMProvider.GetModelProfile(this.Model).Tokenizer;
            return tokenizer.IsKnown ? Describe(tokenizer) : string.Empty;
        }
    }

    private static string Describe(TokenizerRef tokenizer) => tokenizer.Kind switch
    {
        TokenizerKind.HUGGING_FACE => string.Format(TB("This model uses the tokenizer of {0}. Download its tokenizer.json file and select it below to count exactly instead of estimating."), tokenizer.Id),
        TokenizerKind.TIKTOKEN => string.Format(TB("This model uses OpenAI's {0} encoding, which does not come as a tokenizer.json file. AI Studio therefore estimates the token count with its built-in tokenizer."), tokenizer.Id),
        TokenizerKind.PROVIDER_API => string.Format(TB("The vendor of this model publishes no tokenizer file and counts through their API instead ({0}). AI Studio therefore estimates the token count with its built-in tokenizer."), tokenizer.Id),

        _ => string.Empty,
    };
}