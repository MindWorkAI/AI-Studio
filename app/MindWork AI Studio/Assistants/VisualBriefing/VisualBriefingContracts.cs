using System.Text.Json;
using System.Text.Json.Serialization;

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

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSectionRole>))]
public enum VisualBriefingSectionRole
{
    HERO,
    EXECUTIVE_SUMMARY,
    NARRATIVE,
    EVIDENCE,
    EXPLORATION,
    CONCLUSION,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSlotRole>))]
public enum VisualBriefingSlotRole
{
    EYEBROW,
    TITLE,
    SUMMARY,
    BODY,
    LABEL,
    VALUE,
    CONTEXT,
    CAPTION,
    TABLE_DATA,
    PANEL,
    RESULT,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingAlignment>))]
public enum VisualBriefingAlignment
{
    START,
    CENTER,
    END,
    STRETCH,
}

[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingDesignProfile>))]
public enum VisualBriefingDesignProfile
{
    EDITORIAL,
    EXECUTIVE,
    ANALYTICAL,
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
public sealed class VisualBriefingPlanSlot
{
    [JsonRequired]
    public string SlotId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingSlotRole Role { get; set; }
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
    public List<VisualBriefingPlanSlot> Slots { get; set; } = [];
    [JsonRequired]
    public string? AssetId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanSection
{
    [JsonRequired]
    public string SectionId { get; set; } = string.Empty;
    [JsonRequired]
    public VisualBriefingSectionRole Role { get; set; }
    [JsonRequired]
    public string TitleSlotId { get; set; } = string.Empty;
    [JsonRequired]
    public string SummarySlotId { get; set; } = string.Empty;
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
    public string? SectionId { get; set; }
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
public sealed class VisualBriefingDesignResponse
{
    [JsonRequired]
    public int ContractVersion { get; set; }
    
    [JsonRequired]
    public VisualBriefingDesignProfile Profile { get; set; }
    
    [JsonRequired]
    public VisualBriefingLayoutNode Layout { get; set; } = new();
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
/// Derives which assistive component texts the model has to supply. Visible component copy is
/// carried by semantic content slots instead.
/// </summary>
internal static class VisualBriefingComponentTexts
{
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
    /// <param name="slot">The planned semantic slot.</param>
    /// <returns>The required slot type.</returns>
    internal static VisualBriefingSlotType Expected(VisualBriefingPlanSlot slot) =>
        slot.Role is VisualBriefingSlotRole.TABLE_DATA ? VisualBriefingSlotType.TABLE : VisualBriefingSlotType.TEXT;

    /// <summary>
    /// Determines whether a slot carries the tabular data of a table component.
    /// </summary>
    /// <param name="component">The planned component owning the slot.</param>
    /// <param name="slotId">The planned slot ID.</param>
    /// <returns>True when the slot carries tabular data.</returns>
    internal static bool IsTableDataSlot(VisualBriefingPlanComponent component, string slotId) =>
        component.Slots.Any(slot =>
            slot.Role is VisualBriefingSlotRole.TABLE_DATA &&
            string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));

    /// <summary>
    /// Maps every planned slot to its required slot type.
    /// </summary>
    /// <param name="sections">The planned sections.</param>
    /// <returns>The slot types by slot ID.</returns>
    internal static Dictionary<string, VisualBriefingSlotType> Map(IReadOnlyList<VisualBriefingPlanSection> sections)
    {
        Dictionary<string, VisualBriefingSlotType> types = new(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            types[section.TitleSlotId] = VisualBriefingSlotType.TEXT;
            types[section.SummarySlotId] = VisualBriefingSlotType.TEXT;
        }
        foreach (var slot in sections.SelectMany(section => section.Components).SelectMany(component => component.Slots))
            types[slot.SlotId] = Expected(slot);

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
