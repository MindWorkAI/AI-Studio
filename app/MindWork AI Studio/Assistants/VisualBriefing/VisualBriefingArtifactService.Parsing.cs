using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingArtifactService
{
    /// <summary>
    /// Matches the version-independent artifact header at the start of standalone HTML.
    /// </summary>
    private static readonly Regex HEADER_REGEX = HeaderRegex();

    /// <summary>
    /// Matches the version-independent artifact header at the start of standalone HTML.
    /// </summary>
    [GeneratedRegex(@"\A<!doctype html>\n<!--MWAI_VISUAL_BRIEFING_HEADER:(?<value>[A-Za-z0-9+/=]+)-->\n", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderRegex();

    /// <summary>
    /// Matches the generated presentation stylesheet.
    /// </summary>
    private static readonly Regex STYLE_REGEX = StyleRegex();

    /// <summary>
    /// Matches the generated presentation stylesheet.
    /// </summary>
    [GeneratedRegex("""<style\s+id="mwai-briefing-style">(?<value>[\s\S]*?)</style>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleRegex();

    /// <summary>
    /// Matches the embedded declarative runtime.
    /// </summary>
    private static readonly Regex RUNTIME_REGEX = RuntimeRegex();

    /// <summary>
    /// Matches the embedded declarative runtime.
    /// </summary>
    [GeneratedRegex("""<script\s+id="mwai-briefing-runtime">(?<value>[\s\S]*?)</script>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeRegex();

    /// <summary>
    /// Matches the optional embedded chart runtime.
    /// </summary>
    private static readonly Regex ECHARTS_REGEX = EChartsRegex();

    /// <summary>
    /// Matches the optional embedded chart runtime.
    /// </summary>
    [GeneratedRegex("""<script\s+id="mwai-echarts-runtime">(?<value>[\s\S]*?)</script>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EChartsRegex();

    /// <summary>
    /// Reads an intact standalone artifact without applying current compiler or runtime rules.
    /// </summary>
    public static bool TryParse(string html, out VisualBriefingArtifactParts parts, out string issue)
    {
        parts = null!;
        issue = string.Empty;

        if (string.IsNullOrWhiteSpace(html))
        {
            issue = "The briefing file is empty.";
            return false;
        }

        if (!html.EndsWith("</html>", StringComparison.Ordinal))
        {
            issue = "The briefing document wrapper is invalid or incomplete.";
            return false;
        }

        var headerMatch = HEADER_REGEX.Match(html);
        if (!headerMatch.Success)
        {
            issue = "The briefing artifact header is missing or misplaced.";
            return false;
        }

        VisualBriefingExportManifest? exportManifest;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(headerMatch.Groups["value"].Value));
            using var headerDocument = JsonDocument.Parse(json);
            exportManifest = HasDuplicateProperties(headerDocument.RootElement)
                ? null
                : headerDocument.RootElement.Deserialize<VisualBriefingExportManifest>(JSON_OPTIONS);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            issue = "The briefing artifact header is invalid.";
            return false;
        }

        if (!ValidateHeader(exportManifest, out issue))
            return false;

        var documentHash = exportManifest!.DocumentHash;
        exportManifest.DocumentHash = DOCUMENT_HASH_PLACEHOLDER;
        var placeholderHeader = $"<!doctype html>\n<!--{HEADER_MARKER}{EncodeHeader(exportManifest)}-->\n";
        exportManifest.DocumentHash = documentHash;
        var placeholderDocument = placeholderHeader + html[headerMatch.Length..];
        var computedDocumentHash = VisualBriefingHashing.Compute(placeholderDocument);
        if (!string.Equals(computedDocumentHash, documentHash, StringComparison.OrdinalIgnoreCase))
        {
            issue = "The briefing document hash does not match its contents.";
            return false;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var htmlNode = FindUniqueNode(document, "//html");
        var headNode = FindUniqueNode(document, "//head");
        var bodyNode = FindUniqueNode(document, "//body");
        var dataNode = FindUniqueElementById(document, DATA_ELEMENT_ID);
        var rootNode = FindUniqueElementById(document, "mwai-briefing-root");
        var footerNode = FindUniqueElementById(document, "mwai-static-footer");
        var headerNodes = FindNodes(document.DocumentNode, "//*[@id='mwai-static-header']")?.ToArray() ?? [];
        var generatedStyleNode = FindUniqueElementById(document, "mwai-briefing-style");
        var protectedStyleNode = FindUniqueElementById(document, "mwai-protected-style");
        var runtimeNode = FindUniqueElementById(document, "mwai-briefing-runtime");
        var echartsNode = FindUniqueElementById(document, "mwai-echarts-runtime");
        var styleMatch = STYLE_REGEX.Match(html);
        var runtimeMatch = RUNTIME_REGEX.Match(html);
        var echartsMatch = ECHARTS_REGEX.Match(html);

        if (htmlNode is null || headNode is null || bodyNode is null || dataNode is null || rootNode is null ||
            footerNode is null || generatedStyleNode is null || protectedStyleNode is null || runtimeNode is null ||
            headerNodes.Length > 1 ||
            (headerNodes.Length == 1 &&
             (!headerNodes[0].Name.Equals("header", StringComparison.OrdinalIgnoreCase) || headerNodes[0].ParentNode != bodyNode)) ||
            !styleMatch.Success || !runtimeMatch.Success || (echartsNode is not null) != echartsMatch.Success)
        {
            issue = "The briefing envelope is incomplete or ambiguous.";
            return false;
        }

        var scriptNodes = FindNodes(document.DocumentNode, "//script")?.ToArray() ?? [];
        var styleNodes = FindNodes(document.DocumentNode, "//style")?.ToArray() ?? [];
        if (scriptNodes.Any(node => node.Id is not DATA_ELEMENT_ID and not "mwai-echarts-runtime" and not "mwai-briefing-runtime") ||
            scriptNodes.Count(node => node.Id == DATA_ELEMENT_ID) != 1 ||
            scriptNodes.Count(node => node.Id == "mwai-briefing-runtime") != 1 ||
            scriptNodes.Count(node => node.Id == "mwai-echarts-runtime") > 1 ||
            styleNodes.Length != 2 ||
            styleNodes.Count(node => node.Id == "mwai-briefing-style") != 1 ||
            styleNodes.Count(node => node.Id == "mwai-protected-style") != 1 ||
            !string.Equals(dataNode.GetAttributeValue("type", string.Empty), "application/json", StringComparison.OrdinalIgnoreCase))
        {
            issue = "The briefing contains unknown or duplicated executable resources.";
            return false;
        }

        var bodyChildren = FindNodes(document.DocumentNode, "//body/*")?.ToArray() ?? [];
        var allowedBodyIds = new HashSet<string>(StringComparer.Ordinal)
        {
            DATA_ELEMENT_ID,
            "mwai-static-header",
            "mwai-briefing-root",
            "mwai-static-footer",
            "mwai-echarts-runtime",
            "mwai-briefing-runtime",
        };
        if (bodyChildren.Any(node => !allowedBodyIds.Contains(node.Id)) ||
            bodyChildren.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() != bodyChildren.Length)
        {
            issue = "The briefing body contains elements outside the stable artifact envelope.";
            return false;
        }

        JsonElement data;
        try
        {
            using var parsedData = JsonDocument.Parse(dataNode.InnerText);
            data = parsedData.RootElement.Clone();
        }
        catch (JsonException)
        {
            issue = "The briefing data block is invalid.";
            return false;
        }

        var template = CanonicalizeTemplate(rootNode.InnerHtml);
        var css = styleMatch.Groups["value"].Value.Trim();
        var runtime = runtimeMatch.Groups["value"].Value;
        var echarts = echartsMatch.Success ? echartsMatch.Groups["value"].Value : null;
        parts = new(exportManifest, data, template, css, runtime, echarts, documentHash);

        var cspNodes = FindNodes(document.DocumentNode, "//meta[@http-equiv='Content-Security-Policy']")?.ToArray() ?? [];
        var actualCsp = cspNodes.Length == 1
            ? cspNodes[0].GetAttributeValue("content", string.Empty)
            : string.Empty;
        if (!string.Equals(actualCsp, GetContentSecurityPolicy(parts), StringComparison.Ordinal))
        {
            parts = null!;
            issue = "The briefing Content Security Policy is missing or inconsistent with its embedded scripts.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads an intact artifact and additionally applies the current semantic compiler contract.
    /// </summary>
    internal static bool TryParseForRecompile(string html, out VisualBriefingArtifactParts parts, out string issue)
    {
        if (!TryParse(html, out parts, out issue))
            return false;

        if (parts.ExportManifest.SchemaVersion != VisualBriefingVersions.SCHEMA)
        {
            parts = null!;
            issue = "The briefing data schema is not compatible with the current compiler.";
            return false;
        }

        issue = ValidateProtectedData(parts.ExportManifest, parts.Data);
        if (!string.IsNullOrEmpty(issue))
        {
            parts = null!;
            return false;
        }

        issue = ValidateGeneratedParts(
            null,
            parts.Data,
            parts.TemplateHtml,
            parts.Css,
            !string.IsNullOrWhiteSpace(parts.EChartsScript));
        if (!string.IsNullOrEmpty(issue))
        {
            parts = null!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates stable artifact-header fields without imposing current runtime or schema versions.
    /// </summary>
    private static bool ValidateHeader(VisualBriefingExportManifest? exportManifest, out string issue)
    {
        issue = string.Empty;
        if (exportManifest is null ||
            exportManifest.ArtifactVersion != VisualBriefingVersions.ARTIFACT ||
            exportManifest.SchemaVersion <= 0 ||
            exportManifest.RuntimeVersion <= 0 ||
            exportManifest.BriefingId == Guid.Empty ||
            exportManifest.RevisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(exportManifest.Name) ||
            string.IsNullOrWhiteSpace(exportManifest.AIStudioVersion) ||
            string.IsNullOrWhiteSpace(exportManifest.RuntimeAIStudioVersion) ||
            exportManifest.DocumentHash.Length != 64 ||
            !exportManifest.DocumentHash.All(Uri.IsHexDigit) ||
            exportManifest.TargetLanguage is CommonLanguages.OTHER && string.IsNullOrWhiteSpace(exportManifest.CustomTargetLanguage) ||
            exportManifest.ProtectionLevel is VisualBriefingProtectionLevel.OTHER && string.IsNullOrWhiteSpace(exportManifest.CustomProtectionLevel))
        {
            issue = "The briefing artifact header contains invalid or unsupported metadata.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds exactly one node for an XPath expression.
    /// </summary>
    private static HtmlNode? FindUniqueNode(HtmlDocument document, string xpath)
    {
        var nodes = FindNodes(document.DocumentNode, xpath)?.ToArray() ?? [];
        return nodes.Length == 1 ? nodes[0] : null;
    }

    /// <summary>
    /// Finds exactly one element by ID.
    /// </summary>
    private static HtmlNode? FindUniqueElementById(HtmlDocument document, string id)
    {
        var nodes = FindNodes(document.DocumentNode, $"//*[@id='{id}']")?.ToArray() ?? [];
        return nodes.Length == 1 ? nodes[0] : null;
    }

    /// <summary>
    /// Validates current protected data needed for recompilation.
    /// </summary>
    private static string ValidateProtectedData(VisualBriefingExportManifest exportManifest, JsonElement data)
    {
        if (!data.TryGetProperty("_mwai", out var protectedData) ||
            protectedData.ValueKind is not JsonValueKind.Object ||
            !protectedData.TryGetProperty("schemaVersion", out var schemaVersion) ||
            schemaVersion.ValueKind is not JsonValueKind.Number ||
            !schemaVersion.TryGetInt32(out var parsedSchemaVersion) ||
            parsedSchemaVersion != VisualBriefingVersions.SCHEMA ||
            !protectedData.TryGetProperty("runtimeVersion", out var runtimeVersion) ||
            runtimeVersion.ValueKind is not JsonValueKind.Number ||
            !runtimeVersion.TryGetInt32(out var parsedRuntimeVersion) ||
            parsedRuntimeVersion != exportManifest.RuntimeVersion ||
            !protectedData.TryGetProperty("aiStudioVersion", out var aiStudioVersion) ||
            aiStudioVersion.ValueKind is not JsonValueKind.String ||
            !string.Equals(aiStudioVersion.GetString(), exportManifest.AIStudioVersion, StringComparison.Ordinal) ||
            !protectedData.TryGetProperty("assets", out var protectedAssets) ||
            protectedAssets.ValueKind is not JsonValueKind.Object ||
            data.TryGetProperty("assets", out _))
            return "The protected briefing data block is incomplete or inconsistent.";

        var protectedAssetProperties = protectedAssets.EnumerateObject().ToArray();
        if (protectedAssetProperties.Any(property =>
                property.Value.ValueKind is not JsonValueKind.String ||
                !property.Value.GetString()!.StartsWith("data:image/", StringComparison.Ordinal)) ||
            protectedAssetProperties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != protectedAssetProperties.Length)
            return "The protected embedded asset map contains invalid or duplicated entries.";

        if (!protectedData.TryGetProperty("assetMetadata", out var assetMetadata) ||
            assetMetadata.ValueKind is not JsonValueKind.Object)
            return "The protected visual asset metadata is missing.";

        var metadataProperties = assetMetadata.EnumerateObject().ToArray();
        if (metadataProperties.Length != protectedAssetProperties.Length ||
            metadataProperties.Any(property =>
                !protectedAssets.TryGetProperty(property.Name, out _) ||
                property.Value.ValueKind is not JsonValueKind.Object ||
                !property.Value.TryGetProperty("description", out var description) ||
                description.ValueKind is not JsonValueKind.String ||
                string.IsNullOrWhiteSpace(description.GetString()) ||
                !property.Value.TryGetProperty("altText", out var altText) ||
                altText.ValueKind is not JsonValueKind.String ||
                string.IsNullOrWhiteSpace(altText.GetString())))
            return "The protected visual asset metadata is invalid or incomplete.";

        if (!protectedData.TryGetProperty("footer", out var footer) ||
            footer.ValueKind is not JsonValueKind.Object)
            return "The protected briefing footer data is missing.";

        string[] footerFields = ["createdWith", "models", "createdAt", "authors", "protection"];
        return footerFields.Any(field =>
            !footer.TryGetProperty(field, out var value) ||
            value.ValueKind is not JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            ? "The protected briefing footer data is incomplete."
            : string.Empty;
    }
}