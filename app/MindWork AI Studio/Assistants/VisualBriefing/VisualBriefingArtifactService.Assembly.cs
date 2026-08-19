using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AIStudio.Tools.Metadata;

using HtmlAgilityPack;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingArtifactService
{
    /// <summary>
    /// Lazily loads the official MindWork AI Studio icon for self-contained exports.
    /// </summary>
    private static readonly Lazy<string> BRAND_ICON_DATA_URI = new(LoadBrandIconDataUri);

    /// <summary>
    /// Assembles one self-contained briefing HTML file from validated parts.
    /// </summary>
    /// <remarks>
    /// Assembly itself is synchronous; the task-based signature exists because callers run it inside
    /// cancellable pipeline stages.
    /// </remarks>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="request">The validated revision request.</param>
    /// <param name="lockedRuntimeScript">An existing runtime script to reuse, keeping a revision reproducible.</param>
    /// <param name="lockedEChartsScript">An existing chart runtime to reuse, keeping a revision reproducible.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The complete standalone HTML document.</returns>
    public Task<string> BuildAsync(VisualBriefingManifest manifest, VisualBriefingRevisionRequest request, string? lockedRuntimeScript = null, string? lockedEChartsScript = null, CancellationToken token = default)
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
        
        var runtimeAIStudioVersion = ExtractRuntimeAIStudioVersion(runtime) ?? throw new InvalidDataException("The AI Studio runtime does not contain a valid originating app version.");
        
        var echarts = usesCharts ? lockedEChartsScript ?? ECHARTS_SCRIPT.Value : null;
        if (usesCharts && string.IsNullOrWhiteSpace(echarts))
            throw new InvalidOperationException("Apache ECharts 6.1.0 common is not available in this AI Studio build.");

        var exportMetadata = request.ExportMetadataSource;
        var htmlLanguage = GetHtmlLanguage(
            exportMetadata?.TargetLanguage ?? manifest.Settings.TargetLanguage,
            exportMetadata?.CustomTargetLanguage ?? manifest.Settings.CustomTargetLanguage);
        
        var briefingName = exportMetadata?.Name ?? manifest.Name;
        var exportManifest = CreateExportManifest(manifest, request, DOCUMENT_HASH_PLACEHOLDER, this.AIStudioVersion, runtimeAIStudioVersion);

        var parts = new VisualBriefingArtifactParts(exportManifest, data, template, css, runtime, echarts, DOCUMENT_HASH_PLACEHOLDER);
        var csp = GetContentSecurityPolicy(parts);
        var placeholderDocument = AssembleDocument(exportManifest, htmlLanguage, briefingName, dataJson, template, css, runtime, echarts, csp);

        exportManifest.DocumentHash = VisualBriefingHashing.Compute(placeholderDocument);
        return Task.FromResult(AssembleDocument(exportManifest, htmlLanguage, briefingName, dataJson, template, css, runtime, echarts, csp));
    }

    /// <summary>
    /// Assembles the deterministic document around a supplied artifact header.
    /// </summary>
    private static string AssembleDocument(VisualBriefingExportManifest exportManifest, string htmlLanguage, string briefingName, string dataJson, string template, string css, string runtime, string? echarts, string csp)
    {
        var encodedHeader = EncodeHeader(exportManifest);
        return $"""
                <!doctype html>
                <!--{HEADER_MARKER}{encodedHeader}-->
                <html lang="{htmlLanguage}">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width,initial-scale=1">
                <meta http-equiv="Content-Security-Policy" content="{csp}">
                <meta name="referrer" content="no-referrer">
                <title>{HtmlEncode(briefingName)}</title>
                <style id="mwai-briefing-style">{css}</style>
                <style id="mwai-protected-style">{PROTECTED_STATIC_CSS}</style>
                </head>
                <body>
                <script id="{DATA_ELEMENT_ID}" type="application/json">{dataJson}</script>
                <header id="mwai-static-header">
                {BuildStaticHeaderTemplate()}
                </header>
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
    /// Encodes the stable JSON artifact header for embedding in an HTML comment.
    /// </summary>
    /// <remarks>
    /// The header is canonical JSON because verifying a stored briefing encodes it again and compares
    /// the document hash. Plain serialization would tie every stored document to the order in which the
    /// manifest properties happen to be declared, so moving one property would reject every briefing
    /// ever exported.
    /// </remarks>
    private static string EncodeHeader(VisualBriefingExportManifest exportManifest) => Convert.ToBase64String(Encoding.UTF8.GetBytes(VisualBriefingHashing.CanonicalJson(exportManifest)));

    /// <summary>
    /// Defines <c>RuntimeAIVersionRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex RUNTIME_AI_VERSION_REGEX = RuntimeAIVersionRegex();

    /// <summary>
    /// Defines <c>RuntimeAIVersionRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex("""const AI_STUDIO_VERSION = (?<value>"(?:\\.|[^"\\])*");""", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeAIVersionRegex();

    /// <summary>
    /// Builds the protected, app-owned static header template.
    /// </summary>
    private static string BuildStaticHeaderTemplate() => $"""
                                                          <img src="{BRAND_ICON_DATA_URI.Value}" width="32" height="32" alt="" aria-hidden="true">
                                                          <a href="{PROJECT_URL}" target="_blank" rel="noopener noreferrer">MINDWORK AI STUDIO</a>
                                                          """;

    /// <summary>
    /// Loads the official app icon as a Data URL so exported briefings remain self-contained.
    /// </summary>
    private static string LoadBrandIconDataUri()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("AIStudio.Assistants.VisualBriefing.Runtime.mindwork-ai-studio-icon.png") ??
                           throw new InvalidOperationException("The official MindWork AI Studio icon is not available in this build.");
        
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        
        return $"data:image/png;base64,{Convert.ToBase64String(buffer.ToArray())}";
    }

    /// <summary>
    /// Links exported MindWork AI Studio branding to the project repository.
    /// </summary>
    private const string PROJECT_URL = "https://github.com/MindWorkAI/AI-Studio";

    /// <summary>
    /// Defines the protected, app-owned static footer template.
    /// </summary>
    private const string STATIC_FOOTER_TEMPLATE = $"""
                                                  <span>Created with <a href="{PROJECT_URL}" target="_blank" rel="noopener noreferrer">MindWork AI Studio</a> v<span data-mwai-text="_mwai.aiStudioVersion"></span>.</span>
                                                  <span data-mwai-text="_mwai.footer.models"></span>
                                                  <span data-mwai-text="_mwai.footer.createdAt"></span>
                                                  <span data-mwai-text="_mwai.footer.authors"></span>
                                                  <span data-mwai-text="_mwai.footer.protection"></span>
                                                  """;

    /// <summary>
    /// Defines protected static header and footer styles that model CSS cannot override.
    /// </summary>
    private const string PROTECTED_STATIC_CSS = """
                                                html {
                                                  background: #f3f6f3 !important;
                                                }
                                                body {
                                                  min-width: 0 !important;
                                                  margin: 0 !important;
                                                  background: #f3f6f3 !important;
                                                  color: #172a24 !important;
                                                }
                                                #mwai-static-header {
                                                  display: flex !important;
                                                  align-items: center !important;
                                                  gap: .75rem !important;
                                                  position: relative !important;
                                                  z-index: 2147483647 !important;
                                                  visibility: visible !important;
                                                  opacity: 1 !important;
                                                  max-width: 80rem !important;
                                                  margin: 0 auto !important;
                                                  padding: clamp(1rem, 3.5vw, 3rem) clamp(1rem, 3.5vw, 3rem) 0 !important;
                                                  color: #164b3b !important;
                                                  font: 700 .82rem/1.4 system-ui, sans-serif !important;
                                                  letter-spacing: .08em !important;
                                                  text-transform: uppercase !important;
                                                }
                                                #mwai-static-header img {
                                                  box-sizing: border-box !important;
                                                  display: block !important;
                                                  flex: 0 0 auto !important;
                                                  visibility: visible !important;
                                                  opacity: 1 !important;
                                                  width: 2rem !important;
                                                  height: 2rem !important;
                                                  border-radius: .5rem !important;
                                                  object-fit: cover !important;
                                                }
                                                #mwai-static-header a {
                                                  display: inline !important;
                                                  visibility: visible !important;
                                                  opacity: 1 !important;
                                                  color: inherit !important;
                                                  font: inherit !important;
                                                  letter-spacing: inherit !important;
                                                  text-decoration: none !important;
                                                }
                                                #mwai-static-header a:hover {
                                                  text-decoration: underline !important;
                                                  text-underline-offset: .2em !important;
                                                }
                                                #mwai-static-header a:focus-visible {
                                                  outline: 3px solid #f2d264 !important;
                                                  outline-offset: 3px !important;
                                                }
                                                #mwai-static-footer {
                                                  display: flex !important;
                                                  flex-wrap: wrap !important;
                                                  gap: .5rem 1.25rem !important;
                                                  position: relative !important;
                                                  z-index: 2147483647 !important;
                                                  visibility: visible !important;
                                                  opacity: 1 !important;
                                                  max-width: 74rem !important;
                                                  margin: 1rem auto 0 !important;
                                                  padding: 1.25rem clamp(1rem, 3.5vw, 3rem) 2rem !important;
                                                  border-top: 1px solid #d6e2dc !important;
                                                  color: #5e7169 !important;
                                                  font: 12px/1.55 system-ui, sans-serif !important;
                                                }
                                                #mwai-static-footer span {
                                                  display: inline !important;
                                                  visibility: visible !important;
                                                  opacity: 1 !important;
                                                }
                                                #mwai-static-footer a {
                                                  display: inline !important;
                                                  visibility: visible !important;
                                                  opacity: 1 !important;
                                                  color: inherit !important;
                                                  font: inherit !important;
                                                  text-decoration: underline !important;
                                                  text-underline-offset: .15em !important;
                                                }
                                                @media (max-width: 47.99rem) {
                                                  #mwai-static-header {
                                                    padding: .75rem .75rem 0 !important;
                                                  }
                                                }
                                                @media print {
                                                  html, body {
                                                    background: #fffefa !important;
                                                  }
                                                  #mwai-static-header {
                                                    max-width: none !important;
                                                    padding: 0 0 12mm !important;
                                                  }
                                                  #mwai-static-footer {
                                                    max-width: none !important;
                                                    margin-top: 6mm !important;
                                                    padding: 4mm 0 0 !important;
                                                  }
                                                }
                                                """;

    /// <summary>
    /// Defines <c>GetContentSecurityPolicy</c> for the visual briefing feature.
    /// </summary>
    public static string GetContentSecurityPolicy(VisualBriefingArtifactParts parts)
    {
        var echartsHash = string.IsNullOrWhiteSpace(parts.EChartsScript) ? string.Empty : $" {ScriptCspHash(parts.EChartsScript)}";
        return $"default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src {ScriptCspHash(parts.RuntimeScript)}{echartsHash}; font-src 'none'; media-src 'none'; frame-src 'none'; connect-src 'none'; form-action 'none'; base-uri 'none'; object-src 'none'; frame-ancestors 'self'";
    }

    /// <summary>
    /// Defines <c>ScriptCspHash</c> for the visual briefing feature.
    /// </summary>
    private static string ScriptCspHash(string script) => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'";

    /// <summary>
    /// Defines <c>BuildRuntimeScript</c> for the visual briefing feature.
    /// </summary>
    private static string BuildRuntimeScript(string aiStudioVersion) =>
        RUNTIME_SCRIPT.Replace(
            """
            "__MWAI_AI_STUDIO_VERSION__"
            """,
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
    /// Defines <c>HtmlEncode</c> for the visual briefing feature.
    /// </summary>
    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);

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
    /// Defines <c>CreateExportManifest</c> for the visual briefing feature.
    /// </summary>
    private static VisualBriefingExportManifest CreateExportManifest(VisualBriefingManifest manifest, VisualBriefingRevisionRequest request, string documentHash, string aiStudioVersion, string runtimeAIStudioVersion)
    {
        var source = request.ExportMetadataSource;
        return new()
        {
            BriefingId = manifest.BriefingId,
            RevisionId = request.RevisionId ?? Guid.NewGuid(),
            ParentRevisionId = request.ParentRevisionId,
            Name = source?.Name ?? manifest.Name,
            Author = source?.Author ?? manifest.Author,
            CreatedAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow,
            TargetLanguage = source?.TargetLanguage ?? manifest.Settings.TargetLanguage,
            CustomTargetLanguage = source?.CustomTargetLanguage ?? manifest.Settings.CustomTargetLanguage,
            AudienceProfile = source?.AudienceProfile ?? manifest.Settings.AudienceProfile,
            AudienceAgeGroup = source?.AudienceAgeGroup ?? manifest.Settings.AudienceAgeGroup,
            AudienceOrganizationalLevel = source?.AudienceOrganizationalLevel ?? manifest.Settings.AudienceOrganizationalLevel,
            AudienceExpertise = source?.AudienceExpertise ?? manifest.Settings.AudienceExpertise,
            ShowSourceReferences = source?.ShowSourceReferences ?? manifest.Settings.ShowSourceReferences,
            ProtectionLevel = source?.ProtectionLevel ?? manifest.Settings.ProtectionLevel,
            CustomProtectionLevel = source?.CustomProtectionLevel ?? manifest.Settings.CustomProtectionLevel,
            AIStudioVersion = aiStudioVersion,
            RuntimeAIStudioVersion = runtimeAIStudioVersion,
            DocumentHash = documentHash,
        };
    }

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
        var source = request.ExportMetadataSource;
        var protectionLevel = source?.ProtectionLevel ?? manifest.Settings.ProtectionLevel;
        var customProtectionLevel = source?.CustomProtectionLevel ?? manifest.Settings.CustomProtectionLevel;
        var protection = protectionLevel is VisualBriefingProtectionLevel.OTHER
            ? customProtectionLevel
            : protectionLevel.ToString().Replace('_', ' ').ToLowerInvariant();

        var created = (request.CreatedAtUtc ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        var sourceAuthor = source?.Author ?? manifest.Author;
        var author = string.IsNullOrWhiteSpace(sourceAuthor) ? "—" : sourceAuthor;
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
                        var roles = group.Select(contribution => contribution.Role is VisualBriefingModelRole.DESIGN ? "presentation" : "content").Distinct(StringComparer.Ordinal);
                        return $"{group.Key} ({string.Join(", ", roles)})";
                    }));

        // The briefing body follows the chosen target language, but this footer is AI Studio's own
        // statement about the artifact and stays US English. Translations shipped inside an exported
        // artifact cannot be reviewed the way the app UI can, which uses the language plugin system.
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["createdWith"] = $"Created with MindWork AI Studio v{version}.",
            ["models"] = $"Contributing models: {models}.",
            ["createdAt"] = $"Revision created on {created}.",
            ["authors"] = $"Author(s): {author}.",
            ["protection"] = $"Protection level: {protection}.",
        };
    }
}