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
    /// How many tokens the counted parts of the conversation take, the draft included.
    /// </summary>
    public int Tokens => this.HistoryTokens + this.DraftTokens;

    /// <summary>
    /// How many tokens the conversation takes without the message being written right now.
    /// </summary>
    public int HistoryTokens { get; init; }

    /// <summary>
    /// How much of HistoryTokens is what the tools of the running request returned so far.
    /// </summary>
    /// <remarks>
    /// Named on its own because it is the one share which goes away again. The model reads it in
    /// every round of the request, so it fills the window while the tools work -- and none of it is
    /// sent with the next message. Without saying so, the number would climb by tens of thousands
    /// and then drop back once the answer stands, and nobody could tell why.
    /// </remarks>
    public int ToolTokens { get; init; }

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
    /// How many tokens the message being written right now takes.
    /// </summary>
    /// <remarks>
    /// Kept apart from the rest for the same reason the statements above are kept apart: what the
    /// conversation has already cost is something a provider can be asked about, while a sentence
    /// nobody has sent yet can only be estimated.
    /// </remarks>
    public int DraftTokens { get; init; }

    /// <summary>
    /// Whether the conversation so far was counted by the provider rather than by this app.
    /// </summary>
    /// <remarks>
    /// True once a provider has stated what a request of this conversation cost, which makes
    /// everything up to the last answer an exact number. That answer is counted by this app, since
    /// the provider's number for it includes reasoning which no request carries; next to the rest
    /// of the conversation, its share of the error is small enough to still call the history exact.
    /// It says nothing about the draft, which stays an estimate either way -- nobody has charged
    /// for that one yet.
    /// </remarks>
    public bool HistoryIsReported { get; init; }

    /// <summary>
    /// How much the model reads, where anybody has stated it.
    /// </summary>
    public ContextWindow Window { get; init; }

    /// <summary>
    /// How many images travel along, those of the conversation and those of the draft.
    /// </summary>
    public int Images { get; init; }

    /// <summary>
    /// How many of those images are attached to the message being written right now.
    /// </summary>
    public int DraftImages { get; init; }

    /// <summary>
    /// How many images travel along which nobody has counted.
    /// </summary>
    /// <remarks>
    /// Every vendor charges images differently -- OpenAI by tiles of the scaled image, Anthropic by
    /// its area, Google by tiles of another size -- and none of those numbers can be had from the
    /// file without decoding it first. So they are reported as a number of images instead of being
    /// guessed at, or worse, counted as the base64 text they are sent as: that text is two to three
    /// orders of magnitude longer than what any vendor charges for the picture.
    ///
    /// Where the provider reported the conversation so far, it counted the pictures in it as well,
    /// however it charges them. Then only those of the draft are left uncounted.
    /// </remarks>
    public int UncountedImages => this.HistoryIsReported ? this.DraftImages : this.Images;

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
    /// something that works. Whether anybody counted the pictures plays no part: the limit is on
    /// how many travel, not on what they cost.
    /// </remarks>
    public bool TooManyImages => this.ImageLimits.MaxInOneMessage is { } allowed && this.Images > allowed;
}