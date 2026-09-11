using AIStudio.Models.Matching;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Reads the release a Mistral model belongs to out of its name.
/// </summary>
/// <remarks>
/// Mistral names its models after the month they came out: mistral-large-2512 is Mistral Large 3
/// from December 2025. The marketing version lives in the marketing name only, so a rule written
/// against it would miss nearly every model the API actually serves. What a family can state is
/// therefore not "this model reads images" but "this family reads images from this release on",
/// and that is a calculation, not a pattern -- which is what the families do in Refine.
/// </remarks>
public static class MistralReleases
{
    /// <summary>
    /// A threshold no release can ever reach, for a family which never gained the ability at all.
    /// </summary>
    public const int NEVER = int.MaxValue;

    /// <summary>
    /// What a name says when it carries no release at all.
    /// </summary>
    /// <remarks>
    /// Nothing is granted for it. That is the safe direction: offering an ability the model does
    /// not have makes the request fail, while a missing one can be handed back by a person through
    /// the expert settings.
    /// </remarks>
    public const int UNKNOWN = 0;

    /// <summary>
    /// How many digits a release is written with.
    /// </summary>
    private const int RELEASE_LENGTH = 4;

    /// <summary>
    /// Mistral released its first date-named model in 2023.
    /// </summary>
    /// <remarks>
    /// Anything below that is not a release date but a parameter count or a context size which
    /// happens to have four digits.
    /// </remarks>
    private const int FIRST_RELEASE_YEAR = 23;

    /// <summary>
    /// The releases the marketing versions stand for.
    /// </summary>
    /// <remarks>
    /// Mistral serves some models under their marketing version as well, and writes the version
    /// separator both ways: mistral-medium-3.5 and mistral-medium-3-5 are the same model. Those
    /// names carry no release date, so they are mapped onto the release they stand for.
    ///
    /// The order matters, and it is the one place in this rebuild where it still does: these are
    /// read as plain text rather than as patterns, so "mistral-medium-3" would answer for
    /// "mistral-medium-3.5" if it came first.
    /// </remarks>
    private static readonly (string VersionName, int Release)[] VERSION_NAMES =
    [
        ("mistral-large-3", 2512),

        ("mistral-medium-3.5", 2604),
        ("mistral-medium-3-5", 2604),
        ("mistral-medium-3.1", 2508),
        ("mistral-medium-3-1", 2508),
        ("mistral-medium-3", 2505),

        ("mistral-small-4", 2603),
        ("mistral-small-3.2", 2506),
        ("mistral-small-3-2", 2506),
        ("mistral-small-3.1", 2503),
        ("mistral-small-3-1", 2503),
        ("mistral-small-3", 2501),
    ];

    /// <summary>
    /// The release a model name belongs to.
    /// </summary>
    /// <param name="id">The model name.</param>
    /// <param name="latestRelease">The release this family's "latest" alias currently points at.</param>
    /// <returns>The release as YYMM, or unknown.</returns>
    public static int Of(in ModelId id, int latestRelease)
    {
        // The "latest" alias always points at the newest release of its family:
        if (id.ContainsSegments("latest"))
            return latestRelease;

        foreach (var (versionName, release) in VERSION_NAMES)
            if (id.ContainsText(versionName))
                return release;

        return ReadFrom(id.Normalized.AsSpan());
    }

    /// <summary>
    /// Reads the four-digit release out of a name.
    /// </summary>
    /// <remarks>
    /// The block has to be exactly four digits long and has to read as a plausible year and month.
    /// Without that, the size of a model would be mistaken for its release: ministral-14b-2512 has
    /// to resolve to 2512 and not to anything the "14b" part could be read as.
    /// </remarks>
    /// <param name="modelName">The normalized model name.</param>
    /// <returns>The release as YYMM, or unknown.</returns>
    private static int ReadFrom(ReadOnlySpan<char> modelName)
    {
        for (var index = 0; index + RELEASE_LENGTH <= modelName.Length; index++)
        {
            // A digit next to the block means the block is longer than four digits:
            if (index > 0 && char.IsAsciiDigit(modelName[index - 1]))
                continue;

            if (index + RELEASE_LENGTH < modelName.Length && char.IsAsciiDigit(modelName[index + RELEASE_LENGTH]))
                continue;

            var candidate = modelName.Slice(index, RELEASE_LENGTH);
            if (!char.IsAsciiDigit(candidate[0]) || !char.IsAsciiDigit(candidate[1]) ||
                !char.IsAsciiDigit(candidate[2]) || !char.IsAsciiDigit(candidate[3]))
                continue;

            var release = int.Parse(candidate);
            var year = release / 100;
            var month = release % 100;
            if (year < FIRST_RELEASE_YEAR || month is < 1 or > 12)
                continue;

            return release;
        }

        return UNKNOWN;
    }
}