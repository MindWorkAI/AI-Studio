using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stable, content-free validation rules suitable for diagnostics.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingValidationRule>))]
public enum VisualBriefingValidationRule
{
    /// <summary>No validation rule was violated.</summary>
    NONE,
    
    /// <summary>The response was not valid JSON.</summary>
    JSON_INVALID,
    
    /// <summary>A value did not match its required JSON type.</summary>
    VALUE_TYPE_INVALID,
    
    /// <summary>The response contained an unknown field.</summary>
    UNKNOWN_FIELD,
    
    /// <summary>The response used an unsupported contract version.</summary>
    CONTRACT_VERSION_UNSUPPORTED,
    
    /// <summary>An identifier was empty, malformed, or duplicated.</summary>
    ID_INVALID,
    
    /// <summary>A reference did not resolve to its required target.</summary>
    REFERENCE_INVALID,
    
    /// <summary>Source coverage was incomplete or duplicated.</summary>
    SOURCE_COVERAGE_INVALID,
    
    /// <summary>The visual asset plan was incomplete or invalid.</summary>
    ASSET_PLAN_INVALID,
    
    /// <summary>Planned content slots were missing, duplicated, or unexpected.</summary>
    SLOT_FULFILLMENT_INVALID,
    
    /// <summary>A slot value did not match its planned semantic type.</summary>
    SLOT_VALUE_TYPE_INVALID,
    
    /// <summary>The set of charts did not match the planned components.</summary>
    CHART_SET_INVALID,
    
    /// <summary>A chart contained invalid categories or series values.</summary>
    CHART_DATA_INVALID,
    
    /// <summary>An interaction control identifier was invalid.</summary>
    CONTROL_ID_INVALID,
    
    /// <summary>An interaction control targeted an invalid component.</summary>
    CONTROL_TARGET_INVALID,
    
    /// <summary>An interaction control used an invalid initial state.</summary>
    CONTROL_STATE_INVALID,
    
    /// <summary>A component did not satisfy its required controls.</summary>
    CONTROL_REQUIREMENT_INVALID,
    
    /// <summary>A formula targeted an invalid component or output slot.</summary>
    FORMULA_TARGET_INVALID,
    
    /// <summary>A formula tree contained an invalid operation or argument shape.</summary>
    FORMULA_AST_INVALID,
    
    /// <summary>The set of accessibility texts did not match component requirements.</summary>
    ACCESSIBILITY_SET_INVALID,
    
    /// <summary>An accessibility text was empty or invalid.</summary>
    ACCESSIBILITY_TEXT_INVALID,
    
    /// <summary>The bounded presentation layout was invalid.</summary>
    LAYOUT_INVALID,
    
    /// <summary>A compiled template used a prohibited attribute.</summary>
    TEMPLATE_ATTRIBUTE_PROHIBITED,
    
    /// <summary>A model response attempted to provide markup.</summary>
    MODEL_MARKUP_PROHIBITED,
    
    /// <summary>AI Studio's deterministic compiler produced invalid output.</summary>
    COMPILER_OUTPUT_INVALID,
}