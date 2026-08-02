using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Markdig;
using Markdig.Syntax;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Extracts provider-neutral JSON candidates and validates their complete CLR contract.
/// </summary>
internal static partial class VisualBriefingStructuredResponseProcessor
{
    private static readonly MarkdownPipeline MARKDOWN_PIPELINE = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .DisableHtml()
        .Build();
    private static readonly NullabilityInfoContext NULLABILITY = new();
    private static readonly object CONTRACT_LOCK = new();
    private static readonly Dictionary<Type, ContractShape> CONTRACTS = [];

    /// <summary>
    /// Parses every eligible candidate and returns the last fully valid response.
    /// </summary>
    /// <typeparam name="T">The strict response type.</typeparam>
    /// <param name="answer">The complete model answer.</param>
    /// <param name="validate">The semantic stage validator.</param>
    /// <returns>The selected response or a safe issue for the repair attempt.</returns>
    internal static VisualBriefingStructuredResponseResult<T> Process<T>(
        string answer,
        Func<T, VisualBriefingContractIssue?> validate)
        where T : class
    {
        var rawCandidate = new ResponseCandidate(
            answer,
            VisualBriefingStructuredResponseEnvelope.RAW_RESPONSE,
            1,
            1,
            1);
        var rawResult = Evaluate(rawCandidate, validate);
        if (rawResult.Response is not null)
            return rawResult;

        var markdownCandidates = ExtractMarkdownCandidates(answer);
        if (markdownCandidates.Count == 0)
            return rawResult;

        VisualBriefingStructuredResponseResult<T>? lastValid = null;
        VisualBriefingStructuredResponseResult<T>? lastResult = null;
        for (var index = 0; index < markdownCandidates.Count; index++)
        {
            var candidate = markdownCandidates[index] with
            {
                CandidateIndex = index + 1,
                CandidateCount = markdownCandidates.Count,
            };
            var result = Evaluate(candidate, validate);
            lastResult = result;
            if (result.Response is not null)
                lastValid = result;
        }

        return lastValid ?? lastResult!;
    }

    /// <summary>
    /// Renders a compact grammar from the same CLR types used for strict parsing.
    /// </summary>
    /// <typeparam name="T">The response contract type.</typeparam>
    /// <returns>A provider-neutral contract grammar.</returns>
    internal static string BuildContractGrammar<T>()
        where T : class
    {
        var root = GetContract(typeof(T));
        var shapes = EnumerateObjectShapes(root);
        var builder = new StringBuilder();
        builder.AppendLine("Strict JSON grammar generated from the active response contract:");
        foreach (var shape in shapes)
        {
            builder.Append(shape.Name);
            builder.Append(" = {");
            for (var index = 0; index < shape.Properties.Count; index++)
            {
                var property = shape.Properties[index];
                if (index > 0)
                    builder.Append(", ");
                builder.Append('"');
                builder.Append(property.Name);
                builder.Append('"');
                if (!property.Required)
                    builder.Append('?');
                builder.Append(": ");
                builder.Append(Describe(property.Shape));
                if (property.AllowsNull)
                    builder.Append(" | null");
            }
            builder.AppendLine("}");
        }
        builder.Append(
            "Every object may contain only the properties shown above. Required properties must be present even when their value is null.");
        return builder.ToString();
    }

