using AIStudio.Models;

namespace AIStudio.Chat;

/// <summary>
/// What a conversation costs, as far as the app can count it.
/// </summary>
/// <remarks>
/// Three separate statements, and keeping them apart is the point. How many tokens were counted is
/// one; what the model's window is, if anybody has written it down, is the second; and how much of
/// the conversation could not be counted at all is the third. Folding any of them into the others
/// would turn a gap into a number somebody reads as a fact.
/// </remarks>
public readonly record struct ConversationTokens
{
    /// <summary>
    /// The answer when nothing could be counted, which is what a broken tokenizer leaves behind.
    /// </summary>
    /// <remarks>
    /// Deliberately not a zero. A conversation of no tokens and a conversation nobody could measure
    /// look the same as a number and are not the same thing, so the display shows nothing at all
    /// rather than claiming an empty chat.
    /// </remarks>
    public static readonly ConversationTokens UNAVAILABLE = new();

    /// <summary>
    /// Whether anything could be counted.
    /// </summary>
    public bool IsKnown { get; init; }

    /// <summary>
    /// How many tokens the counted parts of the conversation take.
    /// </summary>
    public int Tokens { get; init; }

    /// <summary>
    /// Whether the number is an estimate rather than the model's own count.
    /// </summary>
    /// <remarks>
    /// True whenever the built-in tokenizer did the counting, which is the normal case: a model's
    /// own tokenizer is only used where somebody configured one for their provider. Two tokenizers
    /// disagree by a few percent on ordinary prose and by a lot more on code or a language they were
    /// not trained on, so the number is shown as an approximation unless we counted with the
    /// tokenizer the model itself uses.
    /// </remarks>
    public bool IsEstimate { get; init; }

    /// <summary>
    /// How much the model reads, where anybody has stated it.
    /// </summary>
    public ContextWindow Window { get; init; }

    /// <summary>
    /// How many images travel along which nobody can count.
    /// </summary>
    /// <remarks>
    /// Every vendor charges images differently -- OpenAI by tiles of the scaled image, Anthropic by
    /// its area, Google by tiles of another size -- and none of those numbers can be had from the
    /// file without decoding it first. So they are reported as a number of images instead of being
    /// guessed at, or worse, counted as the base64 text they are sent as: that text is two to three
    /// orders of magnitude longer than what any vendor charges for the picture.
    /// </remarks>
    public int UncountedImages { get; init; }

    /// <summary>
    /// How many images the model takes, where its vendor stated a number.
    /// </summary>
    public ImageLimits ImageLimits { get; init; }

    /// <summary>
    /// Whether more images travel than the model is documented to accept.
    /// </summary>
    /// <remarks>
    /// Counted over the whole conversation rather than over the message being written, because that
    /// is what a request carries: every picture anybody attached is sent again with every further
    /// message, so a chat crosses this line long after the message which added the picture -- and
    /// the person who crosses it has usually forgotten that the pictures are still there.
    ///
    /// False whenever nobody stated a limit, which is most models. An invented ceiling would refuse
    /// something that works.
    /// </remarks>
    public bool TooManyImages => this.ImageLimits.MaxInOneMessage is { } allowed && this.UncountedImages > allowed;
}