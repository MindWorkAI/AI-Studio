namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Translates the stable failure enums of one visual briefing operation into user-facing text.
/// </summary>
/// <remarks>
/// The issue texts that travel with a failure are contract language: they are sent back to the model
/// as repair instructions, and they are persisted into the build record on disk. Both uses require
/// stable English, so they can never be localized at their origin. The UI therefore keeps only the
/// stable enums and asks for its text here, at render time, in the language selected right now.
/// </remarks>
internal static class VisualBriefingFailureExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(VisualBriefingFailureExtensions).Namespace, nameof(VisualBriefingFailureExtensions));

    /// <summary>
    /// Gets the localized message for one recorded failure.
    /// </summary>
    /// <param name="failure">The recorded failure.</param>
    /// <returns>The localized message.</returns>
    internal static string ToUserMessage(this VisualBriefingFailure failure) => ToUserMessage(failure.Code, failure.ValidationRule);

    /// <summary>
    /// Gets the localized message for one failure code and validation rule.
    /// </summary>
    /// <remarks>
    /// The failure code decides because it is the only value that is always about the failure at hand.
    /// A validation rule is not: a failure records the rule of whichever stage recorded one, so a failed
    /// commit or an incompatible content signature can carry the rule of an earlier stage. The two codes
    /// below are the exception. They say no more than "the response was rejected", so there the rule
    /// names the concrete violation and gives the better text.
    /// </remarks>
    /// <param name="code">The stable failure code.</param>
    /// <param name="rule">The stable validation rule.</param>
    /// <returns>The localized message.</returns>
    internal static string ToUserMessage(VisualBriefingFailureCode code, VisualBriefingValidationRule rule) => code switch
    {
        VisualBriefingFailureCode.RESPONSE_JSON_INVALID or VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID when rule is not VisualBriefingValidationRule.NONE => rule.ToUserMessage(),

        _ => code.ToUserMessage(),
    };

    /// <summary>
    /// Gets the localized message for one validation rule.
    /// </summary>
    /// <param name="rule">The stable validation rule.</param>
    /// <returns>The localized message.</returns>
    private static string ToUserMessage(this VisualBriefingValidationRule rule) => rule switch
    {
        VisualBriefingValidationRule.JSON_INVALID => TB("The model did not return valid JSON. Please try again or select another model."),
        VisualBriefingValidationRule.VALUE_TYPE_INVALID => TB("The model response contained a value of the wrong type. Please try again or select another model."),
        VisualBriefingValidationRule.UNKNOWN_FIELD => TB("The model response contained unexpected fields. Please try again or select another model."),
        VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED => TB("The model response used an unsupported contract version. Please try again or select another model."),
        VisualBriefingValidationRule.ID_INVALID => TB("The model response contained an empty, malformed, or duplicated identifier. Please try again or select another model."),
        VisualBriefingValidationRule.REFERENCE_INVALID => TB("The model response referenced content that does not exist. Please try again or select another model."),
        VisualBriefingValidationRule.SOURCE_COVERAGE_INVALID => TB("The model did not cover every source of this briefing exactly once. Please try again or select another model."),
        VisualBriefingValidationRule.ASSET_PLAN_INVALID => TB("The model did not plan every visual asset of this briefing exactly once. Please try again or select another model."),
        VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID => TB("The model did not fill every planned content slot exactly once. Please try again or select another model."),
        VisualBriefingValidationRule.SLOT_VALUE_TYPE_INVALID => TB("The model filled a content slot with the wrong kind of value. Please try again or select another model."),
        VisualBriefingValidationRule.CHART_SET_INVALID => TB("The charts of the model response did not match the planned briefing elements. Please try again or select another model."),
        VisualBriefingValidationRule.CHART_DATA_INVALID => TB("A chart of the model response contained invalid categories or data series. Please try again or select another model."),
        VisualBriefingValidationRule.CONTROL_ID_INVALID => TB("An interactive control of the model response used an invalid identifier. Please try again or select another model."),
        VisualBriefingValidationRule.CONTROL_TARGET_INVALID => TB("An interactive control of the model response targeted an invalid briefing element. Please try again or select another model."),
        VisualBriefingValidationRule.CONTROL_STATE_INVALID => TB("An interactive control of the model response used an invalid initial state. Please try again or select another model."),
        VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID => TB("A briefing element of the model response was missing its required interactive controls. Please try again or select another model."),
        VisualBriefingValidationRule.FORMULA_TARGET_INVALID => TB("A calculation of the model response targeted an invalid briefing element. Please try again or select another model."),
        VisualBriefingValidationRule.FORMULA_AST_INVALID => TB("A calculation of the model response used an invalid operation. Please try again or select another model."),
        VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID => TB("The accessibility texts of the model response did not match the briefing elements. Please try again or select another model."),
        VisualBriefingValidationRule.ACCESSIBILITY_TEXT_INVALID => TB("An accessibility text of the model response was empty or invalid. Please try again or select another model."),
        VisualBriefingValidationRule.LAYOUT_INVALID => TB("The model response used an invalid briefing layout. Please try again or select another model."),
        VisualBriefingValidationRule.TEMPLATE_ATTRIBUTE_PROHIBITED => TB("The model response used a prohibited attribute. Please try again or select another model."),
        VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED => TB("The model response contained markup or code, which this briefing does not allow. Please try again or select another model."),
        VisualBriefingValidationRule.COMPILER_OUTPUT_INVALID => TB("AI Studio compiled this briefing into an inconsistent result. Please copy the technical details and report this issue."),

        _ => string.Empty,
    };

    /// <summary>
    /// Gets the localized message for one failure code.
    /// </summary>
    /// <param name="code">The stable failure code.</param>
    /// <returns>The localized message.</returns>
    private static string ToUserMessage(this VisualBriefingFailureCode code) => code switch
    {
        VisualBriefingFailureCode.PROVIDER_NOT_SELECTED => TB("This briefing has no provider selected. Please select a provider before you generate a briefing."),
        VisualBriefingFailureCode.MODEL_CAPABILITY_MISSING => TB("The selected model lacks a capability this briefing needs. Please select another model."),
        VisualBriefingFailureCode.SOURCE_UNREACHABLE => TB("A source of this briefing can no longer be reached. Please relink or remove the affected source."),
        VisualBriefingFailureCode.TRANSCRIPT_UNAVAILABLE => TB("A media transcript of this briefing is missing or outdated. Please transcribe the affected media again."),
        VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED => TB("The sources of this briefing could not be prepared."),
        VisualBriefingFailureCode.PROVIDER_CALL_FAILED => TB("The selected provider could not complete this briefing stage."),
        VisualBriefingFailureCode.RESPONSE_JSON_INVALID => TB("The model did not return valid JSON. Please try again or select another model."),
        VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID => TB("The model response did not match the required contract. Please try again or select another model."),
        VisualBriefingFailureCode.COMPILER_INVARIANT_VIOLATED => TB("AI Studio compiled this briefing into an inconsistent result. Please copy the technical details and report this issue."),
        VisualBriefingFailureCode.SOURCE_COVERAGE_INVALID => TB("The model did not cover every source of this briefing exactly once. Please try again or select another model."),
        VisualBriefingFailureCode.ASSET_PLAN_INVALID => TB("The model did not plan every visual asset of this briefing exactly once. Please try again or select another model."),
        VisualBriefingFailureCode.CONTENT_SIGNATURE_INCOMPATIBLE => TB("The updated content no longer fits the current presentation. You can continue as a rebuild."),
        VisualBriefingFailureCode.PRESENTATION_INVALID => TB("The presentation of the model response did not match the briefing contract. Please try again or select another model."),
        VisualBriefingFailureCode.ASSEMBLY_FAILED => TB("This briefing could not be assembled."),
        VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED => TB("The assembled briefing did not pass the security validation."),
        VisualBriefingFailureCode.STORE_FAILED => TB("The new version of this briefing could not be saved."),
        VisualBriefingFailureCode.NO_CHANGES => TB("This operation did not change the briefing, so no new version was created."),
        VisualBriefingFailureCode.CANCELED => TB("This visual briefing operation was canceled."),
        VisualBriefingFailureCode.BUILD_INTERRUPTED => TB("AI Studio was closed while this briefing was being built. You can resume the build."),
        VisualBriefingFailureCode.UNEXPECTED => TB("This visual briefing operation failed because of an unexpected internal error. Please copy the technical details for support."),

        _ => string.Empty,
    };
}