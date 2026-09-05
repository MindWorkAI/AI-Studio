using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    //
    // Mistral names its models after the month they were released: mistral-large-2512 is
    // Mistral Large 3 from December 2025. The version number lives in the marketing name only,
    // so matching on it misses nearly every model the API actually serves. The constants below
    // read as YYMM and say from which release on a family gained a capability.
    //
    private const int MISTRAL_LARGE_VISION_SINCE = 2512;      // Mistral Large 3
    private const int MISTRAL_LARGE_REASONING_SINCE = 2512;   // Mistral Large 3
    private const int MISTRAL_MEDIUM_VISION_SINCE = 2505;     // Mistral Medium 3
    private const int MISTRAL_MEDIUM_REASONING_SINCE = 2604;  // Mistral Medium 3.5
    private const int MISTRAL_SMALL_VISION_SINCE = 2503;      // Mistral Small 3.1
    private const int MISTRAL_SMALL_REASONING_SINCE = 2603;   // Mistral Small 4
    private const int MINISTRAL_VISION_SINCE = 2512;          // Ministral 3

    /// <summary>
    /// Used for families which have no reasoning at all. No release date can ever reach it.
    /// </summary>
    private const int MISTRAL_REASONING_NEVER = int.MaxValue;

    //
    // Where the "latest" aliases point to. Mistral moves them on with every release, so they
    // have to behave like the release they resolve to instead of carrying their own rules.
    //
    private const int MISTRAL_LARGE_LATEST = 2512;
    private const int MISTRAL_MEDIUM_LATEST = 2604;
    private const int MISTRAL_SMALL_LATEST = 2603;
    private const int MINISTRAL_LATEST = 2512;

    /// <summary>
    /// Mistral released its first date-named model in 2023. Anything below that is not a release
    /// date but a parameter count or a context size which happens to have four digits.
    /// </summary>
    private const int MISTRAL_FIRST_RELEASE_YEAR = 23;

    //
    // Mistral serves some models under their marketing version as well, and it writes the version
    // separator both ways: mistral-medium-3.5 and mistral-medium-3-5 are the same model. Those
    // names carry no release date, so we map them onto the release they stand for. The order
    // matters: the more specific version has to come first, otherwise "3" would swallow "3.5".
    //
    private static readonly (string VersionName, int ReleaseDate)[] MISTRAL_VERSION_NAMES =
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

    private static List<Capability> GetModelCapabilitiesMistral(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();

        // Pixtral models are able to do process images:
        if (modelName.IndexOf("pixtral") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        // Mistral saba:
        if (modelName.IndexOf("mistral-saba-") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // The four families Mistral versions by release date. Ministral has to be matched before
        // the others, although its name does not contain "mistral" as a substring: keeping the
        // families together makes the block easier to read.
        //
        if (modelName.IndexOf("ministral") is not -1)
            return BuildMistralCapabilities(GetMistralReleaseDate(modelName, MINISTRAL_LATEST), MINISTRAL_VISION_SINCE, MISTRAL_REASONING_NEVER);

        if (modelName.IndexOf("mistral-large") is not -1)
            return BuildMistralCapabilities(GetMistralReleaseDate(modelName, MISTRAL_LARGE_LATEST), MISTRAL_LARGE_VISION_SINCE, MISTRAL_LARGE_REASONING_SINCE);

        if (modelName.IndexOf("mistral-medium") is not -1)
            return BuildMistralCapabilities(GetMistralReleaseDate(modelName, MISTRAL_MEDIUM_LATEST), MISTRAL_MEDIUM_VISION_SINCE, MISTRAL_MEDIUM_REASONING_SINCE);

        if (modelName.IndexOf("mistral-small") is not -1)
            return BuildMistralCapabilities(GetMistralReleaseDate(modelName, MISTRAL_SMALL_LATEST), MISTRAL_SMALL_VISION_SINCE, MISTRAL_SMALL_REASONING_SINCE);

        // Default:
        return GetModelCapabilitiesOpenSource(model);
    }

    /// <summary>
    /// Determines the release date a Mistral model belongs to.
    /// </summary>
    /// <param name="modelName">The lowercase model name to inspect.</param>
    /// <param name="latestReleaseDate">The release the family's "latest" alias points to.</param>
    /// <returns>The release date as YYMM, or 0 when the name carries none.</returns>
    private static int GetMistralReleaseDate(ReadOnlySpan<char> modelName, int latestReleaseDate)
    {
        // The "latest" alias always points to the newest release of its family:
        if (modelName.IndexOf("-latest") is not -1)
            return latestReleaseDate;

        foreach (var (versionName, releaseDate) in MISTRAL_VERSION_NAMES)
            if (modelName.IndexOf(versionName) is not -1)
                return releaseDate;

        return ReadMistralReleaseDate(modelName);
    }

    /// <summary>
    /// Reads the four-digit release date out of a Mistral model name.
    /// </summary>
    /// <remarks>
    /// The block has to be exactly four digits long and has to read as a plausible year and month.
    /// Without that, the size of a model would be mistaken for its release: ministral-14b-2512
    /// must resolve to 2512 and not to anything the "14b" part could be read as.
    /// </remarks>
    /// <param name="modelName">The lowercase model name to inspect.</param>
    /// <returns>The release date as YYMM, or 0 when the name carries none.</returns>
    private static int ReadMistralReleaseDate(ReadOnlySpan<char> modelName)
    {
        for (var index = 0; index + 4 <= modelName.Length; index++)
        {
            // A digit next to the block means the block is longer than four digits:
            if (index > 0 && char.IsAsciiDigit(modelName[index - 1]))
                continue;

            if (index + 4 < modelName.Length && char.IsAsciiDigit(modelName[index + 4]))
                continue;

            var candidate = modelName.Slice(index, 4);
            if (!char.IsAsciiDigit(candidate[0]) || !char.IsAsciiDigit(candidate[1]) ||
                !char.IsAsciiDigit(candidate[2]) || !char.IsAsciiDigit(candidate[3]))
                continue;

            var releaseDate = int.Parse(candidate);
            var year = releaseDate / 100;
            var month = releaseDate % 100;
            if (year < MISTRAL_FIRST_RELEASE_YEAR || month is < 1 or > 12)
                continue;

            return releaseDate;
        }

        return 0;
    }

    /// <summary>
    /// Builds the capabilities of a Mistral model from its release date.
    /// </summary>
    /// <remarks>
    /// A model whose release date we cannot read gets neither image input nor reasoning. That is
    /// the safe direction: offering an ability the model does not have would fail the request,
    /// whereas a missing one can be added by hand through the capability overrides.
    /// </remarks>
    /// <param name="releaseDate">The release date of the model as YYMM, or 0 when unknown.</param>
    /// <param name="visionSince">The release from which this family accepts images.</param>
    /// <param name="reasoningSince">The release from which this family can reason.</param>
    /// <returns>The capabilities of the model.</returns>
    private static List<Capability> BuildMistralCapabilities(int releaseDate, int visionSince, int reasoningSince)
    {
        List<Capability> capabilities = [Capability.TEXT_INPUT, Capability.FUNCTION_CALLING, Capability.CHAT_COMPLETION_API, Capability.TEXT_OUTPUT];

        if (releaseDate >= visionSince)
            capabilities.Add(Capability.MULTIPLE_IMAGE_INPUT);
        
        if (releaseDate >= reasoningSince)
            capabilities.Add(Capability.OPTIONAL_REASONING);

        return capabilities;
    }
}