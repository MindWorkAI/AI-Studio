using System.Reflection;
using System.Security.Cryptography;
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
    /// Marks the Base64 compatibility manifest embedded in standalone HTML.
    /// </summary>
    private const string MANIFEST_MARKER = "MWAI_VISUAL_BRIEFING_MANIFEST:";

    /// <summary>
    /// Identifies the canonical JSON script element.
    /// </summary>
    private const string DATA_ELEMENT_ID = "mwai-briefing-data";

    /// <summary>
    /// Gets the shared compact JSON configuration.
    /// </summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = VisualBriefingJson.Compact;

    /// <summary>
    /// Lists declarative elements allowed in model-generated templates.
    /// </summary>
    private static readonly HashSet<string> ALLOWED_ELEMENTS = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "article", "aside", "button", "canvas", "caption", "dd", "details", "div", "dl", "dt",
        "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "img",
        "input", "label", "li", "main", "nav", "ol", "option", "p", "progress", "section", "select",
        "small", "span", "strong", "summary", "table", "tbody", "td", "template", "tfoot", "th",
        "thead", "tr", "ul",
    };

    /// <summary>
    /// Lists ordinary attributes allowed in model-generated templates.
    /// </summary>
    private static readonly HashSet<string> ALLOWED_ATTRIBUTES = new(StringComparer.OrdinalIgnoreCase)
    {
        "aria-atomic", "aria-controls", "aria-describedby", "aria-expanded", "aria-hidden", "aria-label",
        "aria-labelledby", "aria-live", "aria-selected", "class", "colspan", "disabled", "for", "height",
        "hidden", "href", "id", "max", "min", "name", "open", "placeholder", "role", "rowspan", "scope", "step",
        "tabindex", "type", "value", "width",
    };

    /// <summary>
    /// Lists supported AI Studio runtime bindings.
    /// </summary>
    private static readonly HashSet<string> ALLOWED_DATA_ATTRIBUTES = new(StringComparer.OrdinalIgnoreCase)
    {
        "data-mwai-asset", "data-mwai-chart", "data-mwai-direction", "data-mwai-each", "data-mwai-expr",
        "data-mwai-filter", "data-mwai-filter-value", "data-mwai-if", "data-mwai-model", "data-mwai-reset",
        "data-mwai-region", "data-mwai-search", "data-mwai-set", "data-mwai-sort", "data-mwai-tab-panel", "data-mwai-tab-target",
        "data-mwai-tabs", "data-mwai-text", "data-mwai-toggle", "data-mwai-value",
    };

    /// <summary>
    /// Lists bindings whose values are canonical data paths.
    /// </summary>
    private static readonly HashSet<string> PATH_BINDINGS = new(StringComparer.OrdinalIgnoreCase)
    {
        "data-mwai-chart", "data-mwai-each", "data-mwai-expr", "data-mwai-filter", "data-mwai-filter-value",
        "data-mwai-if", "data-mwai-model", "data-mwai-set", "data-mwai-text", "data-mwai-toggle",
    };

    /// <summary>
    /// Lists supported safe formula operators.
    /// </summary>
    private static readonly HashSet<string> FORMULA_OPERATORS = new(StringComparer.Ordinal)
    {
        "add", "subtract", "multiply", "divide", "power", "eq", "ne", "gt", "gte", "lt", "lte", "if",
        "min", "max", "round", "sqrt", "log", "exp",
    };
    
    /// <summary>
    /// Defines <c>CssProhibitedRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex CSS_PROHIBITED = CssProhibitedRegex();
    
    /// <summary>
    /// Defines <c>CssProtectedTargetRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex CSS_PROTECTED_TARGET = CssProtectedTargetRegex();
    
    /// <summary>
    /// Defines <c>DataPathRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex DATA_PATH = DataPathRegex();
    
    /// <summary>
    /// Defines <c>LocalDataPathRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex LOCAL_DATA_PATH = LocalDataPathRegex();
    
    /// <summary>
    /// Defines <c>HtmlLanguageTagRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex HTML_LANGUAGE_TAG = HtmlLanguageTagRegex();
    
    /// <summary>
    /// Defines <c>SafeSelectorRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex SAFE_SELECTOR = SafeSelectorRegex();
    
    /// <summary>
    /// Defines <c>ManifestRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex MANIFEST_REGEX = ManifestRegex();
    
    /// <summary>
    /// Defines <c>StyleRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex STYLE_REGEX = StyleRegex();
    
    /// <summary>
    /// Defines <c>RuntimeRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex RUNTIME_REGEX = RuntimeRegex();
    
    /// <summary>
    /// Defines <c>RuntimeAIVersionRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex RUNTIME_AI_VERSION_REGEX = RuntimeAIVersionRegex();
    
    /// <summary>
    /// Defines <c>EChartsRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex ECHARTS_REGEX = EChartsRegex();

    /// <summary>
    /// Lazily loads the pinned ECharts common distribution.
    /// </summary>
    private static readonly Lazy<string?> ECHARTS_SCRIPT = new(LoadECharts);

    /// <summary>
    /// Defines the protected, app-owned static footer template.
    /// </summary>
    private const string STATIC_FOOTER_TEMPLATE = """
                                                  <span data-mwai-text="_mwai.footer.createdWith"></span>
                                                  <span data-mwai-text="_mwai.footer.models"></span>
                                                  <span data-mwai-text="_mwai.footer.createdAt"></span>
                                                  <span data-mwai-text="_mwai.footer.authors"></span>
                                                  <span data-mwai-text="_mwai.footer.protection"></span>
                                                  """;

    /// <summary>
    /// Defines protected footer styles that model CSS cannot override.
    /// </summary>
    private const string PROTECTED_FOOTER_CSS = """
                                                  #mwai-static-footer {
                                                    display: flex !important;
                                                    flex-wrap: wrap !important;
                                                    gap: .5rem 1.25rem !important;
                                                    position: relative !important;
                                                    z-index: 2147483647 !important;
                                                    visibility: visible !important;
                                                    opacity: 1 !important;
                                                    padding: 1rem !important;
                                                    font: 13px/1.5 system-ui, sans-serif !important;
                                                  }
                                                  #mwai-static-footer span {
                                                    display: inline !important;
                                                    visibility: visible !important;
                                                    opacity: 1 !important;
                                                  }
                                                  """;

    /// <summary>
    /// Defines <c>AIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    private string AIStudioVersion { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown";

    /// <summary>
    /// Defines <c>RuntimeScript</c> for the visual briefing feature.
    /// </summary>
    private string RuntimeScript => BuildRuntimeScript(this.AIStudioVersion);

    /// <summary>
    /// Defines <c>GetContentSecurityPolicy</c> for the visual briefing feature.
    /// </summary>
    public static string GetContentSecurityPolicy(VisualBriefingArtifactParts parts)
    {
        var echartsHash = string.IsNullOrWhiteSpace(parts.EChartsScript)
            ? string.Empty
            : $" {ScriptCspHash(parts.EChartsScript)}";
        
        return $"default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src {ScriptCspHash(parts.RuntimeScript)}{echartsHash}; font-src 'none'; media-src 'none'; frame-src 'none'; connect-src 'none'; form-action 'none'; base-uri 'none'; object-src 'none'; frame-ancestors 'self'";
    }

    /// <summary>
    /// Defines <c>BuildAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<string> BuildAsync(
        VisualBriefingManifest manifest,
        VisualBriefingRevisionRequest request,
        string? lockedRuntimeScript = null,
        string? lockedEChartsScript = null,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var data = AddProtectedArtifactData(manifest, request);
        var usesCharts = ContainsChartBinding(request.TemplateHtml);
        var validationIssue = ValidateGeneratedParts(manifest, data, request.TemplateHtml, request.Css, usesCharts);
        
        if (!string.IsNullOrEmpty(validationIssue))
            throw new InvalidDataException(validationIssue);

        var dataJson = JsonSerializer.Serialize(data, JSON_OPTIONS);
        var template = CanonicalizeTemplate(request.TemplateHtml);
        var css = request.Css.Trim();
        var runtime = lockedRuntimeScript ?? this.RuntimeScript;
        
        var runtimeAIStudioVersion = ExtractRuntimeAIStudioVersion(runtime)
            ?? throw new InvalidDataException("The AI Studio runtime does not contain a valid originating app version.");
        
        var echarts = usesCharts ? lockedEChartsScript ?? ECHARTS_SCRIPT.Value : null;
        if (usesCharts && string.IsNullOrWhiteSpace(echarts))
            throw new InvalidOperationException("Apache ECharts 6.1.0 common is not available in this AI Studio build.");
        
        await Task.CompletedTask;

        var payloadHash = ComputePayloadHash(dataJson, template, css, runtime, echarts);
        
        var exportManifest = CreateExportManifest(
            manifest,
            request,
            payloadHash,
            this.AIStudioVersion,
            runtimeAIStudioVersion);
        
        var encodedManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(exportManifest, JSON_OPTIONS)));
        var csp = GetContentSecurityPolicy(new(exportManifest, data, template, css, runtime, echarts, payloadHash));

        return $"""
                <!doctype html>
                <html lang="{GetHtmlLanguage(manifest.Settings)}">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width,initial-scale=1">
                <meta http-equiv="Content-Security-Policy" content="{csp}">
                <meta name="referrer" content="no-referrer">
                <title>{HtmlEncode(manifest.Name)}</title>
                <style id="mwai-briefing-style">{css}
                {PROTECTED_FOOTER_CSS}</style>
                </head>
                <body>
                <!--{MANIFEST_MARKER}{encodedManifest}-->
                <script id="{DATA_ELEMENT_ID}" type="application/json">{dataJson}</script>
                <div id="mwai-briefing-root">{template}</div>
                <footer id="mwai-static-footer" class="mwai-footer">
                {STATIC_FOOTER_TEMPLATE}
                </footer>
                {BuildScriptTag(echarts, "mwai-echarts-runtime")}
                <script id="mwai-briefing-runtime">{runtime}</script>
                </body>
                </html>
                """;
    }

    /// <summary>
    /// Defines <c>TryParse</c> for the visual briefing feature.
    /// </summary>
    public bool TryParse(string html, out VisualBriefingArtifactParts parts, out string issue)
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
    /// Defines <c>ValidateGeneratedParts</c> for the visual briefing feature.
    /// </summary>
    public static string ValidateGeneratedParts(
        VisualBriefingManifest? manifest,
        JsonElement data,
        string templateHtml,
        string css,
        bool usesCharts)
    {
        if (data.ValueKind is not JsonValueKind.Object)
            return "The briefing data block must be one JSON object.";

        if (HasDuplicateProperties(data))
            return "The briefing data block contains duplicated JSON property names.";

        if (HasUnsafePropertyNames(data))
            return "The briefing data block contains an unsafe JSON property name.";

        if (ContainsLocalOrInternalValue(data, manifest))
            return "The briefing data block contains a local path or an internal project reference.";

        if (string.IsNullOrWhiteSpace(templateHtml))
            return "The briefing template is empty.";

        if (CSS_PROHIBITED.IsMatch(css) ||
            CSS_PROTECTED_TARGET.IsMatch(css) ||
            css.Contains("</style", StringComparison.OrdinalIgnoreCase))
            return "The briefing CSS contains an external or unsafe construct.";

        var document = new HtmlDocument();
        document.LoadHtml($"<div id=\"validation-root\">{templateHtml}</div>");
        
        var root = FindElementById(document, "validation-root");
        if (root is null)
            return "The briefing template could not be parsed.";

        var elementIds = root.Descendants()
            .Where(node => node.NodeType is HtmlNodeType.Element)
            .Select(node => node.GetAttributeValue("id", string.Empty))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        
        if (elementIds.Any(id => id.StartsWith("mwai-", StringComparison.OrdinalIgnoreCase)) ||
            elementIds.Distinct(StringComparer.Ordinal).Count() != elementIds.Length)
            return "The briefing template contains a reserved or duplicated element ID.";

        foreach (var node in root.Descendants())
        {
            if (node.NodeType is HtmlNodeType.Comment)
                return "Briefing template HTML comments are not allowed.";

            if (node.NodeType is HtmlNodeType.Text)
            {
                if (!string.IsNullOrWhiteSpace(node.InnerText))
                    return "All visible model-generated text must use a data-mwai binding.";

                continue;
            }

            if (node.NodeType is not HtmlNodeType.Element)
                continue;

            if (!ALLOWED_ELEMENTS.Contains(node.Name))
                return $"The briefing template contains the prohibited element '{node.Name}'.";

            foreach (var attribute in node.Attributes)
            {
                if (attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
                    !ALLOWED_ATTRIBUTES.Contains(attribute.Name) && !attribute.Name.StartsWith("data-mwai-", StringComparison.OrdinalIgnoreCase))
                    return $"The briefing template contains the prohibited attribute '{attribute.Name}'.";

                if (attribute.Name.Equals("href", StringComparison.OrdinalIgnoreCase) &&
                    !attribute.Value.StartsWith('#'))
                    return "Only fragment links are allowed in briefing templates.";

                if (attribute.Name.StartsWith("data-mwai-attr-", StringComparison.OrdinalIgnoreCase))
                {
                    var targetAttribute = attribute.Name["data-mwai-attr-".Length..];
                    if (targetAttribute is not "alt" and not "aria-label" and not "aria-describedby" and not "title" and not "placeholder" and not "value" and not "max" and not "min")
                        return $"The briefing template contains an unsafe bound attribute '{targetAttribute}'.";
                }
                else if (attribute.Name.StartsWith("data-mwai-", StringComparison.OrdinalIgnoreCase) &&
                         !ALLOWED_DATA_ATTRIBUTES.Contains(attribute.Name))
                {
                    return $"The briefing template contains the unknown binding '{attribute.Name}'.";
                }
            }

            if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) &&
                FindAttribute(node, "data-mwai-asset") is null)
                return "Every briefing image must use a data-mwai asset binding.";

            if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) &&
                FindAttribute(node, "data-mwai-attr-alt") is null)
                return "Every briefing image must use a bound text alternative.";

            if (FindAttribute(node, "aria-label") is not null &&
                FindAttribute(node, "data-mwai-attr-aria-label") is null ||
                FindAttribute(node, "placeholder") is not null &&
                FindAttribute(node, "data-mwai-attr-placeholder") is null ||
                FindAttribute(node, "title") is not null &&
                FindAttribute(node, "data-mwai-attr-title") is null)
                return "Visible accessibility labels, placeholders, and titles must use data bindings.";

            if (node.Name.Equals("input", StringComparison.OrdinalIgnoreCase) &&
                FindAttribute(node, "value") is not null &&
                FindAttribute(node, "data-mwai-attr-value") is null &&
                FindAttribute(node, "data-mwai-model") is null)
                return "A visible input value must use a data binding.";

            if (node.Name.Equals("table", StringComparison.OrdinalIgnoreCase) &&
                (FindNode(node, "./caption") is not { } caption ||
                 FindAttribute(caption, "data-mwai-text") is null && FindAttribute(caption, "data-mwai-expr") is null &&
                 FindNode(caption, ".//*[@data-mwai-text or @data-mwai-expr]") is null ||
                 FindNode(node, ".//th") is null ||
                 FindNodes(node, ".//th")?.Any(header =>
                     header.GetAttributeValue("scope", string.Empty) is not "row" and not "col") == true))
                return "Every table must have a bound caption and scoped row or column headers.";

            var bindingIssue = ValidateNodeBindings(node, data);
            if (!string.IsNullOrEmpty(bindingIssue))
                return bindingIssue;
        }

        var assets = GetDataAtPath(data, "_mwai.assets");
        var boundAssetIds = root.Descendants()
            .Where(node => node.NodeType is HtmlNodeType.Element && FindAttribute(node, "data-mwai-asset") is not null)
            .Select(node => node.GetAttributeValue("data-mwai-asset", string.Empty))
            .ToArray();
        
        if (boundAssetIds.Any(assetId => string.IsNullOrWhiteSpace(assetId) ||
                                         assets is not { ValueKind: JsonValueKind.Object } ||
                                         !assets.Value.TryGetProperty(assetId, out var assetValue) ||
                                         assetValue.ValueKind is not JsonValueKind.String ||
                                         !assetValue.GetString()!.StartsWith("data:image/", StringComparison.Ordinal)))
            return "The briefing template contains an unknown or invalid visual asset binding.";

        if (manifest is not null)
        {
            foreach (var asset in manifest.Sources.Where(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET))
            {
                var assetNode = root.Descendants()
                    .FirstOrDefault(node =>
                        node.NodeType is HtmlNodeType.Element &&
                        string.Equals(
                            node.GetAttributeValue("data-mwai-asset", string.Empty),
                            asset.AssetId,
                            StringComparison.Ordinal));
                
                if (string.IsNullOrWhiteSpace(asset.AssetId) ||
                    assetNode is null ||
                    FindAttribute(assetNode, "hidden") is not null ||
                    string.Equals(
                        assetNode.GetAttributeValue("aria-hidden", string.Empty),
                        "true",
                        StringComparison.OrdinalIgnoreCase) ||
                    assetNode.Ancestors().TakeWhile(ancestor => ancestor != root)
                        .Take(1)
                        .Any(ancestor =>
                            FindAttribute(ancestor, "hidden") is not null ||
                            string.Equals(
                                ancestor.GetAttributeValue("aria-hidden", string.Empty),
                                "true",
                                StringComparison.OrdinalIgnoreCase)) ||
                    IsHiddenByCss(assetNode, css) ||
                    assetNode.ParentNode != root &&
                    IsHiddenByCss(assetNode.ParentNode, css))
                    return $"The visual asset '{asset.AssetId}' is not visibly bound in the template.";
            }
        }

        var hasCharts = FindNode(root, ".//*[@data-mwai-chart]") is not null;
        if (usesCharts != hasCharts)
            return "Chart runtime selection does not match the template's data-mwai-chart bindings.";

        return string.Empty;
    }

    /// <summary>
    /// Defines <c>ComputePayloadHash</c> for the visual briefing feature.
    /// </summary>
    private static string ComputePayloadHash(string dataJson, string template, string css, string runtime, string? echarts) =>
        VisualBriefingHashing.ComputeSections(dataJson, template, css, runtime, echarts);

    /// <summary>
    /// Defines <c>ScriptCspHash</c> for the visual briefing feature.
    /// </summary>
    private static string ScriptCspHash(string script) =>
        $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'";

    /// <summary>
    /// Defines <c>BuildRuntimeScript</c> for the visual briefing feature.
    /// </summary>
    private static string BuildRuntimeScript(string aiStudioVersion) =>
        RUNTIME_SCRIPT.Replace(
            "\"__MWAI_AI_STUDIO_VERSION__\"",
            JsonSerializer.Serialize(aiStudioVersion, JSON_OPTIONS),
            StringComparison.Ordinal);

    /// <summary>
    /// Defines <c>ExtractRuntimeAIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    private static string? ExtractRuntimeAIStudioVersion(string runtime)
    {
        var match = RUNTIME_AI_VERSION_REGEX.Match(runtime);
        if (!match.Success)
            return null;

        try
        {
            return JsonSerializer.Deserialize<string>(match.Groups["value"].Value, JSON_OPTIONS);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Defines <c>BuildScriptTag</c> for the visual briefing feature.
    /// </summary>
    private static string BuildScriptTag(string? script, string id) => string.IsNullOrWhiteSpace(script)
        ? string.Empty
        : $"<script id=\"{id}\">{script}</script>";

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
    /// Defines <c>HtmlEncode</c> for the visual briefing feature.
    /// </summary>
    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);

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
    /// Defines <c>ValidateNodeBindings</c> for the visual briefing feature.
    /// </summary>
    private static string ValidateNodeBindings(HtmlNode node, JsonElement data)
    {
        var isRepeatedContext = node.Ancestors().Any(ancestor => FindAttribute(ancestor, "data-mwai-each") is not null);
        foreach (var attribute in node.Attributes)
        {
            if (attribute.Name.StartsWith("data-mwai-attr-", StringComparison.OrdinalIgnoreCase) ||
                PATH_BINDINGS.Contains(attribute.Name))
            {
                var path = attribute.Value;
                if (!IsSafeBindingPath(path, isRepeatedContext))
                    return $"The briefing binding '{attribute.Name}' contains an invalid data path.";

                var isRootPath = path.StartsWith("$root.", StringComparison.Ordinal);
                if (isRepeatedContext &&
                    attribute.Name is "data-mwai-model" or "data-mwai-set" or "data-mwai-toggle" or "data-mwai-filter" &&
                    !isRootPath)
                    return $"The interactive binding '{attribute.Name}' inside a repeated area must use a $root path.";

                var value = ResolveBindingValue(node, data, path, out var canValidateValue);
                if (canValidateValue)
                {
                    if (value is null)
                        return $"The briefing binding '{attribute.Name}' references a missing data path.";

                    if (attribute.Name.Equals("data-mwai-each", StringComparison.OrdinalIgnoreCase) &&
                        value.Value.ValueKind is not JsonValueKind.Array)
                        return "A data-mwai-each binding must reference an array.";

                    if (attribute.Name.Equals("data-mwai-expr", StringComparison.OrdinalIgnoreCase) &&
                        !IsValidFormula(value.Value, 0, isRoot: true))
                        return "A data-mwai-expr binding references an invalid formula tree.";

                    if (attribute.Name.Equals("data-mwai-if", StringComparison.OrdinalIgnoreCase) &&
                        value.Value.ValueKind is JsonValueKind.Object &&
                        !IsValidFormula(value.Value, 0, isRoot: true))
                        return "A data-mwai-if binding references an invalid formula tree.";

                    if (attribute.Name.Equals("data-mwai-chart", StringComparison.OrdinalIgnoreCase) &&
                        (value.Value.ValueKind is not JsonValueKind.Object ||
                         !IsValidChartOption(value.Value)))
                        return "A data-mwai-chart binding must reference a whitelisted chart option object.";
                }
            }
        }

        var hasFilter = FindAttribute(node, "data-mwai-filter") is not null;
        var hasFilterValue = FindAttribute(node, "data-mwai-filter-value") is not null;
        if (hasFilter != hasFilterValue)
            return "A data-mwai-filter binding must have a matching data-mwai-filter-value binding.";

        var selector = node.GetAttributeValue("data-mwai-search", string.Empty);
        if (FindAttribute(node, "data-mwai-search") is not null && !SAFE_SELECTOR.IsMatch(selector))
            return "A data-mwai-search binding contains an invalid selector.";

        if (FindAttribute(node, "data-mwai-set") is not null)
        {
            var serializedValue = node.GetAttributeValue("data-mwai-value", string.Empty);
            try
            {
                using var parsedValue = JsonDocument.Parse(serializedValue);
            }
            catch (JsonException)
            {
                return "A data-mwai-set binding must contain a valid JSON data-mwai-value.";
            }
        }

        var tabTarget = node.GetAttributeValue("data-mwai-tab-target", string.Empty);
        if (FindAttribute(node, "data-mwai-tab-target") is not null)
        {
            if (!IsSafeDataPath(tabTarget))
                return "A data-mwai-tab-target binding contains an invalid identifier.";

            var tabs = node.AncestorsAndSelf().FirstOrDefault(candidate => FindAttribute(candidate, "data-mwai-tabs") is not null);
            if (tabs is null || FindNode(tabs, $".//*[@data-mwai-tab-panel='{tabTarget}']") is null)
                return "A data-mwai-tab-target binding has no matching panel.";
        }

        if (FindAttribute(node, "data-mwai-chart") is not null &&
            FindAttribute(node, "aria-describedby") is null &&
            FindAttribute(node, "data-mwai-attr-aria-describedby") is null)
            return "Every chart must reference a visible text or table alternative with aria-describedby.";

        if (FindAttribute(node, "data-mwai-chart") is not null)
        {
            var descriptionIds = node.GetAttributeValue("aria-describedby", string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (FindAttribute(node, "data-mwai-attr-aria-describedby") is { } boundDescription)
            {
                var value = ResolveBindingValue(node, data, boundDescription.Value, out _);
                descriptionIds = value is { ValueKind: JsonValueKind.String }
                    ? value.Value.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    : [];
            }

            if (descriptionIds.Length == 0 ||
                descriptionIds.Any(id => FindElementById(node.OwnerDocument, id) is null))
                return "A chart's aria-describedby binding must reference an existing text or table alternative.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Defines <c>ResolveBindingValue</c> for the visual briefing feature.
    /// </summary>
    private static JsonElement? ResolveBindingValue(
        HtmlNode node,
        JsonElement root,
        string path,
        out bool canValidateValue)
    {
        if (path.StartsWith("$root.", StringComparison.Ordinal))
        {
            canValidateValue = true;
            return GetDataAtPath(root, path[6..]);
        }

        var context = root;
        foreach (var repeat in node.Ancestors()
                     .Where(ancestor => FindAttribute(ancestor, "data-mwai-each") is not null)
                     .Reverse())
        {
            var repeatPath = repeat.GetAttributeValue("data-mwai-each", string.Empty);
            var collection = ResolveRelativePath(root, context, repeatPath);
            
            if (collection is not { ValueKind: JsonValueKind.Array })
            {
                canValidateValue = true;
                return null;
            }
            
            if (collection.Value.GetArrayLength() == 0)
            {
                canValidateValue = false;
                return null;
            }
            
            context = collection.Value[0];
        }

        canValidateValue = true;
        return ResolveRelativePath(root, context, path);
    }

    /// <summary>
    /// Defines <c>ResolveRelativePath</c> for the visual briefing feature.
    /// </summary>
    private static JsonElement? ResolveRelativePath(JsonElement root, JsonElement context, string path)
    {
        if (path is "$root")
            return root;
        
        if (path.StartsWith("$root.", StringComparison.Ordinal))
            return GetDataAtPath(root, path[6..]);
        
        if (path is "." or "$value")
            return context;
        
        if (path is "$index")
            return JsonSerializer.SerializeToElement(0);
        
        if (path.StartsWith(".", StringComparison.Ordinal))
            return GetDataAtPath(context, path[1..]);
        
        return GetDataAtPath(root, path);
    }

    /// <summary>
    /// Defines <c>GetDataAtPath</c> for the visual briefing feature.
    /// </summary>
    private static JsonElement? GetDataAtPath(JsonElement data, string path)
    {
        var current = data;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind is JsonValueKind.Object && current.TryGetProperty(segment, out var property))
            {
                current = property;
                continue;
            }

            if (current.ValueKind is JsonValueKind.Array &&
                int.TryParse(segment, out var index) &&
                index >= 0 &&
                index < current.GetArrayLength())
            {
                current = current[index];
                continue;
            }

            return null;
        }

        return current;
    }

    /// <summary>
    /// Defines <c>IsValidFormula</c> for the visual briefing feature.
    /// </summary>
    private static bool IsValidFormula(JsonElement node, int depth, bool isRoot)
    {
        if (depth > 32)
            return false;

        if (node.ValueKind is JsonValueKind.Number or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
            return !isRoot;

        if (node.ValueKind is not JsonValueKind.Object)
            return false;

        if (isRoot &&
            (!node.TryGetProperty("formulaVersion", out var version) ||
             version.ValueKind is not JsonValueKind.Number ||
             !version.TryGetInt32(out var parsedVersion) ||
             parsedVersion != VisualBriefingVersions.FORMULA))
            return false;

        // Formula paths are always absolute, see VisualBriefingValidation.ValidateFormulaNode.
        // Therefore, relative paths and the context-self path are not allowed here:
        if (node.TryGetProperty("path", out var path))
            return node.EnumerateObject().All(property =>
                       property.Name is "formulaVersion" or "path") &&
                   path.ValueKind is JsonValueKind.String &&
                   IsSafeBindingPath(path.GetString() ?? string.Empty, repeatedContext: false);

        if (node.TryGetProperty("value", out _))
            return node.EnumerateObject().All(property =>
                property.Name is "formulaVersion" or "value");

        if (!node.TryGetProperty("op", out var operation) ||
            operation.ValueKind is not JsonValueKind.String ||
            !FORMULA_OPERATORS.Contains(operation.GetString() ?? string.Empty) ||
            !node.TryGetProperty("args", out var arguments) ||
            arguments.ValueKind is not JsonValueKind.Array)
            return false;

        var argumentCount = arguments.GetArrayLength();
        var validArity = operation.GetString() switch
        {
            "sqrt" or "log" or "exp" => argumentCount == 1,
            "subtract" or "divide" or "power" or "eq" or "ne" or "gt" or "gte" or "lt" or "lte" => argumentCount == 2,
            "if" => argumentCount == 3,
            "round" => argumentCount is 1 or 2,
            _ => argumentCount > 0,
        };
        
        return validArity &&
               node.EnumerateObject().All(property =>
                   property.Name is "formulaVersion" or "op" or "args") &&
               arguments.EnumerateArray().All(argument => IsValidFormula(argument, depth + 1, isRoot: false));
    }

    /// <summary>
    /// Defines <c>IsValidChartOption</c> for the visual briefing feature.
    /// </summary>
    private static bool IsValidChartOption(JsonElement option)
    {
        if (!option.TryGetProperty("series", out var series) ||
            series.ValueKind is not JsonValueKind.Array ||
            series.GetArrayLength() == 0)
            return false;

        HashSet<string> allowedSeries = new(StringComparer.Ordinal)
        {
            "line",
            "bar",
            "scatter",
            "pie",
            "radar",
        };
        
        return series.EnumerateArray().All(item =>
            item.ValueKind is JsonValueKind.Object &&
            item.TryGetProperty("type", out var type) &&
            type.ValueKind is JsonValueKind.String &&
            allowedSeries.Contains(type.GetString() ?? string.Empty));
    }

    /// <summary>
    /// Defines <c>HasDuplicateProperties</c> for the visual briefing feature.
    /// </summary>
    private static bool HasDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Array)
            return value.EnumerateArray().Any(HasDuplicateProperties);

        if (value.ValueKind is not JsonValueKind.Object)
            return false;

        var properties = value.EnumerateObject().ToArray();
        return properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length ||
               properties.Any(property => HasDuplicateProperties(property.Value));
    }

    /// <summary>
    /// Defines <c>HasUnsafePropertyNames</c> for the visual briefing feature.
    /// </summary>
    private static bool HasUnsafePropertyNames(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Array)
            return value.EnumerateArray().Any(HasUnsafePropertyNames);

        if (value.ValueKind is not JsonValueKind.Object)
            return false;

        return value.EnumerateObject().Any(property =>
            property.Name is "__proto__" or "prototype" or "constructor" ||
            HasUnsafePropertyNames(property.Value));
    }

    /// <summary>
    /// Defines <c>ContainsLocalOrInternalValue</c> for the visual briefing feature.
    /// </summary>
    private static bool ContainsLocalOrInternalValue(JsonElement value, VisualBriefingManifest? manifest)
    {
        if (value.ValueKind is JsonValueKind.Array)
            return value.EnumerateArray().Any(item => ContainsLocalOrInternalValue(item, manifest));

        if (value.ValueKind is JsonValueKind.Object)
            return value.EnumerateObject().Any(property =>
                property.Name is not "_mwai" &&
                ContainsLocalOrInternalValue(property.Value, manifest));

        if (value.ValueKind is not JsonValueKind.String)
            return false;

        var text = value.GetString() ?? string.Empty;
        if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return true;

        if (manifest is null)
            return false;

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (manifest.Sources.Any(source =>
                text.Contains(source.Path, pathComparison) ||
                text.Contains(source.Path.Replace('\\', '/'), pathComparison)))
            return true;

        var sensitiveValues = new[]
        {
            manifest.Settings.ProviderId,
            manifest.Settings.ProfileId,
            manifest.Settings.ModelId,
        }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate));
        return sensitiveValues.Any(candidate => text.Contains(candidate, StringComparison.Ordinal));
    }

    /// <summary>
    /// Defines <c>IsSafeDataPath</c> for the visual briefing feature.
    /// </summary>
    private static bool IsSafeDataPath(string path) =>
        DATA_PATH.IsMatch(path) &&
        path.Split('.').All(segment => segment is not "__proto__" and not "prototype" and not "constructor");

    /// <summary>
    /// Defines <c>IsSafeBindingPath</c> for the visual briefing feature.
    /// </summary>
    private static bool IsSafeBindingPath(string path, bool repeatedContext)
    {
        if (path is "$root")
            return true;
        
        if (IsSafeDataPath(path))
            return true;

        // Inside a repeated area, "." addresses the current item itself. ResolveRelativePath
        // resolves it, so the safety check must accept it as well:
        if (repeatedContext && path is ".")
            return true;

        if (!repeatedContext || !LOCAL_DATA_PATH.IsMatch(path))
            return false;
        
        return path.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "__proto__" and not "prototype" and not "constructor");
    }

    /// <summary>
    /// Defines <c>ContainsChartBinding</c> for the visual briefing feature.
    /// </summary>
    private static bool ContainsChartBinding(string templateHtml)
    {
        var document = new HtmlDocument();
        document.LoadHtml($"<div id=\"chart-detection-root\">{templateHtml}</div>");
        
        var root = FindElementById(document, "chart-detection-root");
        return root is not null && FindNode(root, ".//*[@data-mwai-chart]") is not null;
    }

    /// <summary>
    /// Determines whether a simple stylesheet rule hides an element.
    /// </summary>
    /// <param name="node">The element to inspect.</param>
    /// <param name="css">The validated model stylesheet.</param>
    /// <returns><see langword="true"/> when a matching rule hides the element.</returns>
    private static bool IsHiddenByCss(HtmlNode node, string css)
    {
        foreach (Match rule in CssRuleRegex().Matches(css))
        {
            if (!CssHiddenDeclarationRegex().IsMatch(rule.Groups["declarations"].Value))
                continue;
            
            foreach (var selector in rule.Groups["selectors"].Value.Split(','))
            {
                if (SimpleSelectorMatches(node, selector))
                    return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Matches the final simple component of a CSS selector against one element.
    /// </summary>
    /// <param name="node">The element.</param>
    /// <param name="selector">The stylesheet selector.</param>
    /// <returns>Whether the selector targets the element.</returns>
    private static bool SimpleSelectorMatches(HtmlNode node, string selector)
    {
        var candidate = selector.Trim();
        if (candidate.Length == 0)
            return false;
        
        var finalSeparator = candidate.LastIndexOfAny([' ', '>', '+', '~']);
        if (finalSeparator >= 0)
            candidate = candidate[(finalSeparator + 1)..].Trim();
        
        var pseudo = candidate.IndexOf(':');
        if (pseudo >= 0)
            candidate = candidate[..pseudo];
        
        if (candidate.Contains("[data-mwai-asset", StringComparison.OrdinalIgnoreCase))
            return FindAttribute(node, "data-mwai-asset") is not null;

        var idMatch = IdRegex().Match(candidate);
        if (idMatch.Success &&
            !string.Equals(node.Id, idMatch.Groups["id"].Value, StringComparison.Ordinal))
            return false;
        
        var requiredClasses = RequiredClassRegex().Matches(candidate)
            .Select(match => match.Groups["class"].Value)
            .ToArray();
        
        var classes = node.GetAttributeValue("class", string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        
        if (requiredClasses.Any(requiredClass => !classes.Contains(requiredClass)))
            return false;
        
        var tag = TagRegex().Match(candidate);
        
        return !tag.Success ||
               string.Equals(node.Name, tag.Groups["tag"].Value, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Defines <c>CreateExportManifest</c> for the visual briefing feature.
    /// </summary>
    private static VisualBriefingExportManifest CreateExportManifest(
        VisualBriefingManifest manifest,
        VisualBriefingRevisionRequest request,
        string payloadHash,
        string aiStudioVersion,
        string runtimeAIStudioVersion) => new()
    {
        BriefingId = manifest.BriefingId,
        RevisionId = request.RevisionId ?? Guid.NewGuid(),
        ParentRevisionId = request.ParentRevisionId,
        Name = manifest.Name,
        Author = manifest.Author,
        CreatedAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow,
        TargetLanguage = manifest.Settings.TargetLanguage,
        CustomTargetLanguage = manifest.Settings.CustomTargetLanguage,
        AudienceProfile = manifest.Settings.AudienceProfile,
        AudienceAgeGroup = manifest.Settings.AudienceAgeGroup,
        AudienceOrganizationalLevel = manifest.Settings.AudienceOrganizationalLevel,
        AudienceExpertise = manifest.Settings.AudienceExpertise,
        ShowSourceReferences = manifest.Settings.ShowSourceReferences,
        ProtectionLevel = manifest.Settings.ProtectionLevel,
        CustomProtectionLevel = manifest.Settings.CustomProtectionLevel,
        AIStudioVersion = aiStudioVersion,
        RuntimeAIStudioVersion = runtimeAIStudioVersion,
        PayloadHash = payloadHash,
    };

    /// <summary>
    /// Defines <c>AddProtectedArtifactData</c> for the visual briefing feature.
    /// </summary>
    private static JsonElement AddProtectedArtifactData(VisualBriefingManifest manifest, VisualBriefingRevisionRequest request)
    {
        var source = request.Data;
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(source.GetRawText(), JSON_OPTIONS) ?? [];
        dictionary.Remove("assets");
        dictionary.Remove("footerTemplates");
        dictionary.Remove("protectionLabel");
        dictionary.Remove("_mwai");
        dictionary["_mwai"] = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = VisualBriefingVersions.SCHEMA,
            runtimeVersion = VisualBriefingVersions.RUNTIME,
            aiStudioVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown",
            assets = request.EmbeddedAssets ?? new Dictionary<string, string>(StringComparer.Ordinal),
            assetMetadata = (request.AssetPlan ?? []).ToDictionary(
                asset => asset.AssetId,
                asset => new { asset.Description, asset.AltText },
                StringComparer.Ordinal),
            footer = BuildFooter(manifest, request),
        }, JSON_OPTIONS);
        
        return JsonSerializer.SerializeToElement(dictionary, JSON_OPTIONS);
    }

    /// <summary>
    /// Defines <c>BuildFooter</c> for the visual briefing feature.
    /// </summary>
    private static object BuildFooter(VisualBriefingManifest manifest, VisualBriefingRevisionRequest request)
    {
        var labels = FooterLabelsFor(manifest.Settings, request.CustomLanguageLabels);
        var protection = manifest.Settings.ProtectionLevel is VisualBriefingProtectionLevel.OTHER
            ? manifest.Settings.CustomProtectionLevel
            : labels.ProtectionLevel;
        
        var created = (request.CreatedAtUtc ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        var author = string.IsNullOrWhiteSpace(manifest.Author) ? "—" : manifest.Author;
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown";
        
        var contributions = request.ModelContributions?.Where(contribution => !string.IsNullOrWhiteSpace(contribution.Model))
            .Distinct()
            .ToArray() ?? [];
        
        if (contributions.Length == 0 && !string.IsNullOrWhiteSpace(request.ModelDisplayName))
            contributions = [new(VisualBriefingModelRole.CONTENT, request.ModelDisplayName)];
        
        var models = contributions.Length == 0
            ? "—"
            : string.Join(
                "; ",
                contributions
                    .GroupBy(contribution => contribution.Model, StringComparer.Ordinal)
                    .Select(group =>
                    {
                        var roles = group.Select(contribution => contribution.Role)
                            .Distinct()
                            .Select(role => role is VisualBriefingModelRole.DESIGN
                                ? labels.PresentationRole
                                : labels.ContentRole);
                        return $"{group.Key} ({string.Join(", ", roles)})";
                    }));

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["createdWith"] = ApplyFooterTemplate(labels.CreatedWith, "version", version),
            ["models"] = ApplyFooterTemplate(labels.Models, "models", models),
            ["createdAt"] = ApplyFooterTemplate(labels.CreatedAt, "date", created),
            ["authors"] = ApplyFooterTemplate(labels.Authors, "authors", author),
            ["protection"] = ApplyFooterTemplate(labels.Protection, "level", protection),
        };
    }

    /// <summary>
    /// Defines <c>FooterLabelsFor</c> for the visual briefing feature.
    /// </summary>
    private static FooterLabels FooterLabelsFor(
        VisualBriefingLocalSettings settings,
        IReadOnlyDictionary<string, string>? customLabels)
    {
        if (settings.TargetLanguage is CommonLanguages.OTHER &&
            customLabels is not null)
        {
            return new(
                customLabels["createdWith"],
                customLabels["models"],
                customLabels["createdAt"],
                customLabels["authors"],
                customLabels["protection"],
                customLabels["contentRole"],
                customLabels["designRole"],
                customLabels["protectionLevel"]);
        }

        var protection = LocalizedProtectionLevel(settings.TargetLanguage, settings.ProtectionLevel);
        return settings.TargetLanguage switch
        {
            CommonLanguages.ZH_CN => new(
                "使用 MindWork AI Studio v{version} 创建。",
                "贡献模型：{models}。",
                "版本创建日期：{date}。",
                "作者：{authors}。",
                "保护级别：{level}。",
                "内容",
                "演示",
                protection),
            CommonLanguages.HI_IN => new(
                "MindWork AI Studio v{version} से बनाया गया।",
                "योगदान देने वाले मॉडल: {models}।",
                "संस्करण निर्माण तिथि: {date}।",
                "लेखक: {authors}।",
                "सुरक्षा स्तर: {level}।",
                "सामग्री",
                "प्रस्तुति",
                protection),
            CommonLanguages.ES_ES => new(
                "Creado con MindWork AI Studio v{version}.",
                "Modelos participantes: {models}.",
                "Versión creada el {date}.",
                "Autoría: {authors}.",
                "Nivel de protección: {level}.",
                "Contenido",
                "Presentación",
                protection),
            CommonLanguages.FR_FR => new(
                "Créé avec MindWork AI Studio v{version}.",
                "Modèles contributeurs : {models}.",
                "Version créée le {date}.",
                "Auteur(s) : {authors}.",
                "Niveau de protection : {level}.",
                "Contenu",
                "Présentation",
                protection),
            CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH => new(
                "Erstellt mit MindWork AI Studio v{version}.",
                "Beitragende Modelle: {models}.",
                "Version erstellt am {date}.",
                "Autorinnen und Autoren: {authors}.",
                "Schutzniveau: {level}.",
                "Inhalt",
                "Darstellung",
                protection),
            CommonLanguages.JA_JP => new(
                "MindWork AI Studio v{version} で作成。",
                "使用モデル: {models}。",
                "バージョン作成日: {date}。",
                "作成者: {authors}。",
                "保護レベル: {level}。",
                "コンテンツ",
                "プレゼンテーション",
                protection),
            CommonLanguages.RU_RU => new(
                "Создано с помощью MindWork AI Studio v{version}.",
                "Использованные модели: {models}.",
                "Версия создана {date}.",
                "Автор(ы): {authors}.",
                "Уровень защиты: {level}.",
                "Содержание",
                "Представление",
                protection),
            _ => new(
                "Created with MindWork AI Studio v{version}.",
                "Contributing models: {models}.",
                "Revision created on {date}.",
                "Author(s): {authors}.",
                "Protection level: {level}.",
                "Content",
                "Presentation",
                protection),
        };
    }

    /// <summary>
    /// Defines <c>LocalizedProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    private static string LocalizedProtectionLevel(
        CommonLanguages language,
        VisualBriefingProtectionLevel level) => (language, level) switch
    {
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.PUBLIC) => "öffentlich",
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.INTERNAL) => "intern",
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.PRIVATE) => "privat",
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.CONFIDENTIAL) => "vertraulich",
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "streng vertraulich",
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.SECRET) => "geheim",
        (CommonLanguages.DE_DE or CommonLanguages.DE_AT or CommonLanguages.DE_CH, VisualBriefingProtectionLevel.TOP_SECRET) => "streng geheim",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.PUBLIC) => "公开",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.INTERNAL) => "内部",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.PRIVATE) => "私有",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.CONFIDENTIAL) => "机密",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "严格保密",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.SECRET) => "秘密",
        (CommonLanguages.ZH_CN, VisualBriefingProtectionLevel.TOP_SECRET) => "绝密",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.PUBLIC) => "सार्वजनिक",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.INTERNAL) => "आंतरिक",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.PRIVATE) => "निजी",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.CONFIDENTIAL) => "गोपनीय",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "अत्यंत गोपनीय",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.SECRET) => "गुप्त",
        (CommonLanguages.HI_IN, VisualBriefingProtectionLevel.TOP_SECRET) => "परम गुप्त",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.PUBLIC) => "público",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.INTERNAL) => "interno",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.PRIVATE) => "privado",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.CONFIDENTIAL) => "confidencial",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "estrictamente confidencial",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.SECRET) => "secreto",
        (CommonLanguages.ES_ES, VisualBriefingProtectionLevel.TOP_SECRET) => "alto secreto",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.PUBLIC) => "public",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.INTERNAL) => "interne",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.PRIVATE) => "privé",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.CONFIDENTIAL) => "confidentiel",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "strictement confidentiel",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.SECRET) => "secret",
        (CommonLanguages.FR_FR, VisualBriefingProtectionLevel.TOP_SECRET) => "très secret",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.PUBLIC) => "公開",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.INTERNAL) => "社内",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.PRIVATE) => "非公開",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.CONFIDENTIAL) => "機密",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "厳秘",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.SECRET) => "秘密",
        (CommonLanguages.JA_JP, VisualBriefingProtectionLevel.TOP_SECRET) => "最高機密",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.PUBLIC) => "общедоступно",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.INTERNAL) => "для внутреннего использования",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.PRIVATE) => "частное",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.CONFIDENTIAL) => "конфиденциально",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL) => "строго конфиденциально",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.SECRET) => "секретно",
        (CommonLanguages.RU_RU, VisualBriefingProtectionLevel.TOP_SECRET) => "совершенно секретно",
        _ => level.ToString().Replace('_', ' ').ToLowerInvariant(),
    };

    /// <summary>
    /// Defines <c>FooterLabels</c> for the visual briefing feature.
    /// </summary>
    private sealed record FooterLabels(
        string CreatedWith,
        string Models,
        string CreatedAt,
        string Authors,
        string Protection,
        string ContentRole,
        string PresentationRole,
        string ProtectionLevel);

    /// <summary>
    /// Defines <c>ApplyFooterTemplate</c> for the visual briefing feature.
    /// </summary>
    private static string ApplyFooterTemplate(string template, string token, string value) =>
        template.Replace($"{{{token}}}", value, StringComparison.Ordinal);

    /// <summary>
    /// Defines <c>GetHtmlLanguage</c> for the visual briefing feature.
    /// </summary>
    private static string GetHtmlLanguage(VisualBriefingLocalSettings settings) =>
        GetHtmlLanguage(settings.TargetLanguage, settings.CustomTargetLanguage);

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
    /// Defines <c>CssProhibitedRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"(?:@import|@font-face|url\s*\(|expression\s*\(|javascript\s*:|behavior\s*:|-moz-binding|content\s*:|<\s*/?\s*script)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssProhibitedRegex();

    /// <summary>
    /// Defines <c>CssProtectedTargetRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"(?:#mwai-static-footer|\.mwai-footer|(?:^|[^A-Za-z0-9_-])(?:html|body|footer|:root)(?=[^A-Za-z0-9_-]))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex CssProtectedTargetRegex();

    /// <summary>
    /// Defines <c>DataPathRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"^(?:\$root\.)?(?:\$index|\$value|[A-Za-z_][A-Za-z0-9_-]*)(?:\.(?:[A-Za-z_][A-Za-z0-9_-]*|\d+))*$", RegexOptions.CultureInvariant)]
    private static partial Regex DataPathRegex();

    /// <summary>
    /// Defines <c>LocalDataPathRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"^\.(?:[A-Za-z_][A-Za-z0-9_-]*)(?:\.(?:[A-Za-z_][A-Za-z0-9_-]*|\d+))*$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalDataPathRegex();

    /// <summary>
    /// Defines <c>SafeSelectorRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"^[.#]?[A-Za-z][A-Za-z0-9_-]*(?:\s+[.#]?[A-Za-z][A-Za-z0-9_-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSelectorRegex();

    /// <summary>
    /// Defines <c>HtmlLanguageTagRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlLanguageTagRegex();

    /// <summary>
    /// Matches simple CSS rules for visibility checks.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(@"(?<selectors>[^{}]+)\{(?<declarations>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex CssRuleRegex();

    /// <summary>
    /// Matches declarations that visually hide an element.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(@"(?:display\s*:\s*none|visibility\s*:\s*hidden|opacity\s*:\s*0(?:\.0+)?)(?:\s*!important)?\s*(?:;|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssHiddenDeclarationRegex();

    /// <summary>
    /// Defines <c>ManifestRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"<!--MWAI_VISUAL_BRIEFING_MANIFEST:(?<value>[A-Za-z0-9+/=]+)-->", RegexOptions.CultureInvariant)]
    private static partial Regex ManifestRegex();

    /// <summary>
    /// Defines <c>StyleRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"<style\s+id=""mwai-briefing-style"">(?<value>[\s\S]*?)</style>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleRegex();

    /// <summary>
    /// Defines <c>RuntimeRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"<script\s+id=""mwai-briefing-runtime"">(?<value>[\s\S]*?)</script>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeRegex();

    /// <summary>
    /// Defines <c>RuntimeAIVersionRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"const AI_STUDIO_VERSION = (?<value>""(?:\\.|[^""\\])*"");", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeAIVersionRegex();

    /// <summary>
    /// Defines <c>EChartsRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"<script\s+id=""mwai-echarts-runtime"">(?<value>[\s\S]*?)</script>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EChartsRegex();

    /// <summary>
    /// Defines the pinned declarative AI Studio briefing runtime.
    /// </summary>
    private const string RUNTIME_SCRIPT = """
                                         (() => {
                                           "use strict";
                                           const VERSION = 1;
                                           const AI_STUDIO_VERSION = "__MWAI_AI_STUDIO_VERSION__";
                                           const dataElement = document.getElementById("mwai-briefing-data");
                                           const root = document.getElementById("mwai-briefing-root");
                                           if (!dataElement || !root) return;
                                           const state = JSON.parse(dataElement.textContent || "{}");
                                           const contexts = new WeakMap();
                                           const get = (path, context = state) => {
                                             if (!path) return undefined;
                                             if (path === "$root") return state;
                                             if (path === ".") return context && Object.hasOwn(context, "$value") ? context.$value : context;
                                             if (path === "$index") return context && context.$index;
                                             if (path === "$value") return context && context.$value;
                                             const isRoot = path.startsWith("$root.");
                                             const normalized = isRoot ? path.slice(6) : path.startsWith(".") ? path.slice(1) : path;
                                             return normalized.split(".").filter(Boolean).reduce((value, key) => value == null ? undefined : value[key], isRoot ? state : path.startsWith(".") ? context : state);
                                           };
                                           const set = (path, value) => {
                                             const parts = (path.startsWith("$root.") ? path.slice(6) : path).split(".").filter(Boolean);
                                             let target = state;
                                             for (let index = 0; index < parts.length - 1; index++) target = target[parts[index]] ??= {};
                                             target[parts.at(-1)] = value;
                                           };
                                           const expression = (node, context) => {
                                             if (node == null || typeof node !== "object") return node;
                                             if ("path" in node) return get(node.path, context);
                                             if ("value" in node) return node.value;
                                             const args = (node.args || []).map(value => expression(value, context));
                                             switch (node.op) {
                                               case "add": return args.reduce((a, b) => a + b, 0);
                                               case "subtract": return args[0] - args[1];
                                               case "multiply": return args.reduce((a, b) => a * b, 1);
                                               case "divide": return args[1] === 0 ? null : args[0] / args[1];
                                               case "power": return Math.pow(args[0], args[1]);
                                               case "eq": return args[0] === args[1];
                                               case "ne": return args[0] !== args[1];
                                               case "gt": return args[0] > args[1];
                                               case "gte": return args[0] >= args[1];
                                               case "lt": return args[0] < args[1];
                                               case "lte": return args[0] <= args[1];
                                               case "if": return args[0] ? args[1] : args[2];
                                               case "min": return Math.min(...args);
                                               case "max": return Math.max(...args);
                                               case "round": return Math.round(args[0] * Math.pow(10, args[1] || 0)) / Math.pow(10, args[1] || 0);
                                               case "sqrt": return Math.sqrt(args[0]);
                                               case "log": return Math.log(args[0]);
                                               case "exp": return Math.exp(args[0]);
                                               default: return null;
                                             }
                                           };
                                           const bind = (container, context = state) => {
                                             container.querySelectorAll("[data-mwai-text]").forEach(element => {
                                               const value = get(element.dataset.mwaiText, contexts.get(element) || context);
                                               element.textContent = value == null ? "" : String(value);
                                             });
                                             container.querySelectorAll("[data-mwai-expr]").forEach(element => {
                                               const localContext = contexts.get(element) || context;
                                               const tree = get(element.dataset.mwaiExpr, localContext);
                                               const value = expression(tree, localContext);
                                               element.textContent = value == null ? "" : String(value);
                                             });
                                             container.querySelectorAll("[data-mwai-if],[data-mwai-filter]").forEach(element => {
                                               const localContext = contexts.get(element) || context;
                                               const conditionValue = element.dataset.mwaiIf ? get(element.dataset.mwaiIf, localContext) : true;
                                               const conditionMatches = Boolean(conditionValue && typeof conditionValue === "object" ? expression(conditionValue, localContext) : conditionValue);
                                               const selected = element.dataset.mwaiFilter ? get(element.dataset.mwaiFilter, localContext) : "";
                                               const filterValue = element.dataset.mwaiFilterValue ? get(element.dataset.mwaiFilterValue, localContext) : "";
                                               const filterMatches = selected == null || selected === "" || selected === "*" || String(selected) === String(filterValue);
                                               element.hidden = !conditionMatches || !filterMatches;
                                             });
                                             container.querySelectorAll("[data-mwai-asset]").forEach(element => {
                                               const asset = state._mwai?.assets?.[element.dataset.mwaiAsset];
                                               if (asset && element.tagName === "IMG") element.src = asset;
                                             });
                                             container.querySelectorAll("*").forEach(element => {
                                               for (const attribute of [...element.attributes]) {
                                                 if (!attribute.name.startsWith("data-mwai-attr-")) continue;
                                                 const name = attribute.name.slice("data-mwai-attr-".length);
                                                 const value = get(attribute.value, contexts.get(element) || context);
                                                 if (value == null) element.removeAttribute(name); else element.setAttribute(name, String(value));
                                               }
                                             });
                                             container.querySelectorAll("template[data-mwai-each]").forEach(template => {
                                               const values = get(template.dataset.mwaiEach, context);
                                               if (!Array.isArray(values)) return;
                                               const fragment = document.createDocumentFragment();
                                               values.forEach((value, index) => {
                                                 const clone = template.content.cloneNode(true);
                                                 const itemContext = value != null && typeof value === "object"
                                                   ? Object.assign(Object.create(value), value, { $index: index })
                                                   : { $value: value, $index: index };
                                                 clone.querySelectorAll("*").forEach(element => contexts.set(element, itemContext));
                                                 bind(clone, itemContext);
                                                 fragment.appendChild(clone);
                                               });
                                               template.replaceWith(fragment);
                                             });
                                           };
                                           bind(document);
                                           root.querySelectorAll("[data-mwai-tab-target]").forEach(button => button.addEventListener("click", () => {
                                             const group = button.closest("[data-mwai-tabs]") || root;
                                             group.querySelectorAll("[data-mwai-tab-panel]").forEach(panel => panel.hidden = panel.dataset.mwaiTabPanel !== button.dataset.mwaiTabTarget);
                                             group.querySelectorAll("[data-mwai-tab-target]").forEach(tab => tab.setAttribute("aria-selected", tab === button ? "true" : "false"));
                                           }));
                                           root.querySelectorAll("[data-mwai-model]").forEach(control => {
                                             const path = control.dataset.mwaiModel;
                                             const value = get(path);
                                             if (control.type === "checkbox") control.checked = Boolean(value); else if (value != null) control.value = value;
                                             control.addEventListener("input", () => {
                                               set(path, control.type === "checkbox" ? control.checked : control.type === "number" || control.type === "range" ? Number(control.value) : control.value);
                                               bind(root);
                                             });
                                           });
                                           root.querySelectorAll("[data-mwai-set]").forEach(button => button.addEventListener("click", () => {
                                             set(button.dataset.mwaiSet, JSON.parse(button.dataset.mwaiValue || "null"));
                                             bind(root);
                                           }));
                                           root.querySelectorAll("[data-mwai-toggle]").forEach(button => button.addEventListener("click", () => {
                                             const path = button.dataset.mwaiToggle;
                                             set(path, !get(path));
                                             bind(root);
                                           }));
                                           root.querySelectorAll("[data-mwai-reset]").forEach(button => button.addEventListener("click", () => {
                                             const componentId = button.dataset.mwaiReset;
                                             (state.interactions?.controls || [])
                                               .filter(control => control.componentId === componentId)
                                               .forEach(control => set(`interactions.state.${control.controlId}`, control.initialValue));
                                             root.querySelectorAll("[data-mwai-model]").forEach(control => {
                                               const value = get(control.dataset.mwaiModel);
                                               if (control.type === "checkbox") control.checked = Boolean(value); else if (value != null) control.value = value;
                                             });
                                             bind(root);
                                           }));
                                           root.querySelectorAll("[data-mwai-search]").forEach(input => input.addEventListener("input", () => {
                                             const selector = input.dataset.mwaiSearch;
                                             root.querySelectorAll(selector).forEach(item => item.hidden = !item.textContent.toLocaleLowerCase().includes(input.value.toLocaleLowerCase()));
                                           }));
                                           root.querySelectorAll("th[data-mwai-sort]").forEach(header => header.addEventListener("click", () => {
                                             const table = header.closest("table");
                                             const body = table?.tBodies[0];
                                             if (!body) return;
                                             const column = header.cellIndex;
                                             const direction = header.dataset.mwaiDirection === "asc" ? -1 : 1;
                                             [...body.rows].sort((a, b) => a.cells[column].textContent.localeCompare(b.cells[column].textContent, undefined, { numeric: true }) * direction).forEach(row => body.appendChild(row));
                                             header.dataset.mwaiDirection = direction === 1 ? "asc" : "desc";
                                           }));
                                           root.querySelectorAll("[data-mwai-chart]").forEach(element => {
                                             const option = get(element.dataset.mwaiChart, contexts.get(element) || state);
                                             if (!option || !window.echarts) return;
                                             const chart = window.echarts.init(element);
                                             chart.setOption(option);
                                             new ResizeObserver(() => chart.resize()).observe(element);
                                           });
                                           document.documentElement.dataset.mwaiRuntimeVersion = String(VERSION);
                                           document.documentElement.dataset.mwaiAiStudioVersion = AI_STUDIO_VERSION;
                                         })();
                                         """;

    [GeneratedRegex(@"#(?<id>[A-Za-z][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
    
    [GeneratedRegex(@"\.(?<class>[A-Za-z][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredClassRegex();
    
    [GeneratedRegex(@"^(?<tag>[A-Za-z][A-Za-z0-9-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();
}