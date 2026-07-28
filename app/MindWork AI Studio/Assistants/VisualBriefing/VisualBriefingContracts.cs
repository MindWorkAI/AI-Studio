using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.VisualBriefing;

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingComponentKind>))]
public enum VisualBriefingComponentKind
{
    TEXT,
    METRIC,
    TABLE,
    CHART,
    ASSET,
    CALLOUT,
    TABS,
    ACCORDION,
    FILTERABLE_TABLE,
    SIMULATION,
}

/// <summary>
/// Identifies the JSON shape a content slot value must have. The shape follows from the planned
/// component kind alone, so plan, prompt, validator, and compiler always agree.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSlotType>))]
public enum VisualBriefingSlotType
{
    /// <summary>
    /// A JSON string, number, or boolean rendered as text.
    /// </summary>
    TEXT,

    /// <summary>
    /// A tabular object with columns and rows.
    /// </summary>
    TABLE,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingChartKind>))]
public enum VisualBriefingChartKind
{
    LINE,
    AREA,
    BAR,
    STACKED_BAR,
    SCATTER,
    PIE,
    DONUT,
    RADAR,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingControlKind>))]
public enum VisualBriefingControlKind
{
    TAB,
    FILTER,
    NUMBER,
    RANGE,
    SELECT,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingLayoutNodeKind>))]
public enum VisualBriefingLayoutNodeKind
{
    SECTION,
    STACK,
    GRID,
    COMPONENT,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingAlignment>))]
public enum VisualBriefingAlignment
{
    START,
    CENTER,
    END,
    STRETCH,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingDensity>))]
public enum VisualBriefingDensity
{
    COMPACT,
    COMFORTABLE,
    SPACIOUS,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingTypographyScale>))]
public enum VisualBriefingTypographyScale
{
    COMPACT,
    BALANCED,
    EDITORIAL,
    DISPLAY,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSurface>))]
