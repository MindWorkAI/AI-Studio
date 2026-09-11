namespace AIStudio.Models.Matching;

/// <summary>
/// A model ID in the form the rules are written in, next to the form the provider reported.
/// </summary>
/// <remarks>
/// Every provider names the same model differently, and the difference is rarely in the words: it
/// is in what sits between them. Ollama separates the variant with a colon ("qwen3.8:27b-mlx"),
/// Blablador answers with a whole sentence ("10 - Muse Glimmer 30b - the newest META model"),
/// Fireworks puts a path in front ("accounts/fireworks/models/llama-v3p1-405b-instruct"), and the
/// hubs use hyphens. Normalizing once, here, is what lets a rule be written once.
///
/// The dots stay. They carry the version boundary: llama3 and llama3.1 are different models, and
/// only the latter calls functions. Dropping them would merge the two. A hyphen, on the other hand,
/// is where one part of a name ends and the next begins -- which is why the patterns can say "at a
/// name part" and mean something.
/// </remarks>
/// <param name="modelId">The model ID as the provider reports it.</param>
public readonly struct ModelId(string modelId) : IEquatable<ModelId>
{
    /// <summary>
    /// What separates two parts of a normalized name.
    /// </summary>
    public const char SEGMENT_SEPARATOR = '-';

    /// <summary>
    /// The longest model ID we normalize without going to the heap.
    /// </summary>
    private const int MAX_STACK_ALLOCATED_MODEL_ID_LENGTH = 256;

    private readonly string originalId = modelId ?? string.Empty;
    private readonly string normalizedId = Normalize(modelId);

    /// <summary>
    /// The ID exactly as the provider reported it. This is what a person sees.
    /// </summary>
    public string Original => this.originalId ?? string.Empty;

    /// <summary>
    /// The ID in lowercase, with every separator written as a single hyphen.
    /// </summary>
    public string Normalized => this.normalizedId ?? string.Empty;

    /// <summary>
    /// Whether there is nothing here to match against.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this.normalizedId);

    /// <summary>
    /// The parts of the name, in order, without allocating anything.
    /// </summary>
    public ModelIdSegments Segments => new(this.Normalized.AsSpan());

    /// <summary>
    /// Whether the whole name is exactly this text.
    /// </summary>
    /// <param name="text">The text to compare against, already normalized.</param>
    /// <returns>True, when the name and the text are the same.</returns>
    public bool EqualsText(ReadOnlySpan<char> text) => !text.IsEmpty && this.Normalized.AsSpan().SequenceEqual(text);

    /// <summary>
    /// Whether the name begins with this text and a name part ends there.
    /// </summary>
    /// <remarks>
    /// The boundary is what keeps "gpt-5" away from "gpt-55", and what keeps it away from "gpt-5.1"
    /// as well: a dot is a version boundary, not a name part boundary, so those are two models and
    /// a rule for one of them does not answer for the other.
    /// </remarks>
    /// <param name="text">The text to look for, already normalized.</param>
    /// <returns>True, when the name starts with the text.</returns>
    public bool StartsWithSegments(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        var name = this.Normalized.AsSpan();
        return name.StartsWith(text) && IsBoundaryAt(name, text.Length);
    }

    /// <summary>
    /// Whether this text appears in the name as one or more whole name parts.
    /// </summary>
    /// <param name="text">The text to look for, already normalized.</param>
    /// <returns>True, when the text sits between two name part boundaries.</returns>
    public bool ContainsSegments(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        var name = this.Normalized.AsSpan();
        var searchedUpTo = 0;
        while (searchedUpTo <= name.Length - text.Length)
        {
            var offset = name[searchedUpTo..].IndexOf(text);
            if (offset is -1)
                return false;

            var start = searchedUpTo + offset;
            if (IsBoundaryAt(name, start - 1) && IsBoundaryAt(name, start + text.Length))
                return true;

            // The same text may appear again further on, at a boundary this time:
            searchedUpTo = start + 1;
        }

        return false;
    }

    /// <summary>
    /// Whether this text appears anywhere in the name, boundaries or not.
    /// </summary>
    /// <param name="text">The text to look for, already normalized.</param>
    /// <returns>True, when the name contains the text.</returns>
    public bool ContainsText(ReadOnlySpan<char> text) => !text.IsEmpty && this.Normalized.AsSpan().IndexOf(text) is not -1;

    public bool Equals(ModelId other) => string.Equals(this.Normalized, other.Normalized, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ModelId other && this.Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Normalized);

    public override string ToString() => this.Original;

    /// <summary>
    /// Whether a name part begins or ends at this position.
    /// </summary>
    /// <remarks>
    /// Positions outside the name count: the start of the name and its end are boundaries, which is
    /// what makes a one part name match a rule written for that part.
    /// </remarks>
    /// <param name="name">The normalized name.</param>
    /// <param name="index">The position to look at, which may be outside the name.</param>
    /// <returns>True, when there is a boundary at this position.</returns>
    private static bool IsBoundaryAt(ReadOnlySpan<char> name, int index) => index < 0 || index >= name.Length || name[index] is SEGMENT_SEPARATOR;

    /// <summary>
    /// Brings a model ID into the form the capability rules are written in.
    /// </summary>
    /// <param name="modelId">The model ID as the provider reports it, which may be nothing at all.</param>
    /// <returns>The model ID in lowercase, with every separator written as a single hyphen.</returns>
    private static string Normalize(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return string.Empty;

        //
        // Normalizing never makes a name longer, so the original length is always enough room.
        // Model IDs are short, which is why the buffer lives on the stack: the longest ones we
        // know of are the descriptive names Blablador answers with, at around 75 characters. A
        // provider reporting something longer still gets a correct answer, just from the heap.
        //
        Span<char> normalized = modelId.Length <= MAX_STACK_ALLOCATED_MODEL_ID_LENGTH
            ? stackalloc char[modelId.Length]
            : new char[modelId.Length];

        var length = 0;
        foreach (var character in modelId)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.')
            {
                normalized[length++] = char.ToLowerInvariant(character);
                continue;
            }

            // Anything else separates two parts of the name. A leading separator, and a repeated
            // one, say nothing and would only get in the way of the patterns:
            if (length is 0 || normalized[length - 1] is SEGMENT_SEPARATOR)
                continue;

            normalized[length++] = SEGMENT_SEPARATOR;
        }

        // A trailing separator carries no meaning either:
        if (length > 0 && normalized[length - 1] is SEGMENT_SEPARATOR)
            length--;

        return new string(normalized[..length]);
    }
}