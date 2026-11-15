using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The values which the person sorts in the step "Values and perspectives" of the BeyondFacts Lab, in rounds of a few
/// values each.
/// </summary>
/// <remarks>
/// Lara Wüster (TUM) chose the values after the Personal Values Card Sort by Miller et al. (2001). The deck comes with
/// the app as one JSON file per language, below Assistants/BeyondFactsLab/Values; a development build reads it from
/// the disk, see AppResources. Every language has the same values in the same rounds, under the same IDs.
/// </remarks>
public sealed class BflValueDeck
{
    /// <summary>
    /// How many rounds the deck has.
    /// </summary>
    public const int ROUNDS = 3;

    /// <summary>
    /// How many values each round has.
    /// </summary>
    public const int VALUES_PER_ROUND = 4;

    /// <summary>
    /// The folder of the decks, relative to the project folder.
    /// </summary>
    private const string FOLDER = "Assistants/BeyondFactsLab/Values";

    /// <summary>
    /// The language whose deck the app shows when it has none in the language of the app.
    /// </summary>
    private const string FALLBACK_LANGUAGE = "en";

    /// <summary>
    /// The languages the app comes with a deck for, by their two-letter ISO codes.
    /// </summary>
    public static readonly FrozenSet<string> LANGUAGES = FrozenSet.Create(StringComparer.Ordinal, "de", FALLBACK_LANGUAGE);

    /// <summary>
    /// Gets or sets the source the deck is based on, as a citation.
    /// </summary>
    public string Citation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rounds, each with its values, in the order the person sorts them.
    /// </summary>
    public List<List<BflValueCard>> Rounds { get; set; } = [];

    /// <summary>
    /// Gets every value of the deck, round by round.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<BflValueCard> Cards => [.. this.Rounds.SelectMany(round => round)];

    /// <summary>
    /// Finds a value of the deck.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    /// <returns>The value, or null when the deck has none with this ID.</returns>
    public BflValueCard? Find(string valueId) => this.Rounds.SelectMany(round => round).FirstOrDefault(card => string.Equals(card.Id, valueId, StringComparison.Ordinal));

    /// <summary>
    /// Lists what is wrong with the deck: the number of rounds and values, IDs which are empty or appear twice, and
    /// values without a name or a definition.
    /// </summary>
    /// <returns>The problems; empty when the deck is fine.</returns>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(this.Citation))
            problems.Add("The deck names no source.");

        if (this.Rounds.Count != ROUNDS)
            problems.Add($"The deck has {this.Rounds.Count} rounds instead of {ROUNDS}.");

        for (var index = 0; index < this.Rounds.Count; index++)
        {
            var round = this.Rounds[index];
            if (round.Count != VALUES_PER_ROUND)
                problems.Add($"Round {index + 1} has {round.Count} values instead of {VALUES_PER_ROUND}.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in this.Rounds.SelectMany(round => round))
        {
            if (string.IsNullOrWhiteSpace(card.Id))
                problems.Add("A value has no ID.");
            else if (!ids.Add(card.Id))
                problems.Add($"The ID '{card.Id}' appears twice.");

            if (string.IsNullOrWhiteSpace(card.Name) || string.IsNullOrWhiteSpace(card.Definition))
                problems.Add($"The value '{card.Id}' lacks its name or its definition.");
        }

        return problems;
    }

    /// <summary>
    /// Gets the file of the deck which the app shows in a language: the one of that language, or else the English one.
    /// </summary>
    /// <param name="culture">The culture of the language, e.g., the culture of the app.</param>
    /// <returns>The file, relative to the project folder.</returns>
    public static string PathOf(CultureInfo culture)
    {
        var language = LANGUAGES.Contains(culture.TwoLetterISOLanguageName) ? culture.TwoLetterISOLanguageName : FALLBACK_LANGUAGE;
        return $"{FOLDER}/values.{language}.json";
    }

    /// <summary>
    /// Reads the deck which the app shows in a language.
    /// </summary>
    /// <param name="culture">The culture of the language, e.g., the culture of the app.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The deck.</returns>
    /// <exception cref="FileNotFoundException">The app comes without the deck.</exception>
    /// <exception cref="InvalidDataException">The deck cannot be read, or something is wrong with it.</exception>
    public static async Task<BflValueDeck> LoadAsync(CultureInfo culture, CancellationToken token = default)
    {
        var path = PathOf(culture);
        var text = await AppResources.ReadTextAsync(path, token);
        if (string.IsNullOrWhiteSpace(text))
            throw new FileNotFoundException("The BeyondFacts Lab misses its value deck.", path);

        BflValueDeck? deck;
        try
        {
            deck = JsonSerializer.Deserialize<BflValueDeck>(text, BeyondFactsLabJson.PERSISTENCE);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The value deck '{path}' of the BeyondFacts Lab cannot be read.", exception);
        }

        if (deck is null || IsNull(deck.Citation) || IsNull(deck.Rounds) || deck.Rounds.Any(round => IsNull(round) || round.Any(card => IsNull(card) || IsNull(card.Id) || IsNull(card.Name) || IsNull(card.Definition))))
            throw new InvalidDataException($"The value deck '{path}' of the BeyondFacts Lab cannot be read.");

        var problems = deck.Problems();
        if (problems.Count > 0)
            throw new InvalidDataException($"The value deck '{path}' of the BeyondFacts Lab is wrong: {string.Join(" ", problems)}");

        return deck;
    }

    /// <summary>
    /// Checks for null a value which the type system says cannot be null, but which JSON can make null.
    /// </summary>
    /// <param name="value">The value.</param>
    private static bool IsNull<T>(T? value) => value is null;
}