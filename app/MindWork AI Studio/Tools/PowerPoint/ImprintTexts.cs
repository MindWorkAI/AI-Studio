using System.Text.Encodings.Web;
using System.Text.Json;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// The texts of the imprint which closes every presentation, in the language of the presentation.
/// </summary>
/// <remarks>
/// The model translates the texts along with the slides, since the presentation may be in any
/// language, including one only the model knows: the language of the input, or one the user typed
/// in. The labels carry their punctuation, so every language keeps its own.
/// </remarks>
/// <param name="Title">The title of the imprint slide, which also names its section.</param>
/// <param name="TopicLabel">The label of the line naming the topic, i.e., the title of the presentation.</param>
/// <param name="DateLabel">The label of the date.</param>
/// <param name="DatePlaceholder">A sample date, saying the format it expects.</param>
/// <param name="AuthorLabel">The label of the author.</param>
/// <param name="AuthorPlaceholder">A sample name of the author.</param>
/// <param name="InstituteLabel">The label of the institute.</param>
/// <param name="PictureCreditsLabel">The label of the picture credits.</param>
/// <param name="PictureCreditsPlaceholder">A sample of the picture credits; a line break continues it in its column.</param>
internal sealed record ImprintTexts(
    string Title,
    string TopicLabel,
    string DateLabel,
    string DatePlaceholder,
    string AuthorLabel,
    string AuthorPlaceholder,
    string InstituteLabel,
    string PictureCreditsLabel,
    string PictureCreditsPlaceholder)
{
    /// <summary>
    /// The imprint as the corporate template states it. It shows the model what to translate, and
    /// it stands in whenever the model's answer lacks a translation.
    /// </summary>
    public static readonly ImprintTexts GERMAN = new(
        "Impressum",
        "Thema:",
        "Datum:",
        "2023-01-01 (JJJJ-MM-TT)",
        "Autor:",
        "Vorname Nachname",
        "Institut:",
        "Bildcredits:",
        "Alle Bilder „Urheber (Lizenz)“, sofern nicht anders angegeben");

    /// <summary>
    /// The texts as the JSON object the model writes, with the field names it uses.
    /// </summary>
    /// <remarks>
    /// The quotation marks of a language stay as they are, rather than becoming escape sequences
    /// the model might copy.
    /// </remarks>
    /// <returns>The object on a single line, e.g., <c>{ "title": "Impressum", "topic_label": "Thema:", … }</c>.</returns>
    public string ToPromptJson()
    {
        (string Name, string Value)[] fields =
        [
            ("title", this.Title),
            ("topic_label", this.TopicLabel),
            ("date_label", this.DateLabel),
            ("date_placeholder", this.DatePlaceholder),
            ("author_label", this.AuthorLabel),
            ("author_placeholder", this.AuthorPlaceholder),
            ("institute_label", this.InstituteLabel),
            ("picture_credits_label", this.PictureCreditsLabel),
            ("picture_credits_placeholder", this.PictureCreditsPlaceholder),
        ];

        return $"{{ {string.Join(", ", fields.Select(field => $"\"{field.Name}\": \"{JsonEncodedText.Encode(field.Value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\""))} }}";
    }
}