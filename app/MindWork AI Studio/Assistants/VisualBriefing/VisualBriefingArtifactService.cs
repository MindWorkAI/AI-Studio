using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AIStudio.Tools.Metadata;

using HtmlAgilityPack;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingArtifactService</c> for the visual briefing feature.
/// </summary>
public sealed partial class VisualBriefingArtifactService
{
    /// <summary>
    /// Marks the Base64 artifact header embedded at the start of standalone HTML.
    /// </summary>
    private const string HEADER_MARKER = "MWAI_VISUAL_BRIEFING_HEADER:";

    /// <summary>
    /// Breaks the circular dependency while hashing a document that carries its own hash.
    /// </summary>
    private const string DOCUMENT_HASH_PLACEHOLDER = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>
    /// Identifies the canonical JSON script element.
    /// </summary>
    private const string DATA_ELEMENT_ID = "mwai-briefing-data";

    /// <summary>
    /// Gets the frozen JSON configuration whose bytes the document hash covers.
    /// </summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = VisualBriefingJson.Canonical;

    /// <summary>
    /// Defines <c>HtmlLanguageTagRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex HTML_LANGUAGE_TAG = HtmlLanguageTagRegex();

    /// <summary>
    /// Lazily loads the pinned ECharts common distribution.
    /// </summary>
    private static readonly Lazy<string?> ECHARTS_SCRIPT = new(LoadECharts);

    /// <summary>
    /// Defines <c>AIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    private string AIStudioVersion { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown";

    /// <summary>
    /// Defines <c>RuntimeScript</c> for the visual briefing feature.
    /// </summary>
    private string RuntimeScript => BuildRuntimeScript(this.AIStudioVersion);

    /// <summary>
    /// Defines <c>NormalizeTemplate</c> for the visual briefing feature.
    /// </summary>
    private static string NormalizeTemplate(string template) => template.Trim().Replace("\r\n", "\n", StringComparison.Ordinal);

    // HtmlAgilityPack's public annotations declare these lookup APIs as non-null even though
    // they return null for missing nodes and attributes. Keep that behavior explicit here.
    // ReSharper disable once ReturnTypeCanBeNotNullable
    /// <summary>
    /// Defines <c>FindElementById</c> for the visual briefing feature.
    /// </summary>
    private static HtmlNode? FindElementById(HtmlDocument document, string id) => document.GetElementbyId(id);

    // ReSharper disable once ReturnTypeCanBeNotNullable
    /// <summary>
    /// Defines <c>FindNode</c> for the visual briefing feature.
    /// </summary>
    private static HtmlNode? FindNode(HtmlNode node, string xpath) => node.SelectSingleNode(xpath);

    // ReSharper disable once ReturnTypeCanBeNotNullable
    /// <summary>
    /// Defines <c>FindNodes</c> for the visual briefing feature.
    /// </summary>
    private static HtmlNodeCollection? FindNodes(HtmlNode node, string xpath) => node.SelectNodes(xpath);

    // ReSharper disable once ReturnTypeCanBeNotNullable
    /// <summary>
    /// Defines <c>FindAttribute</c> for the visual briefing feature.
    /// </summary>
    private static HtmlAttribute? FindAttribute(HtmlNode node, string name) => node.Attributes[name];

    /// <summary>
    /// Defines <c>CanonicalizeTemplate</c> for the visual briefing feature.
    /// </summary>
    private static string CanonicalizeTemplate(string template)
    {
        var document = new HtmlDocument();
        document.LoadHtml($"<div id=\"mwai-canonical-root\">{NormalizeTemplate(template)}</div>");
        return NormalizeTemplate(FindElementById(document, "mwai-canonical-root")?.InnerHtml ?? string.Empty);
    }

    /// <summary>
    /// Defines <c>GetHtmlLanguage</c> for the visual briefing feature.
    /// </summary>
    private static string GetHtmlLanguage(CommonLanguages language, string customLanguage) => language switch
    {
        CommonLanguages.DE_DE => "de-DE",
        CommonLanguages.DE_AT => "de-AT",
        CommonLanguages.DE_CH => "de-CH",
        CommonLanguages.ZH_CN => "zh-CN",
        CommonLanguages.HI_IN => "hi-IN",
        CommonLanguages.ES_ES => "es-ES",
        CommonLanguages.FR_FR => "fr-FR",
        CommonLanguages.JA_JP => "ja-JP",
        CommonLanguages.RU_RU => "ru-RU",
        CommonLanguages.EN_GB => "en-GB",
        CommonLanguages.EN_US => "en-US",
        CommonLanguages.OTHER when HTML_LANGUAGE_TAG.IsMatch(customLanguage.Trim()) => customLanguage.Trim(),
        _ => "und",
    };

    /// <summary>
    /// Defines <c>LoadECharts</c> for the visual briefing feature.
    /// </summary>
    private static string? LoadECharts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("Assistants.VisualBriefing.Runtime.echarts.common.min.js", StringComparison.Ordinal));
        if (resourceName is null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Defines <c>HtmlLanguageTagRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlLanguageTagRegex();
}