namespace AIStudio.Models;

/// <summary>
/// How many images a model accepts, where anybody has said so.
/// </summary>
/// <remarks>
/// Both numbers exist in the wild and they are not the same one: Anthropic documents a limit for a
/// whole request, while vLLM limits each prompt through --limit-mm-per-prompt and ships with that
/// set to one image. A model card may state either without the other, which is why each is optional
/// on its own instead of sharing one "is known" flag.
///
/// Zero is a real answer here, not a stand-in for unknown: an operator can configure an engine to
/// accept no images at all. Unknown is null.
/// </remarks>
/// <param name="MaxPerMessage">How many images fit into one message, or null when nobody has said.</param>
/// <param name="MaxPerRequest">How many images fit into one request, or null when nobody has said.</param>
public readonly record struct ImageLimits(int? MaxPerMessage, int? MaxPerRequest)
{
    /// <summary>
    /// The number to show a user, or to plan with, where nothing is known.
    /// </summary>
    /// <remarks>
    /// This is a number for whoever needs one, never a limit to enforce. Today, saying that a model
    /// takes several images says nothing about how many, and turning that into a hidden ceiling of
    /// six would take something away from the models which handle a hundred.
    /// </remarks>
    public const int DEFAULT_MAX_IMAGES = 6;

    /// <summary>
    /// The limits of a model nobody has written anything about.
    /// </summary>
    public static readonly ImageLimits UNKNOWN = new(null, null);

    /// <summary>
    /// Whether either of the two numbers is known.
    /// </summary>
    public bool IsKnown => this.MaxPerMessage.HasValue || this.MaxPerRequest.HasValue;

    /// <summary>
    /// How many images may travel in one message, as far as anybody has said.
    /// </summary>
    /// <remarks>
    /// A message is part of a request, so a message cannot carry more than a whole request may --
    /// whichever of the two numbers is smaller decides, and a number nobody stated does not decide
    /// anything. Null means nobody stated either, which is a gap and never a limit of zero.
    /// </remarks>
    public int? MaxInOneMessage => (this.MaxPerMessage, this.MaxPerRequest) switch
    {
        ({ } perMessage, { } perRequest) => Math.Min(perMessage, perRequest),
        ({ } perMessage, null) => perMessage,
        (null, { } perRequest) => perRequest,

        _ => null,
    };
}