    private static VisualBriefingStructuredResponseResult<T> Evaluate<T>(
        ResponseCandidate candidate,
        Func<T, VisualBriefingContractIssue?> validate)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(candidate.Json))
            return Rejected<T>(
                candidate,
                VisualBriefingStructuredResponseIssueKind.EMPTY_RESPONSE,
                VisualBriefingFailureCode.RESPONSE_JSON_INVALID,
                VisualBriefingValidationRule.JSON_INVALID,
                "The model returned an empty structured response.",
                expected: "JSON object");

        var firstContent = candidate.Json.FirstOrDefault(character => !char.IsWhiteSpace(character));
        if (firstContent is not '{')
            return Rejected<T>(
                candidate,
                VisualBriefingStructuredResponseIssueKind.ROOT_NOT_OBJECT,
                VisualBriefingFailureCode.RESPONSE_JSON_INVALID,
                VisualBriefingValidationRule.JSON_INVALID,
                "The structured response root must be a JSON object.",
                expected: "JSON object");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(candidate.Json);
        }
        catch (JsonException exception)
        {
            var kind = ClassifySyntax(candidate.Json);
            return Rejected<T>(
                candidate,
                kind,
                VisualBriefingFailureCode.RESPONSE_JSON_INVALID,
                VisualBriefingValidationRule.JSON_INVALID,
                SyntaxIssue(kind),
                lineNumber: ToResponseLine(candidate, exception.LineNumber),
                bytePositionInLine: exception.BytePositionInLine,
                expected: "valid JSON object");
        }

        using (document)
        {
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return Rejected<T>(
                    candidate,
                    VisualBriefingStructuredResponseIssueKind.ROOT_NOT_OBJECT,
                    VisualBriefingFailureCode.RESPONSE_JSON_INVALID,
                    VisualBriefingValidationRule.JSON_INVALID,
                    "The structured response root must be a JSON object.",
                    expected: "JSON object");

            var contractIssue = Inspect(
                document.RootElement,
                GetContract(typeof(T)),
                "$",
                allowsNull: false);
            if (contractIssue is not null)
                return Rejected<T>(
                    candidate,
                    contractIssue.Kind,
                    contractIssue.Kind is VisualBriefingStructuredResponseIssueKind.UNKNOWN_FIELD or
                        VisualBriefingStructuredResponseIssueKind.REQUIRED_FIELD_MISSING
                        ? VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID
                        : VisualBriefingFailureCode.RESPONSE_JSON_INVALID,
                    contractIssue.Kind is VisualBriefingStructuredResponseIssueKind.UNKNOWN_FIELD
                        ? VisualBriefingValidationRule.UNKNOWN_FIELD
                        : VisualBriefingValidationRule.JSON_INVALID,
                    ContractIssueMessage(contractIssue),
                    contractIssue.Path,
                    fieldName: contractIssue.FieldName,
                    expected: contractIssue.Expected);
        }

        T? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<T>(candidate.Json, VisualBriefingJson.Canonical);
        }
        catch (JsonException exception)
        {
            // The JSON itself parsed, so this is a contract violation, not a syntax error. Naming
            // the expected shape of the failing path is what makes the repair turn actionable:
            return Rejected<T>(
                candidate,
                VisualBriefingStructuredResponseIssueKind.TYPE_MISMATCH,
                VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
                VisualBriefingValidationRule.VALUE_TYPE_INVALID,
                "A JSON value does not match the required contract type.",
                SafeJsonPath(exception.Path),
                ToResponseLine(candidate, exception.LineNumber),
                exception.BytePositionInLine,
                expected: DescribeAtPath(typeof(T), exception.Path));
        }

        if (parsed is null)
            return Rejected<T>(
                candidate,
                VisualBriefingStructuredResponseIssueKind.EMPTY_RESPONSE,
                VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
                VisualBriefingValidationRule.JSON_INVALID,
                "The model returned an empty structured response.",
                expected: "JSON object");

        var semanticIssue = validate(parsed);
        if (semanticIssue is null)
            return new(parsed, null);
        if (semanticIssue.Diagnostic is not null)
            ApplyCandidate(semanticIssue.Diagnostic, candidate);
        else
            semanticIssue = semanticIssue with
            {
                // Expected carries a contract shape, never a rule name. The rule is reported
                // separately, so an unknown shape stays empty:
                Diagnostic = CreateDiagnostic(
                    candidate,
                    VisualBriefingStructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID,
                    "$"),
            };
        return new(null, semanticIssue);
    }

    private static List<ResponseCandidate> ExtractMarkdownCandidates(string answer)
    {
        var document = Markdig.Markdown.Parse(answer, MARKDOWN_PIPELINE);
        return document.Descendants<FencedCodeBlock>()
            .Where(IsEligibleJsonBlock)
            .Select(block => new ResponseCandidate(
                block.Lines.ToString(),
                VisualBriefingStructuredResponseEnvelope.MARKDOWN_JSON_BLOCK,
                1,
                1,
                block.Line + 2))
            .ToList();
    }

    private static bool IsEligibleJsonBlock(FencedCodeBlock block)
    {
        var info = block.Info?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(info))
            return true;
        var separator = info.IndexOfAny([' ', '\t', '\r', '\n']);
        var language = separator < 0 ? info : info[..separator];
        return string.Equals(language, "json", StringComparison.OrdinalIgnoreCase);
    }

    private static VisualBriefingStructuredResponseResult<T> Rejected<T>(
        ResponseCandidate candidate,
        VisualBriefingStructuredResponseIssueKind kind,
        VisualBriefingFailureCode code,
        VisualBriefingValidationRule rule,
        string issue,
        string jsonPath = "$",
        long? lineNumber = null,
        long? bytePositionInLine = null,
        string fieldName = "",
        string expected = "")
        where T : class =>
        new(
            null,
            new(
                code,
                issue,
                rule,
                CreateDiagnostic(
                    candidate,
                    kind,
                    jsonPath,
                    lineNumber,
                    bytePositionInLine,
                    fieldName,
                    expected)));

    private static VisualBriefingStructuredResponseDiagnostic CreateDiagnostic(
        ResponseCandidate candidate,
        VisualBriefingStructuredResponseIssueKind kind,
        string jsonPath,
        long? lineNumber = null,
        long? bytePositionInLine = null,
        string fieldName = "",
        string expected = "") =>
        new()
        {
            IssueKind = kind,
            Envelope = candidate.Envelope,
            CandidateIndex = candidate.CandidateIndex,
            CandidateCount = candidate.CandidateCount,
            JsonPath = SafeJsonPath(jsonPath),
            LineNumber = lineNumber,
            BytePositionInLine = bytePositionInLine,
            FieldName = SafeIdentifier(fieldName),
            Expected = SafeExpected(expected),
        };

    private static void ApplyCandidate(
        VisualBriefingStructuredResponseDiagnostic diagnostic,
        ResponseCandidate candidate)
    {
        diagnostic.Envelope = candidate.Envelope;
        diagnostic.CandidateIndex = candidate.CandidateIndex;
        diagnostic.CandidateCount = candidate.CandidateCount;
    }

    private static VisualBriefingStructuredResponseIssueKind ClassifySyntax(string json)
    {
        var stack = new Stack<char>();
        var insideString = false;
        var escaped = false;
        for (var index = 0; index < json.Length; index++)
        {
            var character = json[index];
            if (insideString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (character is '\\')
                {
                    escaped = true;
                    continue;
                }
                if (character is '"')
                    insideString = false;
                continue;
            }

            if (character is '"')
            {
                insideString = true;
                continue;
            }
            if (character is '{' or '[')
            {
                stack.Push(character);
                continue;
            }
            if (character is '}' or ']')
            {
                if (stack.Count == 0)
                    return VisualBriefingStructuredResponseIssueKind.INVALID_SYNTAX;
                var opening = stack.Pop();
                if (opening is '{' && character is not '}' ||
                    opening is '[' && character is not ']')
                    return VisualBriefingStructuredResponseIssueKind.INVALID_SYNTAX;
                if (stack.Count == 0)
                    return json[(index + 1)..].Any(characterAfterRoot => !char.IsWhiteSpace(characterAfterRoot))
                        ? VisualBriefingStructuredResponseIssueKind.TRAILING_CONTENT
                        : VisualBriefingStructuredResponseIssueKind.INVALID_SYNTAX;
            }
        }

        return insideString || stack.Count > 0
            ? VisualBriefingStructuredResponseIssueKind.UNEXPECTED_END
            : VisualBriefingStructuredResponseIssueKind.INVALID_SYNTAX;
    }

    private static string SyntaxIssue(VisualBriefingStructuredResponseIssueKind kind) => kind switch
    {
        VisualBriefingStructuredResponseIssueKind.UNEXPECTED_END =>
            "The JSON response ended before its root object was complete.",
        VisualBriefingStructuredResponseIssueKind.TRAILING_CONTENT =>
            "The JSON root object is followed by additional non-whitespace content.",
        _ => "The model response contains invalid JSON syntax.",
    };

    private static long? ToResponseLine(ResponseCandidate candidate, long? candidateLine) =>
        candidateLine is null ? null : candidate.StartLine + candidateLine;

    private static ContractInspectionIssue? Inspect(
        JsonElement element,
        ContractShape shape,
        string path,
        bool allowsNull)
    {
        if (element.ValueKind is JsonValueKind.Null)
            return allowsNull
                ? null
                : new(
                    VisualBriefingStructuredResponseIssueKind.TYPE_MISMATCH,
                    path,
                    string.Empty,
                    Describe(shape));

        if (!MatchesKind(element.ValueKind, shape.Kind))
            return new(
                VisualBriefingStructuredResponseIssueKind.TYPE_MISMATCH,
                path,
                string.Empty,
                Describe(shape));

        switch (shape.Kind)
        {
            case ContractShapeKind.ANY:
            case ContractShapeKind.STRING:
            case ContractShapeKind.NUMBER:
            case ContractShapeKind.BOOLEAN:
                return null;
            case ContractShapeKind.ENUM:
            {
                var value = element.GetString();
                return value is not null && shape.EnumValues.Contains(value, StringComparer.Ordinal)
                    ? null
                    : new(
                        VisualBriefingStructuredResponseIssueKind.ENUM_VALUE_INVALID,
                        path,
                        string.Empty,
                        Describe(shape));
            }
            case ContractShapeKind.ARRAY:
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var issue = Inspect(item, shape.Element!, $"{path}[{index}]", allowsNull: false);
                    if (issue is not null)
                        return issue;
                    index++;
                }
                return null;
            }
            case ContractShapeKind.DICTIONARY:
                foreach (var property in element.EnumerateObject())
                {
                    var issue = Inspect(property.Value, shape.Element!, $"{path}.*", allowsNull: false);
                    if (issue is not null)
                        return issue;
                }
                return null;
            case ContractShapeKind.OBJECT:
            {
                var properties = shape.Properties.ToDictionary(property => property.Name, StringComparer.Ordinal);
                foreach (var jsonProperty in element.EnumerateObject())
                {
                    if (properties.ContainsKey(jsonProperty.Name))
                        continue;
                    var safeField = SafeIdentifier(jsonProperty.Name);
                    return new(
                        VisualBriefingStructuredResponseIssueKind.UNKNOWN_FIELD,
                        string.IsNullOrEmpty(safeField) ? path : $"{path}.{safeField}",
                        safeField,
                        $"properties of {shape.Name}");
                }
                foreach (var property in shape.Properties.Where(property => property.Required))
                {
                    if (!element.TryGetProperty(property.Name, out _))
                        return new(
                            VisualBriefingStructuredResponseIssueKind.REQUIRED_FIELD_MISSING,
                            $"{path}.{property.Name}",
                            property.Name,
                            Describe(property.Shape) + (property.AllowsNull ? " | null" : string.Empty));
                }
                foreach (var property in shape.Properties)
                {
                    if (!element.TryGetProperty(property.Name, out var value))
                        continue;
                    var issue = Inspect(
                        value,
                        property.Shape,
                        $"{path}.{property.Name}",
                        property.AllowsNull);
                    if (issue is not null)
                        return issue;
                }
                return null;
            }
            default:
                throw new UnreachableException();
        }
    }

    private static bool MatchesKind(JsonValueKind valueKind, ContractShapeKind shapeKind) => shapeKind switch
    {
        ContractShapeKind.ANY => true,
        ContractShapeKind.STRING or ContractShapeKind.ENUM => valueKind is JsonValueKind.String,
        ContractShapeKind.NUMBER => valueKind is JsonValueKind.Number,
        ContractShapeKind.BOOLEAN => valueKind is JsonValueKind.True or JsonValueKind.False,
        ContractShapeKind.ARRAY => valueKind is JsonValueKind.Array,
        ContractShapeKind.DICTIONARY or ContractShapeKind.OBJECT => valueKind is JsonValueKind.Object,
        _ => false,
    };

    private static string ContractIssueMessage(ContractInspectionIssue issue) => issue.Kind switch
    {
        VisualBriefingStructuredResponseIssueKind.UNKNOWN_FIELD when !string.IsNullOrEmpty(issue.FieldName) =>
            $"The model response contains the unknown field '{issue.FieldName}' at {issue.Path}.",
        VisualBriefingStructuredResponseIssueKind.UNKNOWN_FIELD =>
            $"The model response contains an unknown field at {issue.Path}.",
        VisualBriefingStructuredResponseIssueKind.REQUIRED_FIELD_MISSING =>
            $"The required field '{issue.FieldName}' is missing at {issue.Path}.",
        VisualBriefingStructuredResponseIssueKind.ENUM_VALUE_INVALID =>
            $"The JSON value at {issue.Path} is not one of the allowed enum values.",
        _ => $"The JSON value at {issue.Path} does not match the required type.",
    };

    private static ContractShape GetContract(Type type)
    {
        lock (CONTRACT_LOCK)
        {
            return BuildContract(type);
        }
    }

    private static ContractShape BuildContract(Type sourceType)
    {
        var nullableType = Nullable.GetUnderlyingType(sourceType);
        var type = nullableType ?? sourceType;
        if (CONTRACTS.TryGetValue(type, out var cached))
            return cached;

        var shape = new ContractShape(type.Name);
        CONTRACTS[type] = shape;
        if (type == typeof(JsonElement) || type == typeof(object))
        {
            shape.Kind = ContractShapeKind.ANY;
            return shape;
        }
        if (type == typeof(string) || type == typeof(Guid) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            shape.Kind = ContractShapeKind.STRING;

            // A format-bound string must be reproduced exactly. Naming the format keeps the grammar
            // honest, and makes it obvious in the prompt when a contract asks the model for an
            // opaque identifier it cannot reliably produce:
            shape.Format = type == typeof(Guid)
                ? "uuid"
                : type == typeof(string) ? string.Empty : "date-time";
            return shape;
        }
        if (type == typeof(bool))
        {
            shape.Kind = ContractShapeKind.BOOLEAN;
            return shape;
        }
        if (type.IsEnum)
        {
            shape.Kind = ContractShapeKind.ENUM;
            shape.EnumValues.AddRange(Enum.GetNames(type));
            return shape;
        }
        if (IsNumber(type))
        {
            shape.Kind = ContractShapeKind.NUMBER;
            return shape;
        }
        if (TryGetDictionaryValueType(type, out var dictionaryValueType))
        {
            shape.Kind = ContractShapeKind.DICTIONARY;
            shape.Element = BuildContract(dictionaryValueType);
            return shape;
        }
        if (TryGetEnumerableElementType(type, out var elementType))
        {
            shape.Kind = ContractShapeKind.ARRAY;
            shape.Element = BuildContract(elementType);
            return shape;
        }

        shape.Kind = ContractShapeKind.OBJECT;
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
            if (ignore?.Condition is JsonIgnoreCondition.Always)
                continue;
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                       JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var nullability = NULLABILITY.Create(property);
            var allowsNull = Nullable.GetUnderlyingType(property.PropertyType) is not null ||
                             !property.PropertyType.IsValueType &&
                             nullability.ReadState is NullabilityState.Nullable;
            shape.Properties.Add(new(
                name,
                BuildContract(property.PropertyType),
                property.GetCustomAttribute<JsonRequiredAttribute>() is not null,
                allowsNull));
        }
        return shape;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionary = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() is var definition &&
                (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>)) &&
                candidate.GetGenericArguments()[0] == typeof(string));
        if (dictionary is null)
        {
            valueType = typeof(object);
            return false;
        }
        valueType = dictionary.GetGenericArguments()[1];
        return true;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }
        var enumerable = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable is null || type == typeof(string))
        {
            elementType = typeof(object);
            return false;
        }
        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }

    private static bool IsNumber(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) ||
        type == typeof(short) || type == typeof(ushort) ||
        type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong) ||
        type == typeof(float) || type == typeof(double) ||
        type == typeof(decimal);

    private static IReadOnlyList<ContractShape> EnumerateObjectShapes(ContractShape root)
    {
        List<ContractShape> result = [];
        HashSet<ContractShape> visited = [];
        Queue<ContractShape> pending = new();
        pending.Enqueue(root);
        while (pending.TryDequeue(out var shape))
        {
            if (!visited.Add(shape))
                continue;
            if (shape.Kind is ContractShapeKind.OBJECT)
            {
                result.Add(shape);
                foreach (var property in shape.Properties)
                    pending.Enqueue(property.Shape);
            }
            else if (shape.Element is not null)
                pending.Enqueue(shape.Element);
        }
        return result;
    }

    /// <summary>
    /// Names the contract shape the model should have produced at one JSON path.
    /// </summary>
    /// <param name="contractType">The active response contract type.</param>
    /// <param name="path">The JSON path reported by the deserializer, such as $.facts[0].sourceIds[0].</param>
    /// <returns>The expected shape, or an empty string when the path cannot be resolved.</returns>
    private static string DescribeAtPath(Type contractType, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var shape = GetContract(contractType);
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bracket = segment.IndexOf('[');
            var name = bracket < 0 ? segment : segment[..bracket];
            if (name is not ("$" or ""))
            {
                if (shape.Kind is ContractShapeKind.DICTIONARY && shape.Element is not null)
                    shape = shape.Element;
                else
                {
                    var property = shape.Properties.FirstOrDefault(item =>
                        string.Equals(item.Name, name, StringComparison.Ordinal));
                    if (property is null)
                        return string.Empty;
                    shape = property.Shape;
                }
            }

            // Every remaining "[n]" descends one array level of the resolved shape:
            for (var index = bracket; index >= 0; index = segment.IndexOf('[', index + 1))
            {
                if (shape.Kind is not ContractShapeKind.ARRAY || shape.Element is null)
                    return string.Empty;
                shape = shape.Element;
            }
        }

        return SafeExpected(Describe(shape));
    }

    private static string Describe(ContractShape shape) => shape.Kind switch
    {
        ContractShapeKind.ANY => "any JSON value",
        // Angle brackets, not parentheses: the diagnostic sanitizer SafeExpected drops parentheses:
        ContractShapeKind.STRING => string.IsNullOrEmpty(shape.Format) ? "string" : $"string<{shape.Format}>",
        ContractShapeKind.NUMBER => "number",
        ContractShapeKind.BOOLEAN => "boolean",
        ContractShapeKind.ENUM => string.Join(" | ", shape.EnumValues),
        ContractShapeKind.ARRAY => $"{Describe(shape.Element!)}[]",
        ContractShapeKind.DICTIONARY => $"object<string,{Describe(shape.Element!)}>",
        ContractShapeKind.OBJECT => shape.Name,
        _ => "JSON value",
    };

    private static string SafeIdentifier(string? value) =>
        value is not null && SafeIdentifierRegex().IsMatch(value) ? value : string.Empty;

    private static string SafeJsonPath(string? value) =>
        value is not null && SafeJsonPathRegex().IsMatch(value) ? value : "$";

    private static string SafeExpected(string value) =>
        value.Length <= 256 && SafeExpectedRegex().IsMatch(value) ? value : string.Empty;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex(@"^\$(?:\.[A-Za-z_][A-Za-z0-9_-]{0,63}|\[\d+\]|\.\*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeJsonPathRegex();

    [GeneratedRegex("^[A-Za-z0-9_ |<>,.\\[\\]-]{0,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeExpectedRegex();

    private sealed record ResponseCandidate(
        string Json,
        VisualBriefingStructuredResponseEnvelope Envelope,
        int CandidateIndex,
        int CandidateCount,
        int StartLine);

    private sealed record ContractInspectionIssue(
        VisualBriefingStructuredResponseIssueKind Kind,
        string Path,
        string FieldName,
        string Expected);

    private sealed class ContractShape(string name)
    {
        internal string Name { get; } = name;
        internal ContractShapeKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the required string format, such as uuid. A plain string shape has none.
        /// </summary>
        internal string Format { get; set; } = string.Empty;

        internal ContractShape? Element { get; set; }
        internal List<ContractProperty> Properties { get; } = [];
        internal List<string> EnumValues { get; } = [];
    }

    private sealed record ContractProperty(
        string Name,
        ContractShape Shape,
        bool Required,
        bool AllowsNull);

    private enum ContractShapeKind
    {
        ANY,
        STRING,
        NUMBER,
        BOOLEAN,
        ENUM,
        ARRAY,
        DICTIONARY,
        OBJECT,
    }
}