public enum VisualBriefingSurface
{
    PLAIN,
    SUBTLE,
    RAISED,
    ACCENT,
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceFact
{
    [JsonRequired]
    public string EvidenceId { get; set; } = string.Empty;
    [JsonRequired]
    public string Statement { get; set; } = string.Empty;
    [JsonRequired]
    public List<string> SourceIds { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceMetric
{
    [JsonRequired]
    public string EvidenceId { get; set; } = string.Empty;
    [JsonRequired]
    public string Label { get; set; } = string.Empty;
    [JsonRequired]
    public decimal Value { get; set; }
    [JsonRequired]
    public string Unit { get; set; } = string.Empty;
    [JsonRequired]
    public List<string> SourceIds { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceTable
{
    [JsonRequired]
    public string EvidenceId { get; set; } = string.Empty;
    [JsonRequired]
    public string Title { get; set; } = string.Empty;
    [JsonRequired]
    public List<string> Columns { get; set; } = [];
    [JsonRequired]
    public List<List<JsonElement>> Rows { get; set; } = [];
    [JsonRequired]
    public List<string> SourceIds { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceResponse
{
    [JsonRequired]
    public int ContractVersion { get; set; }
    [JsonRequired]
    public List<VisualBriefingEvidenceFact> Facts { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingEvidenceMetric> Metrics { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingEvidenceTable> Tables { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingSourceCoverage> SourceCoverage { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingAssetPlanItem> AssetPlan { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceArtifact
{
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;
    public int ContractVersion { get; set; } = VisualBriefingVersions.EVIDENCE_CONTRACT;
    public Guid ArtifactId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public List<VisualBriefingEvidenceFact> Facts { get; set; } = [];
    public List<VisualBriefingEvidenceMetric> Metrics { get; set; } = [];
    public List<VisualBriefingEvidenceTable> Tables { get; set; } = [];
    public List<VisualBriefingSourceCoverage> SourceCoverage { get; set; } = [];
    public List<VisualBriefingAssetPlanItem> AssetPlan { get; set; } = [];
    public string Model { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanComponent
{
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingComponentKind Kind { get; set; }
    [JsonRequired]
    public List<string> EvidenceIds { get; set; } = [];
    [JsonRequired]
    public List<string> RequiredSlots { get; set; } = [];
    [JsonRequired]
    public string? AssetId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanSection
{
    [JsonRequired]
    public string SectionId { get; set; } = string.Empty;
    [JsonRequired]
    public string Purpose { get; set; } = string.Empty;
    [JsonRequired]
    public List<VisualBriefingPlanComponent> Components { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanResponse
{
    [JsonRequired]
    public int ContractVersion { get; set; }
    [JsonRequired]
    public List<VisualBriefingPlanSection> Sections { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanArtifact
{
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;
    public int ContractVersion { get; set; } = VisualBriefingVersions.PLAN_CONTRACT;
    public Guid ArtifactId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public List<VisualBriefingPlanSection> Sections { get; set; } = [];
    public string StructuralSignature { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingSlotValue
{
    [JsonRequired]
    public string SlotId { get; set; } = string.Empty;
    [JsonRequired]
    public JsonElement Value { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingChartSeries
{
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public List<decimal> Values { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingChartSpec
{
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingChartKind Kind { get; set; }
    [JsonRequired]
    public string Title { get; set; } = string.Empty;
    [JsonRequired]
    public List<string> Categories { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingChartSeries> Series { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingControlOption
{
    [JsonRequired]
    public string Value { get; set; } = string.Empty;
    [JsonRequired]
    public string Label { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingControlSpec
{
    [JsonRequired]
    public string ControlId { get; set; } = string.Empty;
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingControlKind Kind { get; set; }
    [JsonRequired]
    public JsonElement InitialValue { get; set; }
    [JsonRequired]
    public List<VisualBriefingControlOption> Options { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingFormulaSpec
{
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;
    [JsonRequired]
    public string OutputSlotId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingFormulaNode Formula { get; set; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingContentResponse
{
    [JsonRequired]
    public int ContractVersion { get; set; }
    [JsonRequired]
    public List<VisualBriefingSlotValue> Slots { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingChartSpec> Charts { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingControlSpec> Controls { get; set; } = [];
    [JsonRequired]
    public List<VisualBriefingFormulaSpec> Formulas { get; set; } = [];
    [JsonRequired]
    public Dictionary<string, string> AccessibilityTexts { get; set; } = new(StringComparer.Ordinal);
    [JsonRequired]
    public Dictionary<string, string> VisibleLabels { get; set; } = new(StringComparer.Ordinal);
    [JsonRequired]
    public Dictionary<string, string>? CustomLanguageLabels { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingResponsiveColumns
{
    [JsonRequired]
    public int Mobile { get; set; } = 1;
    [JsonRequired]
    public int Tablet { get; set; } = 1;
    [JsonRequired]
    public int Desktop { get; set; } = 1;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingLayoutNode
{
    [JsonRequired]
    public string NodeId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingLayoutNodeKind Kind { get; set; }
    [JsonRequired]
    public string? ComponentId { get; set; }
    [JsonRequired]
    public List<VisualBriefingLayoutNode> Children { get; set; } = [];
    [JsonRequired]
    public VisualBriefingResponsiveColumns? Columns { get; set; }
    [JsonRequired]
    public int Span { get; set; } = 1;
    [JsonRequired]
    public int Order { get; set; }
    [JsonRequired]
    public bool Emphasized { get; set; }
    [JsonRequired]
    public VisualBriefingAlignment Alignment { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingDesignTokens
{
    [JsonRequired]
    public string PrimaryColor { get; set; } = "#2563eb";
    [JsonRequired]
    public string AccentColor { get; set; } = "#7c3aed";
    [JsonRequired]
    public string TextColor { get; set; } = "#172033";
    [JsonRequired]
    public string BackgroundColor { get; set; } = "#ffffff";
    [JsonRequired]
    public int SpacingScale { get; set; } = 4;
    [JsonRequired]
    public int Radius { get; set; } = 12;
    [JsonRequired]
    public VisualBriefingTypographyScale TypographyScale { get; set; }
    [JsonRequired]
    public VisualBriefingDensity Density { get; set; }
    [JsonRequired]
    public VisualBriefingSurface Surface { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingDesignResponse
{
    [JsonRequired]
    public int ContractVersion { get; set; }
    [JsonRequired]
    public VisualBriefingLayoutNode Layout { get; set; } = new();
    [JsonRequired]
    public VisualBriefingDesignTokens Tokens { get; set; } = new();
}

public sealed record VisualBriefingCompilationResult(
    JsonElement Data,
    string TemplateHtml,
    string Css,
    string TemplateHash,
    string CssHash);

/// <summary>
/// Guards the parts AI Studio compiles itself. Everything the model controls is validated on the
/// JSON contract before compilation, so a rejected compiler output is always a defect in AI Studio.
/// Such a defect must never be reported as a contract violation, because the model cannot repair it.
/// </summary>
internal static class VisualBriefingCompilerInvariant
{
    private const string USER_MESSAGE =
        "AI Studio could not assemble this briefing because its own compiler produced an invalid part. This is a defect in AI Studio, not in the model response.";

    /// <summary>
    /// Fails the build when the compiled parts violate the artifact contract.
    /// </summary>
    /// <param name="stage">The stage running the compilation.</param>
    /// <param name="compilerIssue">The compiler issue, or an empty string when the parts are valid.</param>
    /// <exception cref="VisualBriefingBuildException">Thrown when the compiled parts are invalid.</exception>
    internal static void Guard(VisualBriefingBuildStage stage, string compilerIssue)
    {
        if (string.IsNullOrEmpty(compilerIssue))
            return;

        throw new VisualBriefingBuildException(
            VisualBriefingFailureCode.COMPILER_INVARIANT_VIOLATED,
            stage,
            USER_MESSAGE,
            $"Stage={stage}; CompilerIssue={compilerIssue}");
    }

    /// <summary>
    /// Runs a compilation and translates its structural failures into a compiler invariant failure.
    /// </summary>
    /// <typeparam name="T">The compilation result type.</typeparam>
    /// <param name="stage">The stage running the compilation.</param>
    /// <param name="compile">The compilation to run.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="VisualBriefingBuildException">Thrown when the compilation fails structurally.</exception>
    internal static T Guard<T>(VisualBriefingBuildStage stage, Func<T> compile)
    {
        try
        {
            return compile();
        }
        catch (InvalidDataException exception)
        {
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.COMPILER_INVARIANT_VIOLATED,
                stage,
                USER_MESSAGE,
                $"Stage={stage}; CompilerIssue={exception.Message}");
        }
    }
}

/// <summary>
/// Maps briefing sources to the short handles the model works with. Internal source identity stays
/// a GUID, but a model would have to reproduce it verbatim dozens of times, which it does not do
/// reliably. Prompt, validator, and source references all read the same canonical order from here.
/// </summary>
internal static class VisualBriefingSourceHandles
{
    /// <summary>
    /// Orders the sources canonically and pairs them with their handle. The order matches the order
    /// in which VisualBriefingSourcePreparationService builds the model attachments.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <returns>The handles with their sources, in canonical order.</returns>
    internal static IReadOnlyList<(string Handle, VisualBriefingSource Source)> Map(VisualBriefingManifest manifest) =>
        manifest.Sources
            .OrderBy(source => source.SourceId)
            .Select((source, index) => (Handle: Handle(index), Source: source))
            .ToArray();

    /// <summary>
    /// Names the handle of the source at one canonical position.
    /// </summary>
    /// <param name="index">The zero-based canonical position.</param>
    /// <returns>The source handle.</returns>
    internal static string Handle(int index) => $"s{index + 1}";
}

/// <summary>
/// Derives which component texts the model has to supply. A component text is either an assistive
/// alternative that never becomes visible, or a visible label that also acts as the accessible name.
/// Validator, layout compiler, and the content prompt all read these roles from here.
/// </summary>
internal static class VisualBriefingComponentTexts
{
    /// <summary>
    /// Determines whether a component renders a visible label. The caption of a table and the
    /// summary of an accordion are visible and are the accessible name of their component.
    /// </summary>
    /// <param name="kind">The planned component kind.</param>
    /// <returns>True when the component requires a visible label.</returns>
    internal static bool RequiresVisibleLabel(VisualBriefingComponentKind kind) =>
        kind is VisualBriefingComponentKind.TABLE or
            VisualBriefingComponentKind.FILTERABLE_TABLE or
            VisualBriefingComponentKind.ACCORDION;

    /// <summary>
    /// Determines whether a component requires an assistive alternative that never becomes visible.
    /// Charts bind it as an aria-label, and components with controls label those controls with it.
    /// </summary>
    /// <param name="kind">The planned component kind.</param>
    /// <returns>True when the model has to supply an accessibility text.</returns>
    internal static bool RequiresAccessibilityText(VisualBriefingComponentKind kind) =>
        kind is VisualBriefingComponentKind.CHART or
            VisualBriefingComponentKind.SIMULATION or
            VisualBriefingComponentKind.FILTERABLE_TABLE;

    /// <summary>
    /// Determines whether the accessibility text of a component comes from the validated evidence
    /// instead of the content model. Asset alternatives are written once by the evidence agent.
    /// </summary>
    /// <param name="kind">The planned component kind.</param>
    /// <returns>True when AI Studio supplies the accessibility text.</returns>
    internal static bool InheritsAccessibilityText(VisualBriefingComponentKind kind) =>
        kind is VisualBriefingComponentKind.ASSET;

    /// <summary>
    /// Lists the component IDs the model has to supply an accessibility text for.
    /// </summary>
    /// <param name="components">The planned components.</param>
    /// <returns>The component IDs in plan order.</returns>
    internal static string[] AccessibilityTextKeys(IEnumerable<VisualBriefingPlanComponent> components) =>
        components.Where(component => RequiresAccessibilityText(component.Kind))
            .Select(component => component.ComponentId)
            .ToArray();

    /// <summary>
    /// Lists the component IDs the model has to supply a visible label for.
    /// </summary>
    /// <param name="components">The planned components.</param>
    /// <returns>The component IDs in plan order.</returns>
    internal static string[] VisibleLabelKeys(IEnumerable<VisualBriefingPlanComponent> components) =>
        components.Where(component => RequiresVisibleLabel(component.Kind))
            .Select(component => component.ComponentId)
            .ToArray();
}

/// <summary>
/// Derives the required JSON shape of every planned content slot. Validator, layout compiler, and
/// the content prompt all read the slot types from here so that they cannot drift apart.
/// </summary>
internal static class VisualBriefingSlotTypes
{
    /// <summary>
    /// Determines the slot type of one planned slot.
    /// </summary>
    /// <param name="component">The planned component owning the slot.</param>
    /// <param name="slotId">The planned slot ID.</param>
    /// <returns>The required slot type.</returns>
    internal static VisualBriefingSlotType Expected(VisualBriefingPlanComponent component, string slotId) =>
        IsTableDataSlot(component, slotId) ? VisualBriefingSlotType.TABLE : VisualBriefingSlotType.TEXT;

    /// <summary>
    /// Determines whether a slot carries the tabular data of a table component.
    /// </summary>
    /// <param name="component">The planned component owning the slot.</param>
    /// <param name="slotId">The planned slot ID.</param>
    /// <returns>True when the slot carries tabular data.</returns>
    internal static bool IsTableDataSlot(VisualBriefingPlanComponent component, string slotId) =>
        component.Kind is VisualBriefingComponentKind.TABLE or VisualBriefingComponentKind.FILTERABLE_TABLE &&
        component.RequiredSlots.Count > 0 &&
        string.Equals(component.RequiredSlots[0], slotId, StringComparison.Ordinal);

    /// <summary>
    /// Maps every planned slot to its required slot type.
    /// </summary>
    /// <param name="sections">The planned sections.</param>
    /// <returns>The slot types by slot ID.</returns>
    internal static Dictionary<string, VisualBriefingSlotType> Map(IReadOnlyList<VisualBriefingPlanSection> sections)
    {
        Dictionary<string, VisualBriefingSlotType> types = new(StringComparer.Ordinal);
        foreach (var component in sections.SelectMany(section => section.Components))
        foreach (var slotId in component.RequiredSlots)
            types[slotId] = Expected(component, slotId);

        return types;
    }

    /// <summary>
    /// Names the required shape of a slot type. The wording stays within the sanitized character
    /// set of structured diagnostics, see VisualBriefingStructuredResponseProcessor.SafeExpected.
    /// </summary>
    /// <param name="type">The slot type.</param>
    /// <returns>The human-readable shape description.</returns>
    internal static string Describe(VisualBriefingSlotType type) => type switch
    {
        VisualBriefingSlotType.TABLE => "object with a columns array and a rows array of cells arrays",
        _ => "string, number, or boolean",
    };

    /// <summary>
    /// Checks a slot value against its required slot type.
    /// </summary>
    /// <param name="type">The required slot type.</param>
    /// <param name="value">The slot value returned by the model.</param>
    /// <returns>A short reason when the value does not match, otherwise an empty string.</returns>
    internal static string Validate(VisualBriefingSlotType type, JsonElement value)
    {
        if (type is VisualBriefingSlotType.TEXT)
            return value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? string.Empty
                : "A text slot requires a string, number, or boolean value.";

        if (value.ValueKind is not JsonValueKind.Object)
            return "A table slot requires an object with columns and rows.";

        if (value.EnumerateObject().Any(property => property.Name is not "columns" and not "rows"))
            return "A table slot must contain only columns and rows.";

        if (!value.TryGetProperty("columns", out var columns) ||
            columns.ValueKind is not JsonValueKind.Array ||
            columns.GetArrayLength() == 0)
            return "A table slot requires a non-empty columns array.";

        if (columns.EnumerateArray().Any(column =>
                column.ValueKind is not JsonValueKind.String ||
                string.IsNullOrWhiteSpace(column.GetString())))
            return "Every table column requires a non-empty name.";

        if (!value.TryGetProperty("rows", out var rows) || rows.ValueKind is not JsonValueKind.Array)
            return "A table slot requires a rows array.";

        var columnCount = columns.GetArrayLength();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind is not JsonValueKind.Object ||
                row.EnumerateObject().Any(property => property.Name is not "cells"))
                return "Every table row requires exactly one cells array.";

            if (!row.TryGetProperty("cells", out var cells) || cells.ValueKind is not JsonValueKind.Array)
                return "Every table row requires a cells array.";

            if (cells.GetArrayLength() != columnCount)
                return "Every table row requires exactly one cell per column.";

            if (cells.EnumerateArray().Any(cell =>
                    cell.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)))
                return "Every table cell requires a string, number, or boolean value.";
        }

        return string.Empty;
    }
}

internal static partial class VisualBriefingValidation
{
    private const int MAX_OPTION_VALUE_LENGTH = 128;

    private static readonly Regex ID = IdRegex();
    private static readonly Regex COLOR = ColorRegex();
    /// <summary>
    /// Lists tokens that never occur in ordinary target-language prose. Broader patterns such as a
    /// bare "document." or "=>" are deliberately absent: they reject normal sentences, and model text
    /// only ever reaches the artifact as text content.
    /// </summary>
    private static readonly string[] FORBIDDEN_MODEL_TEXT =
    [
        "data-mwai-", "javascript:", "echarts", "function(",
    ];

    internal static VisualBriefingContractIssue? ValidateEvidence(
        VisualBriefingManifest manifest,
        VisualBriefingEvidenceResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.EVIDENCE_CONTRACT)
            return Invalid(
                "The evidence response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED,
                "$.contractVersion",
                expected: "supported contract version");
        var evidenceIdLocations = response.Facts
            .Select((item, index) => (item.EvidenceId, Path: $"$.facts[{index}].evidenceId"))
            .Concat(response.Metrics
                .Select((item, index) => (item.EvidenceId, Path: $"$.metrics[{index}].evidenceId")))
            .Concat(response.Tables
                .Select((item, index) => (item.EvidenceId, Path: $"$.tables[{index}].evidenceId")))
            .ToArray();
        var invalidEvidenceId = FindInvalidOrDuplicateId(evidenceIdLocations);
        if (invalidEvidenceId is not null)
            return Invalid(
                "Evidence IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidEvidenceId,
                "evidenceId",
                "unique lowercase ID");
        var sourceIds = VisualBriefingSourceHandles.Map(manifest)
            .Select(item => item.Handle)
            .ToHashSet(StringComparer.Ordinal);
        if (response.SourceCoverage.Count != sourceIds.Count ||
            response.SourceCoverage.Select(item => item.SourceId).Distinct().Count() != sourceIds.Count ||
            response.SourceCoverage.Any(item =>
                !sourceIds.Contains(item.SourceId) ||
                string.IsNullOrWhiteSpace(item.Reason)))
            return new(
                VisualBriefingFailureCode.SOURCE_COVERAGE_INVALID,
                "Source coverage must contain every source exactly once.",
                VisualBriefingValidationRule.SOURCE_COVERAGE_INVALID);
        if (response.Facts.Any(item =>
                item.SourceIds.Count == 0 ||
                item.SourceIds.Distinct().Count() != item.SourceIds.Count ||
                item.SourceIds.Any(id => !sourceIds.Contains(id))) ||
            response.Metrics.Any(item =>
                item.SourceIds.Count == 0 ||
                item.SourceIds.Distinct().Count() != item.SourceIds.Count ||
                item.SourceIds.Any(id => !sourceIds.Contains(id))) ||
            response.Tables.Any(item =>
                item.SourceIds.Count == 0 ||
                item.SourceIds.Distinct().Count() != item.SourceIds.Count ||
                item.SourceIds.Any(id => !sourceIds.Contains(id)) ||
                item.Columns.Count == 0 ||
                item.Rows.Any(row => row.Count != item.Columns.Count)) ||
            response.Facts.Any(item => string.IsNullOrWhiteSpace(item.Statement)) ||
            response.Metrics.Any(item => string.IsNullOrWhiteSpace(item.Label)) ||
            response.Tables.Any(item => string.IsNullOrWhiteSpace(item.Title)))
            return Invalid(
                "Every evidence item must reference a supplied source.",
                VisualBriefingValidationRule.REFERENCE_INVALID);
        var assetIds = manifest.Sources
            .Where(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET)
            .Select(source => source.AssetId)
            .ToHashSet(StringComparer.Ordinal);
        if (response.AssetPlan.Count != assetIds.Count ||
            response.AssetPlan.Select(item => item.AssetId).Distinct(StringComparer.Ordinal).Count() != assetIds.Count ||
            response.AssetPlan.Any(item =>
                !assetIds.Contains(item.AssetId) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                string.IsNullOrWhiteSpace(item.AltText)))
            return new(
                VisualBriefingFailureCode.ASSET_PLAN_INVALID,
                "The asset plan must contain every visual asset exactly once.",
                VisualBriefingValidationRule.ASSET_PLAN_INVALID);
        return ContainsForbidden(response)
            ? Invalid(
                "Evidence must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    internal static VisualBriefingContractIssue? ValidatePlan(
        VisualBriefingEvidenceArtifact evidence,
        VisualBriefingPlanResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.PLAN_CONTRACT)
            return Invalid(
                "The plan response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED,
                "$.contractVersion",
                expected: "supported contract version");
        var evidenceIds = evidence.Facts.Select(item => item.EvidenceId)
            .Concat(evidence.Metrics.Select(item => item.EvidenceId))
            .Concat(evidence.Tables.Select(item => item.EvidenceId))
            .ToHashSet(StringComparer.Ordinal);
        var components = response.Sections.SelectMany(item => item.Components).ToArray();
        if (response.Sections.Count == 0)
            return Invalid(
                "Plan section and component IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                "$.sections",
                expected: "non-empty section array");
        var invalidSectionId = FindInvalidOrDuplicateId(response.Sections
            .Select((section, sectionIndex) =>
                (section.SectionId, Path: $"$.sections[{sectionIndex}].sectionId")));
        if (invalidSectionId is not null)
            return Invalid(
                "Plan section and component IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidSectionId,
                "sectionId",
                "unique lowercase ID");
        var emptyPurposeIndex = response.Sections.FindIndex(section => string.IsNullOrWhiteSpace(section.Purpose));
        if (emptyPurposeIndex >= 0)
            return Invalid(
                "Every plan section requires a purpose.",
                VisualBriefingValidationRule.REFERENCE_INVALID,
                $"$.sections[{emptyPurposeIndex}].purpose",
                "purpose",
                "non-empty string");
        var invalidComponentId = FindInvalidOrDuplicateId(response.Sections
            .SelectMany((section, sectionIndex) => section.Components
                .Select((component, componentIndex) =>
                    (component.ComponentId,
                        Path: $"$.sections[{sectionIndex}].components[{componentIndex}].componentId"))));
        if (invalidComponentId is not null)
            return Invalid(
                "Plan section and component IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidComponentId,
                "componentId",
                "unique lowercase ID");
        var invalidSlotId = FindInvalidOrDuplicateId(response.Sections
            .SelectMany((section, sectionIndex) => section.Components
                .SelectMany((component, componentIndex) => component.RequiredSlots
                    .Select((slotId, slotIndex) =>
                        (slotId,
                            Path: $"$.sections[{sectionIndex}].components[{componentIndex}].requiredSlots[{slotIndex}]")))));
        if (invalidSlotId is not null)
            return Invalid(
                "Plan slot IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidSlotId,
                expected: "unique lowercase ID");
        if (components.Any(item =>
                item.EvidenceIds.Count == 0 ||
                item.EvidenceIds.Distinct(StringComparer.Ordinal).Count() != item.EvidenceIds.Count ||
                item.EvidenceIds.Any(id => !evidenceIds.Contains(id)) ||
                item.RequiredSlots.Count == 0 ||
                !UniqueIds(item.RequiredSlots)))
            return Invalid(
                "Every component must reference valid evidence and unique required slots.",
                VisualBriefingValidationRule.REFERENCE_INVALID);
        var plannedAssetIds = components
            .Where(item => item.Kind is VisualBriefingComponentKind.ASSET)
            .Select(item => item.AssetId)
            .ToArray();
        var evidenceAssetIds = evidence.AssetPlan.Select(item => item.AssetId).ToHashSet(StringComparer.Ordinal);
        if (components.Any(item =>
                item.Kind is VisualBriefingComponentKind.ASSET && string.IsNullOrWhiteSpace(item.AssetId) ||
                item.Kind is not VisualBriefingComponentKind.ASSET && item.AssetId is not null) ||
            plannedAssetIds.Any(item => item is null) ||
            plannedAssetIds.Distinct(StringComparer.Ordinal).Count() != plannedAssetIds.Length ||
            !plannedAssetIds.Select(item => item!).ToHashSet(StringComparer.Ordinal).SetEquals(evidenceAssetIds) ||
            components.Where(item => item.Kind is not VisualBriefingComponentKind.ASSET)
                .Any(item => item.AssetId is not null))
            return Invalid(
                "The plan must include every visual asset exactly once.",
                VisualBriefingValidationRule.ASSET_PLAN_INVALID);
        return ContainsForbidden(response)
            ? Invalid(
                "The plan must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    internal static VisualBriefingContractIssue? ValidateContent(
        VisualBriefingPlanArtifact plan,
        VisualBriefingContentResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.CONTENT_CONTRACT)
            return Invalid(
                "The content response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED,
                "$.contractVersion",
                expected: "supported contract version");
        var components = plan.Sections.SelectMany(section => section.Components).ToArray();
        var componentById = components.ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        var chartComponentIds = components
            .Where(item => item.Kind is VisualBriefingComponentKind.CHART)
            .Select(item => item.ComponentId)
            .ToHashSet(StringComparer.Ordinal);
        var requiredSlots = components.SelectMany(item => item.RequiredSlots).ToArray();
        var slots = response.Slots.Select(item => item.SlotId).ToArray();
        var duplicateSlotIndex = FindDuplicateIndex(slots);
        if (duplicateSlotIndex >= 0)
            return Invalid(
                "Every required content slot must be fulfilled exactly once.",
                VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                $"$.slots[{duplicateSlotIndex}].slotId",
                "slotId",
                "unique planned slot ID");
        var requiredSlotSet = requiredSlots.ToHashSet(StringComparer.Ordinal);
        var unknownSlotIndex = Array.FindIndex(slots, slotId => !requiredSlotSet.Contains(slotId));
        if (unknownSlotIndex >= 0)
            return Invalid(
                "Every required content slot must be fulfilled exactly once.",
                VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                $"$.slots[{unknownSlotIndex}].slotId",
                "slotId",
                "planned slot ID");
        if (slots.Length != requiredSlots.Length ||
            !slots.ToHashSet(StringComparer.Ordinal).SetEquals(requiredSlotSet))
            return Invalid(
                "Every required content slot must be fulfilled exactly once.",
                VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                "$.slots",
                expected: "every planned slot exactly once");

        var slotTypes = VisualBriefingSlotTypes.Map(plan.Sections);
        for (var slotIndex = 0; slotIndex < response.Slots.Count; slotIndex++)
        {
            var slot = response.Slots[slotIndex];
            var slotType = slotTypes[slot.SlotId];
            var slotTypeIssue = VisualBriefingSlotTypes.Validate(slotType, slot.Value);
            if (!string.IsNullOrEmpty(slotTypeIssue))
                return Invalid(
                    slotTypeIssue,
                    VisualBriefingValidationRule.SLOT_VALUE_TYPE_INVALID,
                    $"$.slots[{slotIndex}].value",
                    "value",
                    VisualBriefingSlotTypes.Describe(slotType));

            // AI Studio derives the filter options of a filterable table from the first column and
            // compares them against the rendered cell text, so those cells must be text:
            var slotComponent = components.FirstOrDefault(item =>
                VisualBriefingSlotTypes.IsTableDataSlot(item, slot.SlotId) &&
                item.Kind is VisualBriefingComponentKind.FILTERABLE_TABLE);
            if (slotComponent is not null && !HasTextFirstColumn(slot.Value))
                return Invalid(
                    "The first column of a filterable table must contain text values.",
                    VisualBriefingValidationRule.SLOT_VALUE_TYPE_INVALID,
                    $"$.slots[{slotIndex}].value",
                    "value",
                    "string value in the first cell of every row");
        }

        HashSet<string> seenCharts = new(StringComparer.Ordinal);
        for (var chartIndex = 0; chartIndex < response.Charts.Count; chartIndex++)
        {
            var chart = response.Charts[chartIndex];
            if (!chartComponentIds.Contains(chart.ComponentId))
                return Invalid(
                    "A chart targets a component that is not a planned chart.",
                    VisualBriefingValidationRule.CHART_SET_INVALID,
                    $"$.charts[{chartIndex}].componentId",
                    "componentId",
                    "planned CHART component ID");
            if (!seenCharts.Add(chart.ComponentId))
                return Invalid(
                    "Every planned chart component requires exactly one chart.",
                    VisualBriefingValidationRule.CHART_SET_INVALID,
                    $"$.charts[{chartIndex}].componentId",
                    "componentId",
                    "unique planned CHART component ID");
            if (string.IsNullOrWhiteSpace(chart.Title))
                return Invalid(
                    "Every chart requires a title.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].title",
                    "title",
                    "non-empty target-language string");
            if (chart.Categories.Count == 0)
                return Invalid(
                    "Every chart requires categories.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].categories",
                    "categories",
                    "non-empty string array");
            var emptyCategoryIndex = chart.Categories.FindIndex(string.IsNullOrWhiteSpace);
            if (emptyCategoryIndex >= 0)
                return Invalid(
                    "Chart categories must be non-empty.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].categories[{emptyCategoryIndex}]",
                    expected: "non-empty string");
            if (chart.Series.Count == 0)
                return Invalid(
                    "Every chart requires at least one data series.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].series",
                    "series",
                    "non-empty series array");
            if (chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT &&
                chart.Series.Count != 1)
                return Invalid(
                    "Pie and donut charts require exactly one data series.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].series",
                    "series",
                    "exactly one series");
            for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
            {
                var series = chart.Series[seriesIndex];
                if (string.IsNullOrWhiteSpace(series.Name))
                    return Invalid(
                        "Every chart series requires a name.",
                        VisualBriefingValidationRule.CHART_DATA_INVALID,
                        $"$.charts[{chartIndex}].series[{seriesIndex}].name",
                        "name",
                        "non-empty target-language string");
                if (series.Values.Count != chart.Categories.Count)
                    return Invalid(
                        "Every chart series requires one value per category.",
                        VisualBriefingValidationRule.CHART_DATA_INVALID,
                        $"$.charts[{chartIndex}].series[{seriesIndex}].values",
                        "values",
                        "one numeric value per category");
            }
        }
        if (!seenCharts.SetEquals(chartComponentIds))
            return Invalid(
                "Every planned chart component requires exactly one chart.",
                VisualBriefingValidationRule.CHART_SET_INVALID,
                "$.charts",
                expected: "exactly one chart for every planned CHART component");

        HashSet<string> seenControls = new(StringComparer.Ordinal);
        for (var controlIndex = 0; controlIndex < response.Controls.Count; controlIndex++)
        {
            var control = response.Controls[controlIndex];
            if (!IsUsableId(control.ControlId) || !seenControls.Add(control.ControlId))
                return Invalid(
                    "Control IDs must be valid and unique.",
                    VisualBriefingValidationRule.CONTROL_ID_INVALID,
                    $"$.controls[{controlIndex}].controlId",
                    "controlId",
                    "unique lowercase ID");
            if (!componentById.TryGetValue(control.ComponentId, out var component))
                return Invalid(
                    "A control targets an unknown component.",
                    VisualBriefingValidationRule.CONTROL_TARGET_INVALID,
                    $"$.controls[{controlIndex}].componentId",
                    "componentId",
                    "planned interactive component ID");
            if (!ControlMatchesComponent(control.Kind, component.Kind))
                return Invalid(
                    "A control kind is incompatible with its planned component.",
                    VisualBriefingValidationRule.CONTROL_TARGET_INVALID,
                    $"$.controls[{controlIndex}].kind",
                    "kind",
                    ExpectedControlKinds(component.Kind));
            var controlIssue = ValidateControlState(control, controlIndex);
            if (controlIssue is not null)
                return controlIssue;
        }

        foreach (var component in components)
        {
            var controls = response.Controls
                .Where(control => control.ComponentId == component.ComponentId)
                .ToArray();
            if (component.Kind is VisualBriefingComponentKind.TABS)
            {
                if (controls.Length != 1 || controls[0].Kind is not VisualBriefingControlKind.TAB)
                    return Invalid(
                        "Every tabs component requires exactly one TAB control.",
                        VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID,
                        "$.controls",
                        expected: "exactly one TAB control for every planned TABS component");
                if (controls[0].Options.Count != component.RequiredSlots.Count)
                    return Invalid(
                        "Every tabs option requires one matching planned slot.",
                        VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID,
                        $"$.controls[{response.Controls.IndexOf(controls[0])}].options",
                        "options",
                        "one option per planned tab slot");
            }
            else if (component.Kind is VisualBriefingComponentKind.SIMULATION &&
                     controls.All(control =>
                         control.Kind is not (
                             VisualBriefingControlKind.NUMBER or
                             VisualBriefingControlKind.RANGE or
                             VisualBriefingControlKind.SELECT)))
                return Invalid(
                    "Every simulation requires at least one typed input control.",
                    VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID,
                    "$.controls",
                    expected: "NUMBER, RANGE, or SELECT control for every planned SIMULATION component");
        }

        HashSet<string> formulaOutputs = new(StringComparer.Ordinal);
        for (var formulaIndex = 0; formulaIndex < response.Formulas.Count; formulaIndex++)
        {
            var formula = response.Formulas[formulaIndex];
            if (!componentById.TryGetValue(formula.ComponentId, out var component) ||
                component.Kind is not VisualBriefingComponentKind.SIMULATION)
                return Invalid(
                    "A formula must target a planned simulation.",
                    VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                    $"$.formulas[{formulaIndex}].componentId",
                    "componentId",
                    "planned SIMULATION component ID");
            if (!component.RequiredSlots.Contains(formula.OutputSlotId, StringComparer.Ordinal))
                return Invalid(
                    "A formula output must target a slot of its simulation.",
                    VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                    $"$.formulas[{formulaIndex}].outputSlotId",
                    "outputSlotId",
                    "slot ID planned for the same SIMULATION component");
            if (!formulaOutputs.Add(formula.OutputSlotId))
                return Invalid(
                    "Formula output slots must be unique.",
                    VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                    $"$.formulas[{formulaIndex}].outputSlotId",
                    "outputSlotId",
                    "unique simulation output slot ID");
            var simulationControlIds = response.Controls
                .Where(control => control.ComponentId == formula.ComponentId)
                .Select(control => control.ControlId)
                .ToHashSet(StringComparer.Ordinal);
            var formulaIssue = ValidateFormulaNode(
                formula.Formula,
                $"$.formulas[{formulaIndex}].formula",
                0,
                simulationControlIds);
            if (formulaIssue is not null)
                return formulaIssue;
        }
        var simulationWithoutFormula = components.FirstOrDefault(component =>
            component.Kind is VisualBriefingComponentKind.SIMULATION &&
            response.Formulas.All(formula => formula.ComponentId != component.ComponentId));
        if (simulationWithoutFormula is not null)
            return Invalid(
                "Every simulation requires at least one formula.",
                VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                "$.formulas",
                expected: "at least one formula for every planned SIMULATION component");

        var accessibilityIssue = ValidateComponentTexts(
            response.AccessibilityTexts,
            VisualBriefingComponentTexts.AccessibilityTextKeys(components),
            "accessibilityTexts");
        if (accessibilityIssue is not null)
            return accessibilityIssue;
        var labelIssue = ValidateComponentTexts(
            response.VisibleLabels,
            VisualBriefingComponentTexts.VisibleLabelKeys(components),
            "visibleLabels");
        if (labelIssue is not null)
            return labelIssue;

        return ContainsForbidden(response)
            ? Invalid(
                "Content must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    internal static VisualBriefingContractIssue? ValidateDesign(
        VisualBriefingPlanArtifact plan,
        VisualBriefingDesignResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.DESIGN_CONTRACT)
            return Invalid(
                "The design response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED);
        if (!COLOR.IsMatch(response.Tokens.PrimaryColor) ||
            !COLOR.IsMatch(response.Tokens.AccentColor) ||
            !COLOR.IsMatch(response.Tokens.TextColor) ||
            !COLOR.IsMatch(response.Tokens.BackgroundColor) ||
            response.Tokens.SpacingScale is < 2 or > 12 ||
            response.Tokens.Radius is < 0 or > 32)
            return Invalid(
                "Design tokens are outside the supported values.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        var planned = plan.Sections.SelectMany(section => section.Components)
            .Select(component => component.ComponentId)
            .ToHashSet(StringComparer.Ordinal);
        List<string> references = [];
        List<string> nodeIds = [];
        var issue = ValidateLayoutNode(response.Layout, references, nodeIds);
        if (issue is not null)
            return issue;
        if (nodeIds.Distinct(StringComparer.Ordinal).Count() != nodeIds.Count ||
            nodeIds.Any(planned.Contains))
            return Invalid(
                "Layout node IDs must be unique and must not collide with component IDs.",
                VisualBriefingValidationRule.ID_INVALID);
        if (references.Count != planned.Count ||
            references.Distinct(StringComparer.Ordinal).Count() != references.Count ||
            !references.ToHashSet(StringComparer.Ordinal).SetEquals(planned))
            return Invalid(
                "The layout must reference every planned component exactly once.",
                VisualBriefingValidationRule.LAYOUT_INVALID);

        // The caller compiles the validated layout right afterwards and guards that compilation as a
        // compiler invariant, see VisualBriefingCompilerInvariant. There is no trial compilation here.
        return ContainsForbidden(response)
            ? Invalid(
                "Design must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    private static VisualBriefingContractIssue? ValidateLayoutNode(
        VisualBriefingLayoutNode node,
        List<string> references,
        List<string> nodeIds)
    {
        if (!IsUsableId(node.NodeId) || node.Span is < 1 or > 12 || node.Order is < 0 or > 1000)
            return Invalid(
                "A layout node contains an invalid ID, span, or order.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        nodeIds.Add(node.NodeId);
        if (node.Kind is VisualBriefingLayoutNodeKind.COMPONENT)
        {
            if (string.IsNullOrWhiteSpace(node.ComponentId) || node.Children.Count != 0 || node.Columns is not null)
                return Invalid(
                    "Component layout nodes may only contain a component reference.",
                    VisualBriefingValidationRule.LAYOUT_INVALID);
            references.Add(node.ComponentId);
            return null;
        }
        if (node.ComponentId is not null || node.Children.Count == 0)
            return Invalid(
                "Container layout nodes require children and cannot reference a component.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        if (node.Kind is VisualBriefingLayoutNodeKind.GRID &&
            (node.Columns is null ||
             node.Columns.Mobile is < 1 or > 4 ||
             node.Columns.Tablet is < 1 or > 8 ||
             node.Columns.Desktop is < 1 or > 12))
            return Invalid(
                "Grid nodes require valid responsive column counts.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        if (node.Kind is not VisualBriefingLayoutNodeKind.GRID && node.Columns is not null)
            return Invalid(
                "Responsive columns are only valid for grid nodes.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        foreach (var child in node.Children)
        {
            var issue = ValidateLayoutNode(child, references, nodeIds);
            if (issue is not null)
                return issue;
        }
        return null;
    }

    private static bool ContainsForbidden<T>(T value)
    {
        var json = JsonSerializer.SerializeToElement(value, VisualBriefingJson.Compact);
        return ContainsForbiddenElement(json);
    }

    private static bool ContainsForbiddenElement(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Array)
            return value.EnumerateArray().Any(ContainsForbiddenElement);
        if (value.ValueKind is JsonValueKind.Object)
            return value.EnumerateObject().Any(property =>
                property.Name is "html" or "templateHtml" or "css" or "script" or "echarts" ||
                ContainsForbiddenElement(property.Value));
        if (value.ValueKind is not JsonValueKind.String)
            return false;
        var text = value.GetString() ?? string.Empty;
        return FORBIDDEN_MODEL_TEXT.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
               ScriptAccessRegex().IsMatch(text) ||
               HtmlMarkupRegex().IsMatch(text) ||
               CssSnippetRegex().IsMatch(text);
    }

    private static bool UniqueIds(IEnumerable<string> values)
    {
        var items = values.ToArray();
        return items.Length > 0 &&
               items.All(value => ID.IsMatch(value)) &&
               items.Distinct(StringComparer.Ordinal).Count() == items.Length;
    }

    private static VisualBriefingContractIssue? ValidateFormulaNode(
        VisualBriefingFormulaNode node,
        string path,
        int depth,
        IReadOnlySet<string> controlIds)
    {
        if (depth > 32)
            return Invalid(
                "A formula exceeds the maximum supported depth.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                path,
                expected: "formula depth at most 32");
        if (depth == 0 && node.FormulaVersion != VisualBriefingVersions.FORMULA)
            return Invalid(
                "The formula root uses an unsupported version.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.formulaVersion",
                "formulaVersion",
                "supported formula version");
        if (depth > 0 &&
            node.FormulaVersion is not 0 &&
            node.FormulaVersion != VisualBriefingVersions.FORMULA)
            return Invalid(
                "A nested formula node uses an unsupported version.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.formulaVersion",
                "formulaVersion",
                "zero or supported formula version");
        var hasPath = !string.IsNullOrWhiteSpace(node.Path);
        var hasValue = node.Value is not null;
        var hasOperation = !string.IsNullOrWhiteSpace(node.Operation);
        if (new[] { hasPath, hasValue, hasOperation }.Count(value => value) != 1)
            return Invalid(
                "Every formula node must contain exactly one node kind.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                path,
                expected: "exactly one of path, value, or op");
        if (hasPath)
        {
            if (node.Arguments is not null)
                return Invalid(
                    "A formula path node must not contain arguments.",
                    VisualBriefingValidationRule.FORMULA_AST_INVALID,
                    $"{path}.args",
                    "args",
                    "omitted");
            const string PREFIX = "interactions.state.";
            if (!node.Path!.StartsWith(PREFIX, StringComparison.Ordinal) ||
                !controlIds.Contains(node.Path[PREFIX.Length..]))
                return Invalid(
                    "A formula path must reference a control of the same simulation.",
                    VisualBriefingValidationRule.FORMULA_AST_INVALID,
                    $"{path}.path",
                    "path",
                    "interactions.state.<controlId>");
            return null;
        }
        if (hasValue)
            return node.Arguments is null
                ? null
                : Invalid(
                    "A formula value node must not contain arguments.",
                    VisualBriefingValidationRule.FORMULA_AST_INVALID,
                    $"{path}.args",
                    "args",
                    "omitted");
        HashSet<string> operators = new(StringComparer.Ordinal)
        {
            "add", "subtract", "multiply", "divide", "power", "eq", "ne", "gt", "gte", "lt", "lte",
            "if", "min", "max", "round", "sqrt", "log", "exp",
        };
        if (!operators.Contains(node.Operation!))
            return Invalid(
                "A formula uses an unsupported operation.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.op",
                "op",
                "supported formula operation");
        if (node.Arguments is null)
            return Invalid(
                "A formula operation requires arguments.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.args",
                "args",
                "argument array with valid arity");
        var count = node.Arguments.Count;
        var validArity = node.Operation switch
        {
            "sqrt" or "log" or "exp" => count == 1,
            "subtract" or "divide" or "power" or "eq" or "ne" or "gt" or "gte" or "lt" or "lte" => count == 2,
            "if" => count == 3,
            "round" => count is 1 or 2,
            _ => count > 0,
        };
        if (!validArity)
            return Invalid(
                "A formula operation has an invalid number of arguments.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.args",
                "args",
                "argument array with valid arity");
        for (var argumentIndex = 0; argumentIndex < node.Arguments.Count; argumentIndex++)
        {
            var issue = ValidateFormulaNode(
                node.Arguments[argumentIndex],
                $"{path}.args[{argumentIndex}]",
                depth + 1,
                controlIds);
            if (issue is not null)
                return issue;
        }
        return null;
    }

    /// <summary>
    /// Checks whether every row of a validated table slot starts with a text cell.
    /// </summary>
    /// <param name="tableData">The validated table slot value.</param>
    /// <returns>True when every first cell is a string.</returns>
    private static bool HasTextFirstColumn(JsonElement tableData) =>
        tableData.ValueKind is JsonValueKind.Object &&
        tableData.TryGetProperty("rows", out var rows) &&
        rows.ValueKind is JsonValueKind.Array &&
        rows.EnumerateArray().All(row =>
            row.TryGetProperty("cells", out var cells) &&
            cells.ValueKind is JsonValueKind.Array &&
            cells.GetArrayLength() > 0 &&
            cells[0].ValueKind is JsonValueKind.String);

    /// <summary>
    /// Checks one component text map against the component IDs that actually consume it. Asking for
    /// texts that are never rendered is as much a defect as missing the ones that are.
    /// </summary>
    /// <param name="texts">The model-supplied map.</param>
    /// <param name="requiredKeys">The component IDs that consume this kind of text.</param>
    /// <param name="field">The contract field name used in diagnostics.</param>
    /// <returns>The contract issue, or null when the map is complete and exact.</returns>
    private static VisualBriefingContractIssue? ValidateComponentTexts(
        IReadOnlyDictionary<string, string> texts,
        IReadOnlyList<string> requiredKeys,
        string field)
    {
        var required = requiredKeys.ToHashSet(StringComparer.Ordinal);
        var unknownKey = texts.Keys.FirstOrDefault(key => !required.Contains(key));
        if (unknownKey is not null)
            return Invalid(
                $"The {field} contain an entry for a component that does not use one.",
                VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID,
                $"$.{field}.*",
                field,
                "only component IDs that require this text");
        foreach (var key in requiredKeys)
        {
            if (!texts.TryGetValue(key, out var text))
                return Invalid(
                    $"A required entry is missing from {field}.",
                    VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID,
                    $"$.{field}",
                    field,
                    "one entry for every component ID that requires this text");
            if (string.IsNullOrWhiteSpace(text))
                return Invalid(
                    $"An entry in {field} must not be empty.",
                    VisualBriefingValidationRule.ACCESSIBILITY_TEXT_INVALID,
                    $"$.{field}.{key}",
                    field,
                    "non-empty target-language string");
        }
        return texts.Count == required.Count
            ? null
            : Invalid(
                $"The {field} must contain exactly one entry per requiring component.",
                VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID,
                $"$.{field}",
                field,
                "exactly one entry for every component ID that requires this text");
    }

    private static VisualBriefingContractIssue? ValidateControlState(
        VisualBriefingControlSpec control,
        int controlIndex)
    {
        var optionValues = control.Options.Select(option => option.Value).ToArray();
        HashSet<string> seenOptions = new(StringComparer.Ordinal);
        for (var optionIndex = 0; optionIndex < control.Options.Count; optionIndex++)
        {
            var option = control.Options[optionIndex];

            // Option values are pure data: they are compared against the control state and never
            // become element IDs, so they may carry the same text as the data they select:
            if (string.IsNullOrWhiteSpace(option.Value) ||
                option.Value.Length > MAX_OPTION_VALUE_LENGTH ||
                !seenOptions.Add(option.Value))
                return Invalid(
                    "Control option values must be non-empty, short, and unique.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].options[{optionIndex}].value",
                    "value",
                    "unique non-empty string");
            if (string.IsNullOrWhiteSpace(option.Label))
                return Invalid(
                    "Control option labels must not be empty.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].options[{optionIndex}].label",
                    "label",
                    "non-empty target-language string");
        }
        if (control.Kind is VisualBriefingControlKind.TAB or
            VisualBriefingControlKind.FILTER or
            VisualBriefingControlKind.SELECT)
        {
            if (optionValues.Length == 0)
                return Invalid(
                    "This control kind requires options.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].options",
                    "options",
                    "non-empty option array");
            if (control.InitialValue.ValueKind is not JsonValueKind.String ||
                !optionValues.Contains(control.InitialValue.GetString(), StringComparer.Ordinal))
                return Invalid(
                    "The initial control value must select one declared option.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].initialValue",
                    "initialValue",
                    "string equal to one option value");
            return null;
        }
        if (optionValues.Length != 0)
            return Invalid(
                "Numeric controls must not declare options.",
                VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                $"$.controls[{controlIndex}].options",
                "options",
                "empty array");
        return control.InitialValue.ValueKind is JsonValueKind.Number
            ? null
            : Invalid(
                "Numeric controls require a numeric initial value.",
                VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                $"$.controls[{controlIndex}].initialValue",
                "initialValue",
                "JSON number");
    }

    private static bool ControlMatchesComponent(
        VisualBriefingControlKind control,
        VisualBriefingComponentKind component) => component switch
        {
            VisualBriefingComponentKind.TABS =>
                control is VisualBriefingControlKind.TAB,
            VisualBriefingComponentKind.SIMULATION =>
                control is VisualBriefingControlKind.NUMBER or VisualBriefingControlKind.RANGE or
                    VisualBriefingControlKind.SELECT,

            // FILTER controls are generated from the table data, never supplied by the model:
            _ => false,
        };

    private static string ExpectedControlKinds(VisualBriefingComponentKind component) => component switch
    {
        VisualBriefingComponentKind.TABS => "TAB",
        VisualBriefingComponentKind.SIMULATION => "NUMBER, RANGE, or SELECT",
        _ => "no controls",
    };

    private static string? FindInvalidOrDuplicateId(
        IEnumerable<(string Id, string Path)> candidates)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!IsUsableId(candidate.Id) || !seen.Add(candidate.Id))
                return candidate.Path;
        }
        return null;
    }

    /// <summary>
    /// Checks whether an ID is well-formed and free of the reserved AI Studio prefix. Compiled
    /// element IDs are derived from these IDs, and the artifact contract reserves the mwai- prefix.
    /// </summary>
    /// <param name="id">The model-supplied ID.</param>
    /// <returns>True when the ID can be used.</returns>
    private static bool IsUsableId(string id) =>
        ID.IsMatch(id) && !id.StartsWith("mwai-", StringComparison.OrdinalIgnoreCase);

    private static int FindDuplicateIndex(IReadOnlyList<string> values)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            if (!seen.Add(values[index]))
                return index;
        }
        return -1;
    }

    private static VisualBriefingContractIssue Invalid(
        string issue,
        VisualBriefingValidationRule rule = VisualBriefingValidationRule.NONE,
        string jsonPath = "$",
        string fieldName = "",
        string expected = "") =>
        new(
            VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
            issue,
            rule,
            new()
            {
                IssueKind = VisualBriefingStructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID,
                JsonPath = jsonPath,
                FieldName = fieldName,

                // Expected carries a contract shape, never a rule name. The rule is reported
                // separately, so an unknown shape stays empty:
                Expected = expected,
            });

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorRegex();

    // Matches scripted member access such as document.getElementById( but not a sentence that
    // happens to end with the word "document":
    [GeneratedRegex(@"\b(?:document|window|globalThis)\.[A-Za-z_$][A-Za-z0-9_$]*\s*[({=\[.]", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptAccessRegex();

    // Matches real HTML tags only. A generic "<...>" pattern would reject ordinary prose such as
    // comparisons or placeholders in angle brackets:
    [GeneratedRegex(
        @"<\s*/?\s*(?:script|style|iframe|object|embed|link|meta|form|input|button|select|option|template|svg|img|video|audio|canvas|table|thead|tbody|tfoot|tr|td|th|caption|div|span|p|a|ul|ol|li|dl|dt|dd|h[1-6]|section|article|aside|header|footer|main|nav|figure|figcaption|details|summary|small|strong|em|b|i|u|br|hr|label|progress)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlMarkupRegex();

    [GeneratedRegex(@"(?:^|\s)[.#]?[A-Za-z][A-Za-z0-9 _-]*\s*\{[^{}]*:[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex CssSnippetRegex();
}

internal sealed class VisualBriefingChartCompiler
{
    internal JsonElement Compile(VisualBriefingChartSpec chart)
    {
        object series = chart.Kind switch
        {
            VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT =>
                chart.Categories.Select((category, index) => new
                {
                    name = category,
                    value = chart.Series[0].Values[index],
                }).ToArray(),
            VisualBriefingChartKind.RADAR => chart.Series.Select(item => new
            {
                name = item.Name,
                type = "radar",
                data = new[]
                {
                    new
                    {
                        value = item.Values,
                        name = item.Name,
                    },
                },
            }).ToArray(),
            _ => chart.Series.Select(item => new
            {
                name = item.Name,
                type = SeriesType(chart.Kind),
                stack = chart.Kind is VisualBriefingChartKind.STACKED_BAR ? "total" : null,
                areaStyle = chart.Kind is VisualBriefingChartKind.AREA ? new { } : null,
                data = item.Values,
            }).ToArray(),
        };
        var option = new
        {
            title = new { text = chart.Title },
            tooltip = new { trigger = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT ? "item" : "axis" },
            legend = new { show = true },
            xAxis = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new { type = "category", data = chart.Categories },
            yAxis = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new { type = "value" },
            radar = chart.Kind is VisualBriefingChartKind.RADAR
                ? new { indicator = chart.Categories.Select(name => new { name }).ToArray() }
                : null,
            series = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT
                ? new[] { new { type = "pie", radius = chart.Kind is VisualBriefingChartKind.DONUT ? new[] { "45%", "70%" } : new[] { "0%", "70%" }, data = series } }
                : series,
        };
        return JsonSerializer.SerializeToElement(option, VisualBriefingJson.Compact);
    }

    private static string SeriesType(VisualBriefingChartKind kind) => kind switch
    {
        VisualBriefingChartKind.LINE or VisualBriefingChartKind.AREA => "line",
        VisualBriefingChartKind.BAR or VisualBriefingChartKind.STACKED_BAR => "bar",
        VisualBriefingChartKind.SCATTER => "scatter",
        VisualBriefingChartKind.RADAR => "radar",
        _ => "line",
    };
}

internal sealed class VisualBriefingInteractionCompiler
{
    internal JsonElement Compile(
        IReadOnlyList<VisualBriefingControlSpec> controls,
        IReadOnlyList<VisualBriefingFormulaSpec> formulas)
    {
        var state = controls.ToDictionary(
            control => control.ControlId,
            control => control.InitialValue.Clone(),
            StringComparer.Ordinal);
        var formulaMap = formulas.ToDictionary(
            formula => formula.OutputSlotId,
            formula => formula.Formula,
            StringComparer.Ordinal);
        return JsonSerializer.SerializeToElement(new
        {
            controls,
            state,
            formulas = formulaMap,
        }, VisualBriefingJson.Compact);
    }

    internal string CompileMarkup(string componentId, IReadOnlyList<VisualBriefingControlSpec> controls)
    {
        var builder = new StringBuilder();
        foreach (var indexed in controls.Select((control, index) => (Control: control, Index: index))
                     .Where(item => item.Control.ComponentId == componentId))
        {
            var control = indexed.Control;

            // Controls carry no element ID: nothing references it, and a model-chosen control ID
            // could otherwise collide with a layout node ID in the compiled template:
            var id = HtmlEncoder.Default.Encode(control.ControlId);
            var accessibilityPath = $"accessibility.{HtmlEncoder.Default.Encode(componentId)}";
            builder.Append(control.Kind switch
            {
                VisualBriefingControlKind.SELECT or VisualBriefingControlKind.FILTER =>
                    $"<select data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\"><template data-mwai-each=\"interactions.controls.{indexed.Index}.options\"><option data-mwai-attr-value=\".value\" data-mwai-text=\".label\"></option></template></select>",
                VisualBriefingControlKind.RANGE =>
                    $"<input type=\"range\" data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\">",
                VisualBriefingControlKind.NUMBER =>
                    $"<input type=\"number\" data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\">",
                _ => string.Empty,
            });
        }
        return builder.ToString();
    }

    internal static string CompileResetMarkup(string componentId) =>
        $"<button type=\"button\" data-mwai-reset=\"{HtmlEncoder.Default.Encode(componentId)}\" data-mwai-text=\"labels.reset\"></button>";
}

internal sealed class VisualBriefingLayoutCompiler(
    VisualBriefingChartCompiler chartCompiler,
    VisualBriefingInteractionCompiler interactionCompiler)
{
    internal VisualBriefingCompilationResult Compile(
        VisualBriefingPlanArtifact plan,
        VisualBriefingContentArtifact content,
        VisualBriefingLayoutNode layout,
        VisualBriefingDesignTokens tokens)
    {
        var slots = content.Slots.ToDictionary(item => item.SlotId, item => item.Value.Clone(), StringComparer.Ordinal);
        var components = plan.Sections.SelectMany(section => section.Components)
            .ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        var charts = content.Charts.ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        var missingSlot = components.Values
            .SelectMany(component => component.RequiredSlots)
            .FirstOrDefault(slotId => !slots.ContainsKey(slotId));
        if (missingSlot is not null)
            throw new InvalidDataException("A planned content slot is missing during compilation.");
        var missingChart = components.Values
            .Where(component => component.Kind is VisualBriefingComponentKind.CHART)
            .Select(component => component.ComponentId)
            .FirstOrDefault(componentId => !charts.ContainsKey(componentId));
        if (missingChart is not null)
            throw new InvalidDataException("A planned chart is missing during compilation.");
        var chartOptions = content.Charts.ToDictionary(
            item => item.ComponentId,
            item => chartCompiler.Compile(item),
            StringComparer.Ordinal);
        var interactions = interactionCompiler.Compile(content.Controls, content.Formulas);
        var data = JsonSerializer.SerializeToElement(new
        {
            slots,
            charts = chartOptions,
            interactions,
            accessibility = content.AccessibilityTexts,
            visibleLabels = content.VisibleLabels,
            sourceReferences = content.SourceReferences,
            labels = new { reset = content.ResetLabel },
        }, VisualBriefingJson.Compact);
        var html = this.CompileNode(layout, components, content);
        var css = CompileCss(tokens, layout);
        return new(
            data,
            html,
            css,
            VisualBriefingHashing.Compute(html),
            VisualBriefingHashing.Compute(css));
    }

    private string CompileNode(
        VisualBriefingLayoutNode node,
        IReadOnlyDictionary<string, VisualBriefingPlanComponent> components,
        VisualBriefingContentArtifact content)
    {
        var id = HtmlEncoder.Default.Encode(node.NodeId);
        if (node.Kind is VisualBriefingLayoutNodeKind.COMPONENT)
        {
            if (node.ComponentId is null || !components.TryGetValue(node.ComponentId, out var component))
                throw new InvalidDataException("The layout references an unknown component.");
            var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
            var body = this.CompileComponent(component, content);
            var componentClasses = CompileLayoutClasses(
                node,
                $"mwai-component mwai-{component.Kind.ToString().ToLowerInvariant()}");
            return $"<article id=\"{id}\" class=\"{componentClasses}\" data-mwai-region=\"{componentId}\">{body}</article>";
        }
        var tag = node.Kind is VisualBriefingLayoutNodeKind.SECTION ? "section" : "div";
        var kind = node.Kind.ToString().ToLowerInvariant();
        var layoutClasses = CompileLayoutClasses(node, $"mwai-layout mwai-{kind}");
        var children = string.Concat(node.Children.OrderBy(child => child.Order)
            .Select(child => this.CompileNode(child, components, content)));
        return $"<{tag} id=\"{id}\" class=\"{layoutClasses}\">{children}</{tag}>";
    }

    private static string CompileLayoutClasses(VisualBriefingLayoutNode node, string prefix) =>
        $"{prefix} mwai-span-{node.Span} mwai-align-{node.Alignment.ToString().ToLowerInvariant()}" +
        (node.Emphasized ? " mwai-emphasized" : string.Empty);

    private string CompileComponent(
        VisualBriefingPlanComponent component,
        VisualBriefingContentArtifact content)
    {
        var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
        var slotMarkup = string.Concat(component.RequiredSlots.Select(slotId =>
        {
            var encoded = HtmlEncoder.Default.Encode(slotId);
            return $"<span data-mwai-text=\"slots.{encoded}\"></span>";
        }));
        var controls = interactionCompiler.CompileMarkup(component.ComponentId, content.Controls);
        var formulas = string.Concat(content.Formulas
            .Where(formula => formula.ComponentId == component.ComponentId)
            .Select(formula =>
                $"<span data-mwai-expr=\"interactions.formulas.{HtmlEncoder.Default.Encode(formula.OutputSlotId)}\"></span>"));
        var filterControl = content.Controls.FirstOrDefault(control =>
            control.ComponentId == component.ComponentId &&
            control.Kind is VisualBriefingControlKind.FILTER);

        // Rows are filtered by their first cell, so the filter options of a filterable table
        // correspond to the values of the table's first column:
        var filterAttributes = filterControl is null
            ? string.Empty
            : $" data-mwai-filter=\"$root.interactions.state.{HtmlEncoder.Default.Encode(filterControl.ControlId)}\" data-mwai-filter-value=\".cells.0\"";
        var body = component.Kind switch
        {
            VisualBriefingComponentKind.CHART =>
                $"<figure><div role=\"img\" data-mwai-attr-aria-label=\"accessibility.{HtmlEncoder.Default.Encode(component.ComponentId)}\" aria-describedby=\"{componentId}-chart-alt\" data-mwai-chart=\"charts.{HtmlEncoder.Default.Encode(component.ComponentId)}\"></div><figcaption id=\"{componentId}-chart-alt\">{slotMarkup}</figcaption></figure>",
            VisualBriefingComponentKind.ASSET =>
                $"<figure><img data-mwai-asset=\"{HtmlEncoder.Default.Encode(component.AssetId ?? throw new InvalidDataException("An asset component is missing its asset ID."))}\" data-mwai-attr-alt=\"accessibility.{componentId}\"><figcaption>{slotMarkup}</figcaption></figure>",
            VisualBriefingComponentKind.TABLE or VisualBriefingComponentKind.FILTERABLE_TABLE =>
                CompileTable(component, componentId, controls, filterAttributes),
            VisualBriefingComponentKind.TABS =>
                this.CompileTabs(component, content.Controls),
            VisualBriefingComponentKind.ACCORDION =>
                $"<details><summary><span data-mwai-text=\"visibleLabels.{componentId}\"></span></summary><div>{slotMarkup}</div></details>",
            VisualBriefingComponentKind.SIMULATION =>
                $"<div class=\"mwai-simulation\">{controls}{slotMarkup}{formulas}{VisualBriefingInteractionCompiler.CompileResetMarkup(component.ComponentId)}</div>",
            _ => $"{slotMarkup}{controls}",
        };
        var references = content.SourceReferences.ContainsKey(component.ComponentId)
            ? $"<small><template data-mwai-each=\"sourceReferences.{componentId}\"><span data-mwai-text=\".\"></span> </template></small>"
            : string.Empty;
        return $"{body}{references}";
    }

    /// <summary>
    /// Compiles a table component from its tabular data slot. The first required slot carries the
    /// columns and rows, see VisualBriefingSlotTypes; any further slot is rendered as leading text.
    /// </summary>
    /// <param name="component">The planned table component.</param>
    /// <param name="componentId">The encoded component ID.</param>
    /// <param name="controls">The compiled control markup of the component.</param>
    /// <param name="filterAttributes">The compiled row filter attributes, if any.</param>
    /// <returns>The compiled table markup.</returns>
    private static string CompileTable(
        VisualBriefingPlanComponent component,
        string componentId,
        string controls,
        string filterAttributes)
    {
        var dataSlot = HtmlEncoder.Default.Encode(component.RequiredSlots[0]);
        var leadingText = string.Concat(component.RequiredSlots.Skip(1).Select(slotId =>
            $"<span data-mwai-text=\"slots.{HtmlEncoder.Default.Encode(slotId)}\"></span>"));
        return $"{leadingText}<div class=\"mwai-table-wrap\">{controls}<table>" +
               $"<caption data-mwai-text=\"visibleLabels.{componentId}\"></caption>" +
               $"<thead><tr><template data-mwai-each=\"slots.{dataSlot}.columns\"><th scope=\"col\" data-mwai-text=\".\"></th></template></tr></thead>" +
               $"<tbody><template data-mwai-each=\"slots.{dataSlot}.rows\"><tr{filterAttributes}><template data-mwai-each=\".cells\"><td data-mwai-text=\".\"></td></template></tr></template></tbody>" +
               "</table></div>";
    }

    private string CompileTabs(
        VisualBriefingPlanComponent component,
        IReadOnlyList<VisualBriefingControlSpec> controls)
    {
        var indexedControl = controls.Select((control, index) => (Control: control, Index: index))
            .First(item =>
                item.Control.ComponentId == component.ComponentId &&
                item.Control.Kind is VisualBriefingControlKind.TAB);
        var initial = indexedControl.Control.InitialValue.GetString();
        var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
        var buttons = new StringBuilder();
        var panels = new StringBuilder();
        for (var index = 0; index < indexedControl.Control.Options.Count; index++)
        {
            var option = indexedControl.Control.Options[index];

            // The panel ID must remain a safe identifier, so it is derived from the option position
            // instead of the model-supplied option value:
            var panelId = $"{componentId}-tab-{index}";
            var selected = string.Equals(option.Value, initial, StringComparison.Ordinal);
            buttons.Append(
                $"<button type=\"button\" role=\"tab\" aria-controls=\"{panelId}\" aria-selected=\"{selected.ToString().ToLowerInvariant()}\" data-mwai-tab-target=\"{panelId}\" data-mwai-text=\"interactions.controls.{indexedControl.Index}.options.{index}.label\"></button>");
            var slotId = component.RequiredSlots[Math.Min(index, component.RequiredSlots.Count - 1)];
            panels.Append(
                $"<section id=\"{panelId}\" role=\"tabpanel\" data-mwai-tab-panel=\"{panelId}\"{(selected ? string.Empty : " hidden")}><span data-mwai-text=\"slots.{HtmlEncoder.Default.Encode(slotId)}\"></span></section>");
        }
        return $"<div data-mwai-tabs=\"{componentId}\"><div role=\"tablist\">{buttons}</div>{panels}</div>";
    }

    private static string CompileCss(
        VisualBriefingDesignTokens tokens,
        VisualBriefingLayoutNode layout)
    {
        var density = tokens.Density switch
        {
            VisualBriefingDensity.COMPACT => 0.75m,
            VisualBriefingDensity.SPACIOUS => 1.25m,
            _ => 1m,
        };
        var shadow = tokens.Surface is VisualBriefingSurface.RAISED
            ? "0 12px 32px rgba(23,32,51,.12)"
            : "none";
        var surface = tokens.Surface switch
        {
            VisualBriefingSurface.SUBTLE => "background:color-mix(in srgb,var(--mwai-bg),var(--mwai-primary) 4%);",
            VisualBriefingSurface.ACCENT => "border:1px solid var(--mwai-accent);",
            _ => string.Empty,
        };
        var typeScale = tokens.TypographyScale switch
        {
            VisualBriefingTypographyScale.COMPACT => 0.9m,
            VisualBriefingTypographyScale.EDITORIAL => 1.1m,
            VisualBriefingTypographyScale.DISPLAY => 1.2m,
            _ => 1m,
        };
        var css = new StringBuilder($$"""
                                    .mwai-layout{--mwai-primary:{{tokens.PrimaryColor}};--mwai-accent:{{tokens.AccentColor}};--mwai-text:{{tokens.TextColor}};--mwai-bg:{{tokens.BackgroundColor}};--mwai-space:{{tokens.SpacingScale}}px;--mwai-radius:{{tokens.Radius}}px;--mwai-density:{{density.ToString(System.Globalization.CultureInfo.InvariantCulture)}};--mwai-type-scale:{{typeScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}};box-sizing:border-box;color:var(--mwai-text);background:var(--mwai-bg);font-size:calc(1rem*var(--mwai-type-scale));gap:calc(var(--mwai-space)*var(--mwai-density)*4);}
                                    .mwai-section,.mwai-stack{display:flex;flex-direction:column;}
                                    .mwai-grid{display:grid;}
                                    .mwai-component{display:flex;flex-direction:column;min-width:0;padding:calc(var(--mwai-space)*var(--mwai-density)*4);border-radius:var(--mwai-radius);box-shadow:{{shadow}};{{surface}}}
                                    .mwai-emphasized{border-inline-start:4px solid var(--mwai-accent);}
                                    .mwai-align-start{align-items:start;}.mwai-align-center{align-items:center;}.mwai-align-end{align-items:end;}.mwai-align-stretch{align-items:stretch;}
                                    .mwai-table-wrap{overflow:auto;}table{border-collapse:collapse;width:100%;}img,canvas{display:block;max-width:100%;height:auto;}
                                    """);
        foreach (var grid in EnumerateGridNodes(layout))
        {
            var id = grid.NodeId;
            css.Append($"#{id}{{grid-template-columns:repeat({grid.Columns!.Mobile},minmax(0,1fr));}}");
            foreach (var child in grid.Children)
                css.Append($"#{child.NodeId}{{grid-column:span {Math.Min(child.Span, grid.Columns.Mobile)};}}");
            css.Append($"@media(min-width:48rem){{#{id}{{grid-template-columns:repeat({grid.Columns.Tablet},minmax(0,1fr));}}");
            foreach (var child in grid.Children)
                css.Append($"#{child.NodeId}{{grid-column:span {Math.Min(child.Span, grid.Columns.Tablet)};}}");
            css.Append('}');
            css.Append($"@media(min-width:75rem){{#{id}{{grid-template-columns:repeat({grid.Columns.Desktop},minmax(0,1fr));}}");
            foreach (var child in grid.Children)
                css.Append($"#{child.NodeId}{{grid-column:span {Math.Min(child.Span, grid.Columns.Desktop)};}}");
            css.Append('}');
        }
        return css.ToString();
    }

    private static IEnumerable<VisualBriefingLayoutNode> EnumerateGridNodes(VisualBriefingLayoutNode node)
    {
        if (node.Kind is VisualBriefingLayoutNodeKind.GRID)
            yield return node;
        foreach (var child in node.Children)
        foreach (var grid in EnumerateGridNodes(child))
            yield return grid;
    }
}
