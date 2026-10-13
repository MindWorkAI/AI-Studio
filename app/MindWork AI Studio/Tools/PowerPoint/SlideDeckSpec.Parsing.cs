using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Assistants.Builder;

namespace AIStudio.Tools.PowerPoint;

internal sealed partial record SlideDeckSpec
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 16,
    };

    /// <summary>
    /// The deepest list level a paragraph may have; PowerPoint knows nine, starting at 0.
    /// </summary>
    private const int MAX_PARAGRAPH_LEVEL = 8;

    /// <summary>
    /// What models put in front of a bullet point although the template draws the bullets.
    /// </summary>
    private static readonly string[] BULLET_PREFIXES = ["- ", "* ", "• ", "– "];

    /// <summary>
    /// Reads the slide deck out of a model answer.
    /// </summary>
    /// <remarks>
    /// The JSON may be wrapped in prose or in a code fence. A text slot may be written as a list of
    /// paragraphs or as a single string, and a paragraph as a string or as an object with a text and a
    /// level. A string holding line breaks counts as one paragraph per line.
    /// </remarks>
    /// <param name="modelResponse">The answer of the model.</param>
    /// <param name="spec">The slide deck, or an empty one when reading failed.</param>
    /// <param name="issue">Why reading failed, written for the model; empty on success.</param>
    /// <returns>Whether the answer could be read.</returns>
    public static bool TryParse(string modelResponse, out SlideDeckSpec spec, out string issue)
    {
        spec = new SlideDeckSpec(string.Empty, []);
        issue = string.Empty;

        var json = LuaResponse.ExtractJson(modelResponse);
        if (string.IsNullOrWhiteSpace(json))
        {
            issue = "The answer contains no complete JSON object.";
            return false;
        }

        DeckDto? deck;
        try
        {
            deck = JsonSerializer.Deserialize<DeckDto>(json, JSON_OPTIONS);
        }
        catch (JsonException e)
        {
            issue = $"The JSON object cannot be read: {e.Message}";
            return false;
        }

        if (deck is null)
        {
            issue = "The JSON object is empty.";
            return false;
        }

        spec = new SlideDeckSpec(Clean(deck.Title), (deck.Slides ?? []).OfType<SlideDto>().Select(ToSlide).ToList());
        return true;
    }

    private static SlideSpec ToSlide(SlideDto slide) => new(
        Clean(slide.Layout),
        slide.StartsChapter ?? false,
        Clean(slide.Title),
        Clean(slide.Subtitle),
        (slide.Texts ?? []).Select(ToTextBlock).ToList(),
        (slide.Labels ?? []).Select(TextOf).ToList(),
        slide.Pictures ?? [],
        slide.Table is null ? null : ToTable(slide.Table));

    private static SlideTextBlock ToTextBlock(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => new SlideTextBlock(element.EnumerateArray().SelectMany(ToParagraphs).ToList()),

        _ => new SlideTextBlock(ToParagraphs(element).ToList()),
    };

    private static IEnumerable<SlideParagraph> ToParagraphs(JsonElement element)
    {
        var level = 0;
        var text = TextOf(element);
        if (element.ValueKind is JsonValueKind.Object)
        {
            text = element.TryGetProperty("text", out var textElement) ? TextOf(textElement) : string.Empty;
            if (element.TryGetProperty("level", out var levelElement) && levelElement.ValueKind is JsonValueKind.Number && levelElement.TryGetInt32(out var parsedLevel))
                level = Math.Clamp(parsedLevel, 0, MAX_PARAGRAPH_LEVEL);
        }

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var paragraph = WithoutBullet(line.Trim());
            if (paragraph.Length > 0)
                yield return new SlideParagraph(paragraph, level);
        }
    }

    private static SlideTableSpec ToTable(TableDto table) => new(
        (table.Headers ?? []).Select(TextOf).ToList(),
        (table.Rows ?? []).OfType<List<JsonElement>>().Select(row => (IReadOnlyList<string>)row.Select(TextOf).ToList()).ToList());

    /// <summary>
    /// The text of a JSON value, whatever kind of value the model chose: a number in a table cell
    /// is as good as a string.
    /// </summary>
    private static string TextOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => Clean(element.GetString()),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,

        _ => element.GetRawText(),
    };

    private static string WithoutBullet(string text)
    {
        foreach (var prefix in BULLET_PREFIXES)
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return text[prefix.Length..].TrimStart();

        return text;
    }

    private static string Clean(string? text) => text?.Trim() ?? string.Empty;

    //
    // What the model writes, before anything is checked. Every value may be missing or null.
    //

    private sealed class DeckDto
    {
        public string? Title { get; set; }

        public List<SlideDto?>? Slides { get; set; }
    }

    private sealed class SlideDto
    {
        public string? Layout { get; set; }

        public bool? StartsChapter { get; set; }

        public string? Title { get; set; }

        public string? Subtitle { get; set; }

        public List<JsonElement>? Texts { get; set; }

        public List<JsonElement>? Labels { get; set; }

        public List<int>? Pictures { get; set; }

        public TableDto? Table { get; set; }
    }

    private sealed class TableDto
    {
        public List<JsonElement>? Headers { get; set; }

        public List<List<JsonElement>?>? Rows { get; set; }
    }
}