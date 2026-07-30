using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stable, content-free validation rules suitable for diagnostics.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingValidationRule>))]
public enum VisualBriefingValidationRule
{
    NONE,
    JSON_INVALID,
    VALUE_TYPE_INVALID,
    UNKNOWN_FIELD,
    CONTRACT_VERSION_UNSUPPORTED,
    ID_INVALID,
    REFERENCE_INVALID,
    SOURCE_COVERAGE_INVALID,
    ASSET_PLAN_INVALID,
    SLOT_FULFILLMENT_INVALID,
    SLOT_VALUE_TYPE_INVALID,
    CHART_SET_INVALID,
    CHART_DATA_INVALID,
    CONTROL_ID_INVALID,
    CONTROL_TARGET_INVALID,
    CONTROL_STATE_INVALID,
    CONTROL_REQUIREMENT_INVALID,
    FORMULA_TARGET_INVALID,
    FORMULA_AST_INVALID,
    ACCESSIBILITY_SET_INVALID,
    ACCESSIBILITY_TEXT_INVALID,
    LAYOUT_INVALID,
    TEMPLATE_ATTRIBUTE_PROHIBITED,
    MODEL_MARKUP_PROHIBITED,
    COMPILER_OUTPUT_INVALID,
}