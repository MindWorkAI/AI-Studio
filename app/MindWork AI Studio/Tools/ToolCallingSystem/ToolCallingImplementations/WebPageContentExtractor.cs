using System.Net;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

internal static class WebPageContentExtractor
{
    private const int MIN_SEMANTIC_CONTENT_CHARACTERS = 200;
    private const int MAX_SEMANTIC_CANDIDATES = 100;
    private const int MAX_OUTLINE_ITEM_CHARACTERS = 200;
    private const int MAX_METADATA_CHARACTERS = 1000;
    private const int MAX_AUTHOR_CHARACTERS = 200;
    private const int MAX_AUTHORS = 10;
    private const int MAX_JSON_LD_SCRIPTS = 20;
    private const int MAX_JSON_LD_BYTES = 256 * 1024;
    private const int MAX_JSON_LD_DEPTH = 32;

    private static readonly HashSet<string> ARTICLE_JSON_LD_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "Article", "NewsArticle", "BlogPosting", "Report", "TechArticle", "ScholarlyArticle"
    };

    private static readonly HashSet<string> PAGE_JSON_LD_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "WebPage", "ProfilePage", "FAQPage", "QAPage"
    };

    private static readonly HashSet<string> HARD_REMOVED_ELEMENT_NAMES = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "template", "nav", "dialog", "iframe", "object", "embed", "canvas", "svg",
        "button", "input", "select", "textarea"
    };

    private static readonly HashSet<string> HARD_REMOVED_ROLES = new(StringComparer.OrdinalIgnoreCase)
    {
        "navigation", "dialog", "alertdialog"
    };

    private static readonly HashSet<string> REMOVED_CLASS_OR_ID_TOKENS = new(StringComparer.OrdinalIgnoreCase)
    {
        "cookie-banner", "cookie-consent", "consent-banner", "newsletter-popup", "share-buttons", "social-share"
    };

    public static ExtractedWebPage Extract(HTMLParser htmlParser, HtmlDocument document, Uri finalUrl)
    {
        var jsonLdMetadata = ExtractJsonLdMetadata(document, finalUrl);
        var contentBaseUrl = ResolveUrl(finalUrl, GetAttribute(document.DocumentNode.SelectSingleNode("//base[@href]"), "href")) ?? finalUrl;
        var sourceRoot = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;
        var cleanedRoot = sourceRoot.CloneNode(true);
        RemoveHardNoise(cleanedRoot);

        var contentRoot = SelectContentRoot(cleanedRoot);
        if (ReferenceEquals(contentRoot, cleanedRoot))
            RemovePageLevelSupportingNodes(contentRoot);
        RemoveImagesWithoutAltText(contentRoot);
        MakeResourceUrlsAbsolute(contentRoot, contentBaseUrl);

        var outline = contentRoot
            .Descendants()
            .Where(x => x.Name is "h1" or "h2" or "h3")
            .Select(GetNodeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => LimitLength(x, MAX_OUTLINE_ITEM_CHARACTERS))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var markdown = htmlParser.ParseToMarkdown(contentRoot.InnerHtml)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var canonicalUrl = ResolveUrl(
            finalUrl,
            FirstNonEmpty(
                GetCanonicalHref(document),
                jsonLdMetadata.PageUrl?.ToString() ?? string.Empty,
                GetMetaContent(document, "property", "og:url")));
        var title = FirstNonEmpty(
            jsonLdMetadata.Title,
            GetMetaContent(document, "property", "og:title"),
            GetMetaContent(document, "name", "citation_title"),
            GetMetaContent(document, "name", "dc.title"),
            GetItemPropValue(document, "headline"),
            GetNodeText(contentRoot.SelectSingleNode(".//h1")),
            GetMetaContent(document, "name", "twitter:title"),
            htmlParser.ExtractTitle(document));
        var description = FirstNonEmpty(
            jsonLdMetadata.Description,
            GetMetaContent(document, "property", "og:description"),
            GetMetaContent(document, "name", "description"),
            GetMetaContent(document, "name", "dc.description"),
            GetItemPropValue(document, "description"),
            GetMetaContent(document, "name", "twitter:description"));
        var authors = BuildAuthors(document, jsonLdMetadata.Authors);
        var publishedTime = FirstNonEmpty(
            jsonLdMetadata.PublishedTime,
            GetMetaContent(document, "property", "article:published_time"),
            GetMetaContent(document, "name", "citation_publication_date"),
            GetMetaContent(document, "name", "citation_date"),
            GetMetaContent(document, "name", "dc.date"),
            GetItemPropValue(document, "datePublished"));
        var modifiedTime = FirstNonEmpty(
            jsonLdMetadata.ModifiedTime,
            GetMetaContent(document, "property", "article:modified_time"),
            GetItemPropValue(document, "dateModified"));
        var language = FirstNonEmpty(
            GetAttribute(document.DocumentNode.SelectSingleNode("//html"), "lang"),
            jsonLdMetadata.Language,
            GetMetaContent(document, "property", "og:locale"),
            GetMetaContent(document, "name", "dc.language"),
            GetItemPropValue(document, "inLanguage"),
            GetMetaContent(document, "http-equiv", "content-language"));
        var siteName = FirstNonEmpty(
            GetMetaContent(document, "property", "og:site_name"),
            jsonLdMetadata.SiteName);

        return new ExtractedWebPage
        {
            Title = title,
            Description = description,
            Authors = authors,
            PublishedTime = publishedTime,
            ModifiedTime = modifiedTime,
            Language = language,
            SiteName = siteName,
            CanonicalUrl = canonicalUrl,
            Markdown = markdown,
            Outline = outline,
        };
    }

    private static JsonLdMetadata ExtractJsonLdMetadata(HtmlDocument document, Uri finalUrl)
    {
        JsonLdCandidate? bestCandidate = null;
        var inspectedBytes = 0;
        var scripts = document.DocumentNode
            .SelectNodes("//script[@type]")?
            .Where(x => x.GetAttributeValue("type", string.Empty).StartsWith("application/ld+json", StringComparison.OrdinalIgnoreCase))
            .Take(MAX_JSON_LD_SCRIPTS) ?? [];

        foreach (var script in scripts)
        {
            var json = script.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            var jsonBytes = Encoding.UTF8.GetByteCount(json);
            if (jsonBytes > MAX_JSON_LD_BYTES - inspectedBytes)
                continue;
            inspectedBytes += jsonBytes;

            try
            {
                using var jsonDocument = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = MAX_JSON_LD_DEPTH,
                });
                foreach (var jsonObject in EnumerateJsonLdObjects(jsonDocument.RootElement))
                {
                    var candidate = CreateJsonLdCandidate(jsonObject, finalUrl);
                    if (candidate is not null && (bestCandidate is null || candidate.Score > bestCandidate.Score))
                        bestCandidate = candidate;
                }
            }
            catch (JsonException)
            {
            }
        }

        return bestCandidate?.Metadata ?? new JsonLdMetadata();
    }

    private static IEnumerable<JsonElement> EnumerateJsonLdObjects(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var jsonObject in EnumerateJsonLdObjects(item))
                    yield return jsonObject;
            yield break;
        }

        if (element.ValueKind is not JsonValueKind.Object)
            yield break;

        yield return element;
        if (element.TryGetProperty("@graph", out var graph))
            foreach (var jsonObject in EnumerateJsonLdObjects(graph))
                yield return jsonObject;
    }

    private static JsonLdCandidate? CreateJsonLdCandidate(JsonElement jsonObject, Uri finalUrl)
    {
        var types = GetJsonStringValues(jsonObject, "@type").ToList();
        var isArticle = types.Any(x => IsJsonLdType(x, ARTICLE_JSON_LD_TYPES));
        var isPage = types.Any(x => IsJsonLdType(x, PAGE_JSON_LD_TYPES));
        if (!isArticle && !isPage)
            return null;

        var pageUrl = ResolveUrl(finalUrl, GetJsonPageUrl(jsonObject));
        var pageUrlMatches = pageUrl is not null && UrlsMatch(pageUrl, finalUrl);
        var title = FirstNonEmpty(GetJsonString(jsonObject, "headline"), GetJsonString(jsonObject, "name"));
        var score = isArticle ? 100 : 20;
        if (pageUrl is not null)
            score += pageUrlMatches ? 50 : -80;
        if (!string.IsNullOrWhiteSpace(title))
            score += 10;

        var authors = jsonObject.TryGetProperty("author", out var author)
            ? ReadJsonNames(author)
                .Select(x => NormalizeMetadataText(x, MAX_AUTHOR_CHARACTERS))
                .Where(x => !string.IsNullOrWhiteSpace(x) && !IsHttpUrl(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MAX_AUTHORS)
                .ToList()
            : [];
        var siteName = jsonObject.TryGetProperty("publisher", out var publisher)
            ? ReadJsonNames(publisher).FirstOrDefault() ?? string.Empty
            : string.Empty;

        return new JsonLdCandidate(score, new JsonLdMetadata
        {
            Title = title,
            Description = GetJsonString(jsonObject, "description"),
            Authors = authors,
            PublishedTime = GetJsonString(jsonObject, "datePublished"),
            ModifiedTime = GetJsonString(jsonObject, "dateModified"),
            Language = GetJsonString(jsonObject, "inLanguage"),
            SiteName = siteName,
            PageUrl = pageUrlMatches ? pageUrl : null,
        });
    }

    private static IEnumerable<string> ReadJsonNames(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.String)
        {
            yield return element.GetString() ?? string.Empty;
            yield break;
        }

        if (element.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var name in ReadJsonNames(item))
                    yield return name;
            yield break;
        }

        if (element.ValueKind is not JsonValueKind.Object)
            yield break;

        var nameValue = GetJsonString(element, "name");
        if (!string.IsNullOrWhiteSpace(nameValue))
        {
            yield return nameValue;
            yield break;
        }

        var combinedName = $"{GetJsonString(element, "givenName")} {GetJsonString(element, "familyName")}".Trim();
        if (!string.IsNullOrWhiteSpace(combinedName))
            yield return combinedName;
    }

    private static IEnumerable<string> GetJsonStringValues(JsonElement jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetProperty(propertyName, out var value))
            yield break;

        if (value.ValueKind is JsonValueKind.String)
        {
            yield return value.GetString() ?? string.Empty;
            yield break;
        }

        if (value.ValueKind is JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                if (item.ValueKind is JsonValueKind.String)
                    yield return item.GetString() ?? string.Empty;
    }

    private static string GetJsonString(JsonElement jsonObject, string propertyName) =>
        GetJsonStringValues(jsonObject, propertyName)
            .Select(x => NormalizeMetadataText(x, MAX_METADATA_CHARACTERS))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string GetJsonPageUrl(JsonElement jsonObject)
    {
        var url = FirstNonEmpty(GetJsonString(jsonObject, "url"), GetJsonString(jsonObject, "@id"));
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        if (!jsonObject.TryGetProperty("mainEntityOfPage", out var mainEntityOfPage))
            return string.Empty;
        if (mainEntityOfPage.ValueKind is JsonValueKind.String)
            return NormalizeMetadataText(mainEntityOfPage.GetString() ?? string.Empty, MAX_METADATA_CHARACTERS);
        if (mainEntityOfPage.ValueKind is JsonValueKind.Object)
            return FirstNonEmpty(GetJsonString(mainEntityOfPage, "@id"), GetJsonString(mainEntityOfPage, "url"));
        return string.Empty;
    }

    private static bool UrlsMatch(Uri left, Uri right) =>
        left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port &&
        left.AbsolutePath.TrimEnd('/').Equals(right.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static bool IsJsonLdType(string type, IReadOnlySet<string> knownTypes)
    {
        if (knownTypes.Contains(type))
            return true;

        var separatorIndex = type.LastIndexOfAny(['/', '#']);
        return separatorIndex >= 0 && separatorIndex < type.Length - 1 && knownTypes.Contains(type[(separatorIndex + 1)..]);
    }

    private static HtmlNode SelectContentRoot(HtmlNode body)
    {
        var bodyTextLength = GetNodeText(body).Length;
        var candidate = body
            .SelectNodes(".//main | .//*[@role='main'] | .//article")?
            .Distinct()
            .Take(MAX_SEMANTIC_CANDIDATES)
            .Select(x => new { Node = x, TextLength = GetNodeText(x).Length })
            .OrderByDescending(x => x.TextLength)
            .FirstOrDefault();
        if (candidate is null || candidate.TextLength < MIN_SEMANTIC_CONTENT_CHARACTERS)
            return body;

        var isMainRegion = candidate.Node.Name.Equals("main", StringComparison.OrdinalIgnoreCase) ||
                           candidate.Node.GetAttributeValue("role", string.Empty).Equals("main", StringComparison.OrdinalIgnoreCase);
        return isMainRegion || candidate.TextLength * 2 >= bodyTextLength
            ? candidate.Node
            : body;
    }

    private static void RemoveHardNoise(HtmlNode root)
    {
        foreach (var node in root.Descendants().Where(ShouldRemoveHard).Reverse().ToList())
            node.Remove();

        foreach (var form in root.Descendants("form").Reverse().ToList())
            UnwrapNode(form);
    }

    private static bool ShouldRemoveHard(HtmlNode node)
    {
        if (node.NodeType is HtmlNodeType.Comment || HARD_REMOVED_ELEMENT_NAMES.Contains(node.Name))
            return true;

        if (node.Attributes["hidden"] is not null ||
            node.GetAttributeValue("aria-hidden", string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (HARD_REMOVED_ROLES.Contains(node.GetAttributeValue("role", string.Empty)))
            return true;

        var style = string.Concat(node.GetAttributeValue("style", string.Empty).Where(x => !char.IsWhiteSpace(x))).ToLowerInvariant();
        if (style.Contains("display:none", StringComparison.Ordinal) || style.Contains("visibility:hidden", StringComparison.Ordinal))
            return true;

        return GetClassOrIdTokens(node).Any(REMOVED_CLASS_OR_ID_TOKENS.Contains);
    }

    private static void RemovePageLevelSupportingNodes(HtmlNode root)
    {
        var nodes = root.Descendants()
            .Where(IsSupportingNode)
            .Where(x => !x.Ancestors().Any(IsSemanticContentNode))
            .Reverse()
            .ToList();
        foreach (var node in nodes)
            node.Remove();
    }

    private static bool IsSupportingNode(HtmlNode node) =>
        node.Name is "aside" or "footer" ||
        node.GetAttributeValue("role", string.Empty).Equals("contentinfo", StringComparison.OrdinalIgnoreCase) ||
        node.GetAttributeValue("role", string.Empty).Equals("complementary", StringComparison.OrdinalIgnoreCase);

    private static bool IsSemanticContentNode(HtmlNode node) =>
        node.Name is "main" or "article" ||
        node.GetAttributeValue("role", string.Empty).Equals("main", StringComparison.OrdinalIgnoreCase);

    private static void UnwrapNode(HtmlNode node)
    {
        var parent = node.ParentNode;
        if (parent is null)
            return;

        foreach (var child in node.ChildNodes.ToList())
            parent.InsertBefore(child, node);
        node.Remove();
    }

    private static void RemoveImagesWithoutAltText(HtmlNode root)
    {
        foreach (var image in root.Descendants("img").ToList())
        {
            var alternativeText = FirstNonEmpty(
                image.GetAttributeValue("alt", string.Empty),
                image.GetAttributeValue("aria-label", string.Empty),
                image.GetAttributeValue("title", string.Empty));
            if (string.IsNullOrWhiteSpace(alternativeText))
                image.Remove();
            else
                image.SetAttributeValue("alt", alternativeText);
        }
    }

    private static IEnumerable<string> GetClassOrIdTokens(HtmlNode node) =>
        $"{node.GetAttributeValue("class", string.Empty)} {node.GetAttributeValue("id", string.Empty)}"
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void MakeResourceUrlsAbsolute(HtmlNode root, Uri baseUrl)
    {
        foreach (var node in root.DescendantsAndSelf())
        {
            MakeAttributeUrlAbsolute(node, "href", baseUrl);
            MakeAttributeUrlAbsolute(node, "src", baseUrl);
            MakeAttributeUrlAbsolute(node, "poster", baseUrl);
        }
    }

    private static void MakeAttributeUrlAbsolute(HtmlNode node, string attributeName, Uri baseUrl)
    {
        var attribute = node.Attributes[attributeName];
        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value))
            return;

        var value = WebUtility.HtmlDecode(attribute.Value).Trim();
        if (value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes.Remove(attribute);
            return;
        }

        if (Uri.TryCreate(baseUrl, value, out var absoluteUrl) && absoluteUrl is { Scheme: "http" or "https" })
            attribute.Value = absoluteUrl.ToString();
    }

    private static List<string> BuildAuthors(HtmlDocument document, IReadOnlyList<string> jsonLdAuthors)
    {
        var authors = jsonLdAuthors
            .Concat(GetMetaContents(document, "name", "citation_author"))
            .Concat(GetMetaContents(document, "name", "dc.creator"))
            .Concat(GetMetaContents(document, "name", "author"))
            .Concat(GetMetaContents(document, "property", "article:author"))
            .Concat(GetItemPropValues(document, "author"))
            .Select(x => NormalizeMetadataText(x, MAX_AUTHOR_CHARACTERS))
            .Where(x => !string.IsNullOrWhiteSpace(x) && !IsHttpUrl(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MAX_AUTHORS)
            .ToList();
        return authors;
    }

    private static string GetCanonicalHref(HtmlDocument document)
    {
        var canonicalNode = document.DocumentNode
            .SelectNodes("//link[@rel]")?
            .FirstOrDefault(x => x.GetAttributeValue("rel", string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("canonical", StringComparer.OrdinalIgnoreCase));
        return GetAttribute(canonicalNode, "href");
    }

    private static string GetItemPropValue(HtmlDocument document, string itemProp)
        => GetItemPropValues(document, itemProp).FirstOrDefault() ?? string.Empty;

    private static IEnumerable<string> GetItemPropValues(HtmlDocument document, string itemProp)
    {
        var nodes = document.DocumentNode
            .SelectNodes("//*[@itemprop]")?
            .Where(x => x.GetAttributeValue("itemprop", string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(itemProp, StringComparer.OrdinalIgnoreCase)) ?? [];
        foreach (var node in nodes)
        {
            var value = FirstNonEmpty(GetAttribute(node, "content"), GetAttribute(node, "datetime"), GetNodeText(node));
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static IEnumerable<string> GetMetaContents(HtmlDocument document, string attributeName, string attributeValue) =>
        document.DocumentNode
            .SelectNodes("//meta")?
            .Where(x => x.GetAttributeValue(attributeName, string.Empty).Equals(attributeValue, StringComparison.OrdinalIgnoreCase))
            .Select(x => GetAttribute(x, "content"))
            .Where(x => !string.IsNullOrWhiteSpace(x)) ?? [];

    private static string GetMetaContent(HtmlDocument document, string attributeName, string attributeValue) =>
        GetMetaContents(document, attributeName, attributeValue).FirstOrDefault() ?? string.Empty;

    private static string GetNodeText(HtmlNode? node)
    {
        if (node is null)
            return string.Empty;

        var text = WebUtility.HtmlDecode(node.InnerText);
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string GetAttribute(HtmlNode? node, string attributeName) =>
        NormalizeMetadataText(node?.GetAttributeValue(attributeName, string.Empty) ?? string.Empty, MAX_METADATA_CHARACTERS);

    private static Uri? ResolveUrl(Uri baseUrl, string url) =>
        Uri.TryCreate(baseUrl, url, out var resolvedUrl) && resolvedUrl is { Scheme: "http" or "https" }
            ? resolvedUrl
            : null;

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && url is { Scheme: "http" or "https" };

    private static string FirstNonEmpty(params string[] values) =>
        NormalizeMetadataText(values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty, MAX_METADATA_CHARACTERS);

    private static string NormalizeMetadataText(string value, int maxCharacters)
    {
        var decoded = WebUtility.HtmlDecode(value);
        var normalized = string.Join(' ', decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return LimitLength(normalized, maxCharacters);
    }

    private static string LimitLength(string value, int maxCharacters) =>
        value.Length <= maxCharacters ? value : value[..maxCharacters].TrimEnd();

    private sealed record JsonLdCandidate(int Score, JsonLdMetadata Metadata);

    private sealed class JsonLdMetadata
    {
        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<string> Authors { get; init; } = [];

        public string PublishedTime { get; init; } = string.Empty;

        public string ModifiedTime { get; init; } = string.Empty;

        public string Language { get; init; } = string.Empty;

        public string SiteName { get; init; } = string.Empty;

        public Uri? PageUrl { get; init; }
    }
}

internal sealed class ExtractedWebPage
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> Authors { get; init; }

    public required string PublishedTime { get; init; }

    public required string ModifiedTime { get; init; }

    public required string Language { get; init; }

    public required string SiteName { get; init; }

    public required Uri? CanonicalUrl { get; init; }

    public required string Markdown { get; init; }

    public required IReadOnlyList<string> Outline { get; init; }
}
