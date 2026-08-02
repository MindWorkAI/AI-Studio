using System.Text.Json;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingArtifactService
{
    /// <summary>
    /// Lists declarative elements allowed in model-generated templates.
    /// </summary>
    private static readonly HashSet<string> ALLOWED_ELEMENTS = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "article", "aside", "button", "canvas", "caption", "dd", "details", "div", "dl", "dt",
        "fieldset", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "i", "img",
        "input", "label", "legend", "li", "main", "nav", "ol", "option", "output", "p", "progress", "section", "select",
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
    /// Defines <c>CssProhibitedRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex CSS_PROHIBITED = CssProhibitedRegex();

    /// <summary>
    /// Defines <c>CssProhibitedRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"(?:@import|@font-face|url\s*\(|expression\s*\(|javascript\s*:|behavior\s*:|-moz-binding|content\s*:|<\s*/?\s*script)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssProhibitedRegex();

    /// <summary>
    /// Defines <c>CssProtectedTargetRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex CSS_PROTECTED_TARGET = CssProtectedTargetRegex();

    /// <summary>
    /// Defines <c>CssProtectedTargetRegex</c> for the visual briefing feature.
    /// </summary>
    [GeneratedRegex(@"(?:#mwai-static-footer|\.mwai-footer|(?:^|[^A-Za-z0-9_-])(?:html|body|footer|:root)(?=[^A-Za-z0-9_-]))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex CssProtectedTargetRegex();

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
                    IsHiddenInTemplate(assetNode, root, css))
                    return $"The visual asset '{asset.AssetId}' is not visibly bound in the template.";
            }
        }

        var hasCharts = FindNode(root, ".//*[@data-mwai-chart]") is not null;
        if (usesCharts != hasCharts)
            return "Chart runtime selection does not match the template's data-mwai-chart bindings.";

        return string.Empty;
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
    /// Determines whether an element or one of its template ancestors is hidden.
    /// </summary>
    /// <param name="node">The bound asset element.</param>
    /// <param name="root">The validation root that encloses the model template.</param>
    /// <param name="css">The validated model stylesheet.</param>
    /// <returns><see langword="true"/> when the asset is hidden in the template.</returns>
    private static bool IsHiddenInTemplate(HtmlNode node, HtmlNode root, string css)
    {
        foreach (var candidate in node.AncestorsAndSelf().TakeWhile(candidate => candidate != root))
            if (FindAttribute(candidate, "hidden") is not null || string.Equals(candidate.GetAttributeValue("aria-hidden", string.Empty), "true", StringComparison.OrdinalIgnoreCase) || IsHiddenByCss(candidate, css))
                return true;

        return false;
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

            if (rule.Groups["selectors"].Value.Split(',').Any(selector => SimpleSelectorMatches(node, selector)))
                return true;
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
        var candidate = FinalSimpleSelector(selector);
        if (candidate.Length == 0)
            return false;

        var pseudo = FindPseudoStart(candidate);
        if (pseudo >= 0)
            candidate = candidate[..pseudo];

        // A pseudo-only selector cannot safely be evaluated by this deliberately small matcher.
        // Treating it as a match is conservative for the visibility invariant.
        if (candidate.Length == 0)
            return true;

        foreach (Match attributeSelector in AttributeSelectorRegex().Matches(candidate))
            if (!AttributeSelectorMatches(node, attributeSelector))
                return false;

        if (IdRegex().Matches(candidate).Any(idMatch => !string.Equals(node.Id, idMatch.Groups["id"].Value, StringComparison.Ordinal)))
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
        
        return !tag.Success || string.Equals(node.Name, tag.Groups["tag"].Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the final simple selector while ignoring combinators inside attribute values and pseudo functions.
    /// </summary>
    private static string FinalSimpleSelector(string selector)
    {
        var candidate = selector.Trim();
        var bracketDepth = 0;
        var parenthesisDepth = 0;
        var quote = '\0';

        for (var index = candidate.Length - 1; index >= 0; index--)
        {
            var character = candidate[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || candidate[index - 1] != '\\'))
                    quote = '\0';

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            switch (character)
            {
                case ']':
                    bracketDepth++;
                    continue;
                
                case '[':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    continue;
                
                case ')':
                    parenthesisDepth++;
                    continue;
                
                case '(':
                    parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                    continue;
            }

            if (bracketDepth == 0 && parenthesisDepth == 0 && (char.IsWhiteSpace(character) || character is '>' or '+' or '~'))
                return candidate[(index + 1)..].Trim();
        }

        return candidate;
    }

    /// <summary>
    /// Finds the first pseudo selector outside an attribute selector.
    /// </summary>
    private static int FindPseudoStart(string selector)
    {
        var bracketDepth = 0;
        var quote = '\0';

        for (var index = 0; index < selector.Length; index++)
        {
            var character = selector[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || selector[index - 1] != '\\'))
                    quote = '\0';

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '[')
                bracketDepth++;
            else if (character == ']')
                bracketDepth = Math.Max(0, bracketDepth - 1);
            else if (character == ':' && bracketDepth == 0)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Matches one CSS attribute selector against an element.
    /// </summary>
    private static bool AttributeSelectorMatches(HtmlNode node, Match selector)
    {
        var attribute = FindAttribute(node, selector.Groups["name"].Value);
        if (attribute is null)
            return false;

        var operation = selector.Groups["operator"].Value;
        if (operation.Length == 0)
            return true;

        var expected = selector.Groups["double"].Success
            ? selector.Groups["double"].Value
            : selector.Groups["single"].Success
                ? selector.Groups["single"].Value
                : selector.Groups["unquoted"].Value;

        var comparison = selector.Groups["modifier"].Value.Equals("i", StringComparison.OrdinalIgnoreCase)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return operation switch
        {
            "=" => string.Equals(attribute.Value, expected, comparison),
            "~=" => attribute.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(value => string.Equals(value, expected, comparison)),
            "|=" => string.Equals(attribute.Value, expected, comparison) || attribute.Value.StartsWith($"{expected}-", comparison),
            "^=" => attribute.Value.StartsWith(expected, comparison),
            "$=" => attribute.Value.EndsWith(expected, comparison),
            "*=" => attribute.Value.Contains(expected, comparison),
            
            _ => true,
        };
    }

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

    [GeneratedRegex(@"#(?<id>[A-Za-z][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"\.(?<class>[A-Za-z][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredClassRegex();

    [GeneratedRegex(@"^(?<tag>[A-Za-z][A-Za-z0-9-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex("""\[\s*(?<name>[A-Za-z_:][A-Za-z0-9_:.-]*)\s*(?:(?<operator>[~|^$*]?=)\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<unquoted>[^\]\s]+))\s*(?<modifier>[iIsS])?\s*)?\]""", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeSelectorRegex();
}