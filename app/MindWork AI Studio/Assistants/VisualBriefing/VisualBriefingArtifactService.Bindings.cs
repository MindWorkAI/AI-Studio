using System.Text.Json;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingArtifactService
{
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
    /// Defines <c>DataPathRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex DATA_PATH = DataPathRegex();

    /// <summary>
    /// Defines <c>LocalDataPathRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex LOCAL_DATA_PATH = LocalDataPathRegex();

    /// <summary>
    /// Defines <c>SafeSelectorRegex</c> for the visual briefing feature.
    /// </summary>
    private static readonly Regex SAFE_SELECTOR = SafeSelectorRegex();

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
        
        return path.Split('.', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not "__proto__" and not "prototype" and not "constructor");
    }

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
}