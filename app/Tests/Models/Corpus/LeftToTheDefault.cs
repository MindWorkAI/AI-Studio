using static AIStudio.Provider.LLMProviders;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// The models of the corpus no rule answers for, and why each of them is all right that way.
/// </summary>
/// <remarks>
/// Hugging Face carries more than a hundred thousand models. Writing a rule for each is not a goal
/// anybody could reach, so the question was never whether models fall through to the default but
/// which ones may. This list is that decision, written down: every model here was looked at once,
/// and leaving it to the default was the answer.
///
/// It exists because the alternative is silence. A family nobody got round to and a family nobody
/// wanted look exactly the same from the outside -- both are simply missing -- and the difference
/// only survives if somebody writes it down. The verification run reads this list, and a test holds
/// it against the rules from both sides: nothing falls through unlisted, and nothing stays listed
/// once a rule does answer for it.
///
/// What the default says is that a model reads and writes text, speaks the chat completion API, and
/// calls functions. The last part is a guess, and the one that matters: the models where it goes
/// the wrong way are named in WithoutToolCallingFamily instead of being left here.
/// </remarks>
public static class LeftToTheDefault
{
    /// <summary>
    /// A model whose answer the default already gets right, word for word.
    /// </summary>
    private const string THE_DEFAULT_SAYS_THE_SAME = "The default answers exactly what the rules for it answer today: text in, text out, and tool calling.";

    /// <summary>
    /// A model which keeps what it needs and loses what was extra.
    /// </summary>
    private const string THE_DEFAULT_KEEPS_WHAT_MATTERS = "A family we decided not to write down. The default keeps the chat and the tool calling; what it drops is the thinking, which a person turns back on in the expert settings and an organization states in a model plugin.";

    /// <summary>
    /// A model which reads more than text, and is told it does not.
    /// </summary>
    private const string THE_DEFAULT_DROPS_THE_MODALITIES = "A family we decided not to write down. The default cannot know what it reads besides text, so images have to be turned on by hand -- in the expert settings, or for everybody through a model plugin.";

    /// <summary>
    /// Something a provider answered with which was never the name of a model.
    /// </summary>
    private const string NOT_A_MODEL_AT_ALL = "Not a model name. It is in the corpus because providers really answer with it, and the rules have to stay quiet rather than invent something.";

    /// <summary>
    /// Every model which reaches the global default on purpose.
    /// </summary>
    public static readonly IReadOnlyList<ModelLeftToTheDefault> ENTRIES =
    [
        //
        // Families whose answer the default already is. Writing them down would add a file and
        // change nothing about a single answer.
        //
        new(SELF_HOSTED, "olmo3:7b", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "falcon3:10b", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "falcon-h1-1.5b-tool-calling", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "salamandra-7b-instruct-tools", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "ling-1t", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "inclusionai/ling-mini-2.0", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "starling-lm:7b", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "ernie-4.5-21b", THE_DEFAULT_SAYS_THE_SAME),
        new(SELF_HOSTED, "phi3:14b", "The Phi rules were written for the fourth generation and the ones before it already reached the default, which answers them the same as it does today."),

        //
        // Families which lose their thinking to the default. It is the ability a person misses
        // least: the model still answers, and the answer still carries the thinking -- it is only
        // not announced, so the thinking settings stay hidden.
        //
        new(SELF_HOSTED, "olmo-3-32b-think", THE_DEFAULT_KEEPS_WHAT_MATTERS),
        new(SELF_HOSTED, "seed-oss:36b", THE_DEFAULT_KEEPS_WHAT_MATTERS),
        new(SELF_HOSTED, "ring-1t", THE_DEFAULT_KEEPS_WHAT_MATTERS),
        new(SELF_HOSTED, "ernie-x1.1-thinking", THE_DEFAULT_KEEPS_WHAT_MATTERS),
        new(SELF_HOSTED, "smollm3:3b", THE_DEFAULT_KEEPS_WHAT_MATTERS),
        new(HUGGINGFACE, "HuggingFaceTB/SmolLM3-3B", THE_DEFAULT_KEEPS_WHAT_MATTERS),
        new(SELF_HOSTED, "internlm3:8b", THE_DEFAULT_KEEPS_WHAT_MATTERS),

        //
        // Families which read more than text. These are the ones the decision costs something:
        // until somebody says otherwise, the chat will not offer to send them a picture.
        //
        new(SELF_HOSTED, "internvl3-8b", THE_DEFAULT_DROPS_THE_MODALITIES),
        new(GWDG, "internvl2.5-8b", THE_DEFAULT_DROPS_THE_MODALITIES),
        new(SELF_HOSTED, "ernie-4.5-vl-28b", THE_DEFAULT_DROPS_THE_MODALITIES),
        new(SELF_HOSTED, "apriel-1.5-15b-thinker", THE_DEFAULT_DROPS_THE_MODALITIES),
        new(SELF_HOSTED, "apriel-1.6-15b-thinker", THE_DEFAULT_DROPS_THE_MODALITIES),
        new(SELF_HOSTED, "apertus-1.5-8b", "A family we decided not to write down, and the one which loses the most by it: it reads images and listens to audio, and the default knows about neither."),

        //
        // Names which were never models.
        //
        new(OPEN_AI, "", NOT_A_MODEL_AT_ALL),
        new(SELF_HOSTED, "   ", NOT_A_MODEL_AT_ALL),
        new(SELF_HOSTED, "---", NOT_A_MODEL_AT_ALL),
        new(NONE, "gpt-5.6", "A model without a provider. There is no way to reach it, so there is nothing to say about how it could be used."),
        new(LITE_LLM, "the-fast-one", "A freely chosen LiteLLM alias. Nothing in the name says what is behind it, which is what the default exists for."),
        new(SELF_HOSTED, "a-model-nobody-has-heard-of", "The corpus entry for the default itself. It has to reach it, or the default would never be measured."),

        //
        // Still to do rather than decided.
        //
        new(LITE_LLM, "bedrock/anthropic.claude-3-5-sonnet-20241022-v2:0", "Not a decision: the LiteLLM host cannot take the Bedrock spelling apart yet, because the vendor sits behind a dot rather than a slash. ExpectedChanges holds the answer it has to arrive at."),
    ];
}