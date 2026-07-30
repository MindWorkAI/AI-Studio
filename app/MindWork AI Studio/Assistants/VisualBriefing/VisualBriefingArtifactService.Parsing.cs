using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingArtifactService
{
    /// <summary>
    /// Defines <c>ManifestRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex MANIFEST_REGEX = ManifestRegex();

    /// <summary>
    /// Defines <c>ManifestRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex("<!--MWAI_VISUAL_BRIEFING_MANIFEST:(?<value>[A-Za-z0-9+/=]+)-->", RegexOptions.CultureInvariant)]
    private static partial Regex ManifestRegex();

    /// <summary>
    /// Defines <c>StyleRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex STYLE_REGEX = StyleRegex();

    /// <summary>
    /// Defines <c>StyleRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex("""<style\s+id="mwai-briefing-style">(?<value>[\s\S]*?)</style>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleRegex();

    /// <summary>
    /// Defines <c>RuntimeRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex RUNTIME_REGEX = RuntimeRegex();

    /// <summary>
    /// Defines <c>RuntimeRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex("""<script\s+id="mwai-briefing-runtime">(?<value>[\s\S]*?)</script>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeRegex();

    /// <summary>
    /// Defines <c>EChartsRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex ECHARTS_REGEX = EChartsRegex();

    /// <summary>
    /// Defines <c>EChartsRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex("""<script\s+id="mwai-echarts-runtime">(?<value>[\s\S]*?)</script>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EChartsRegex();

    /// <summary>
    /// Defines <c>TryParse</c> for the visual briefing feature.
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

        if (!html.StartsWith("<!doctype html>\n", StringComparison.Ordinal) ||
            !html.EndsWith("</html>", StringComparison.Ordinal))
        {
            issue = "The briefing document wrapper is invalid or modified.";
            return false;
        }

        var manifestMatch = MANIFEST_REGEX.Match(html);
        if (!manifestMatch.Success)
        {
            issue = "The briefing compatibility manifest is missing.";
            return false;
        }

        VisualBriefingExportManifest? exportManifest;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(manifestMatch.Groups["value"].Value));
            using var manifestDocument = JsonDocument.Parse(json);
            exportManifest = HasDuplicateProperties(manifestDocument.RootElement)
                ? null
                : manifestDocument.RootElement.Deserialize<VisualBriefingExportManifest>(JSON_OPTIONS);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            issue = "The briefing compatibility manifest is invalid.";
            return false;
        }

        if (exportManifest is null ||
            exportManifest.ArtifactVersion != VisualBriefingVersions.ARTIFACT ||
            exportManifest.SchemaVersion != VisualBriefingVersions.SCHEMA ||
            exportManifest.RuntimeVersion != VisualBriefingVersions.RUNTIME ||
            exportManifest.BriefingId == Guid.Empty ||
            exportManifest.RevisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(exportManifest.Name) ||
            string.IsNullOrWhiteSpace(exportManifest.AIStudioVersion) ||
            string.IsNullOrWhiteSpace(exportManifest.RuntimeAIStudioVersion) ||
            string.IsNullOrWhiteSpace(exportManifest.PayloadHash) ||
            exportManifest.PayloadHash.Length != 64 ||
            !exportManifest.PayloadHash.All(Uri.IsHexDigit) ||
            exportManifest.TargetLanguage is CommonLanguages.OTHER &&
            string.IsNullOrWhiteSpace(exportManifest.CustomTargetLanguage) ||
            exportManifest.ProtectionLevel is VisualBriefingProtectionLevel.OTHER &&
            string.IsNullOrWhiteSpace(exportManifest.CustomProtectionLevel))
        {
            issue = "The briefing uses an unsupported or invalid artifact version.";
            return false;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);
        
        var dataNode = FindElementById(document, DATA_ELEMENT_ID);
        var rootNode = FindElementById(document, "mwai-briefing-root");
        var footerNode = FindElementById(document, "mwai-static-footer");
        var headNode = FindNode(document.DocumentNode, "//head");
        var bodyNode = FindNode(document.DocumentNode, "//body");
        var htmlNode = FindNode(document.DocumentNode, "//html");
        var styleMatch = STYLE_REGEX.Match(html);
        var runtimeMatch = RUNTIME_REGEX.Match(html);
        
        if (dataNode is null || rootNode is null || footerNode is null || headNode is null || bodyNode is null ||
            htmlNode is null || !styleMatch.Success || !runtimeMatch.Success)
        {
            issue = "The briefing structure is incomplete.";
            return false;
        }

        var headChildren = headNode.ChildNodes.Where(node => node.NodeType is HtmlNodeType.Element).ToArray();
        var metaNodes = headChildren.Where(node => node.Name.Equals("meta", StringComparison.OrdinalIgnoreCase)).ToArray();
        var styleNodes = headChildren.Where(node => node.Name.Equals("style", StringComparison.OrdinalIgnoreCase)).ToArray();
        var titleNodes = headChildren.Where(node => node.Name.Equals("title", StringComparison.OrdinalIgnoreCase)).ToArray();
        
        if (headChildren.Length != 6 ||
            metaNodes.Length != 4 ||
            styleNodes.Length != 1 ||
            titleNodes.Length != 1 ||
            metaNodes.Count(node => string.Equals(node.GetAttributeValue("charset", string.Empty), "utf-8", StringComparison.OrdinalIgnoreCase)) != 1 ||
            metaNodes.Count(node => string.Equals(node.GetAttributeValue("name", string.Empty), "viewport", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(node.GetAttributeValue("content", string.Empty), "width=device-width,initial-scale=1", StringComparison.Ordinal)) != 1 ||
            metaNodes.Count(node => string.Equals(node.GetAttributeValue("http-equiv", string.Empty), "Content-Security-Policy", StringComparison.OrdinalIgnoreCase)) != 1 ||
            metaNodes.Count(node => string.Equals(node.GetAttributeValue("name", string.Empty), "referrer", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(node.GetAttributeValue("content", string.Empty), "no-referrer", StringComparison.OrdinalIgnoreCase)) != 1 ||
            metaNodes.Any(node => FindAttribute(node, "charset") is not null
                ? !HasExactAttributes(node, "charset")
                : !HasExactAttributes(node, FindAttribute(node, "http-equiv") is not null ? "http-equiv" : "name", "content")) ||
            styleNodes[0].Id != "mwai-briefing-style" ||
            !HasExactAttributes(styleNodes[0], "id") ||
            !HasExactAttributes(titleNodes[0]) ||
            !string.Equals(titleNodes[0].InnerText, exportManifest.Name, StringComparison.Ordinal) ||
            !HasExactAttributes(headNode) ||
            !HasExactAttributes(bodyNode) ||
            !HasExactAttributes(htmlNode, "lang") ||
            !string.Equals(
                htmlNode.GetAttributeValue("lang", string.Empty),
                GetHtmlLanguage(exportManifest.TargetLanguage, exportManifest.CustomTargetLanguage),
                StringComparison.Ordinal))
        {
            issue = "The briefing head or document structure was modified.";
            return false;
        }

        var bodyChildren = FindNodes(document.DocumentNode, "//body/*")?.ToArray() ?? [];
        var bodyComments = bodyNode.ChildNodes.Where(node => node.NodeType is HtmlNodeType.Comment).ToArray();
        var allowedBodyIds = new HashSet<string>(StringComparer.Ordinal)
        {
            DATA_ELEMENT_ID,
            "mwai-briefing-root",
            "mwai-static-footer",
            "mwai-echarts-runtime",
            "mwai-briefing-runtime",
        };
        
        if (bodyChildren.Any(node => !allowedBodyIds.Contains(node.Id)) ||
            bodyChildren.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() != bodyChildren.Length ||
            bodyComments.Length != 1 ||
            !string.Equals(
                bodyComments[0].OuterHtml,
                $"<!--{MANIFEST_MARKER}{manifestMatch.Groups["value"].Value}-->",
                StringComparison.Ordinal) ||
            bodyNode.ChildNodes.Any(node =>
                node.NodeType is HtmlNodeType.Text && !string.IsNullOrWhiteSpace(node.InnerText)) ||
            CanonicalizeTemplate(footerNode.InnerHtml) != CanonicalizeTemplate(STATIC_FOOTER_TEMPLATE) ||
            !HasExactAttributes(dataNode, "id", "type") ||
            !HasExactAttributes(rootNode, "id") ||
            !HasExactAttributes(footerNode, "id", "class") ||
            !string.Equals(footerNode.GetAttributeValue("class", string.Empty), "mwai-footer", StringComparison.Ordinal))
        {
            issue = "The briefing body or static footer structure was modified.";
            return false;
        }

        var scriptNodes = FindNodes(document.DocumentNode, "//script")?.ToArray() ?? [];
        if (scriptNodes.Any(node => node.Id is not DATA_ELEMENT_ID and not "mwai-echarts-runtime" and not "mwai-briefing-runtime") ||
            scriptNodes.Count(node => node.Id == DATA_ELEMENT_ID) != 1 ||
            scriptNodes.Count(node => node.Id == "mwai-briefing-runtime") != 1 ||
            scriptNodes.Any(node => node.Id != DATA_ELEMENT_ID && !HasExactAttributes(node, "id")) ||
            !string.Equals(dataNode.GetAttributeValue("type", string.Empty), "application/json", StringComparison.OrdinalIgnoreCase))
        {
            issue = "The briefing contains an unknown or duplicated script element.";
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

        var protectedDataIssue = ValidateProtectedData(exportManifest, data);
        if (!string.IsNullOrEmpty(protectedDataIssue))
        {
            issue = protectedDataIssue;
            return false;
        }

        var template = CanonicalizeTemplate(rootNode.InnerHtml);
        var combinedCss = styleMatch.Groups["value"].Value.Trim();
        const string PROTECTED_CSS_SUFFIX = $"\n{PROTECTED_FOOTER_CSS}";
        
        if (!combinedCss.EndsWith(PROTECTED_CSS_SUFFIX, StringComparison.Ordinal))
        {
            issue = "The protected briefing footer stylesheet is missing or modified.";
            return false;
        }
        
        var css = combinedCss[..^PROTECTED_CSS_SUFFIX.Length].Trim();
        var runtime = runtimeMatch.Groups["value"].Value;
        var echartsMatch = ECHARTS_REGEX.Match(html);
        var echarts = echartsMatch.Success ? echartsMatch.Groups["value"].Value : null;
        
        if (echarts is not null && !string.Equals(echarts, ECHARTS_SCRIPT.Value, StringComparison.Ordinal))
        {
            issue = "The briefing contains an unknown or modified ECharts runtime.";
            return false;
        }
        
        var validationIssue = ValidateGeneratedParts(null, data, template, css, !string.IsNullOrWhiteSpace(echarts));
        if (!string.IsNullOrEmpty(validationIssue))
        {
            issue = validationIssue;
            return false;
        }
        
        if (!string.Equals(runtime, BuildRuntimeScript(exportManifest.RuntimeAIStudioVersion), StringComparison.Ordinal))
        {
            issue = "The briefing contains an unknown or modified AI Studio runtime.";
            return false;
        }

        var dataJson = JsonSerializer.Serialize(data, JSON_OPTIONS);
        var payloadHash = ComputePayloadHash(dataJson, template, css, runtime, echarts);
        
        if (!string.Equals(payloadHash, exportManifest.PayloadHash, StringComparison.OrdinalIgnoreCase))
        {
            issue = "The briefing payload hash does not match its manifest.";
            return false;
        }

        var expectedCsp = GetContentSecurityPolicy(new(exportManifest, data, template, css, runtime, echarts, payloadHash));
        var actualCsp = FindNode(document.DocumentNode, "//meta[@http-equiv='Content-Security-Policy']")
            ?.GetAttributeValue("content", string.Empty);
        
        if (!string.Equals(actualCsp, expectedCsp, StringComparison.Ordinal))
        {
            issue = "The briefing Content Security Policy is missing or modified.";
            return false;
        }

        parts = new(exportManifest, data, template, css, runtime, echarts, payloadHash);
        return true;
    }

    /// <summary>
    /// Defines <c>HasExactAttributes</c> for the visual briefing feature.
    /// </summary>
    private static bool HasExactAttributes(HtmlNode node, params string[] expectedNames)
    {
        if (node.Attributes.Count != expectedNames.Length)
            return false;

        return expectedNames.All(expectedName =>
            node.Attributes.Any(attribute => attribute.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Defines <c>ValidateProtectedData</c> for the visual briefing feature.
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
            parsedRuntimeVersion != VisualBriefingVersions.RUNTIME ||
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