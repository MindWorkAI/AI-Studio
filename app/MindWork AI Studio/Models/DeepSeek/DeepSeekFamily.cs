using static AIStudio.Provider.Capability;

namespace AIStudio.Models.DeepSeek;

/// <summary>
/// DeepSeek, from V3 to V4, including R1 and the checkpoints distilled from it.
/// </summary>
/// <remarks>
/// One family for all of it, and bound to no provider: DeepSeek publishes its models as open
/// weights and offers them on its own platform under the very same names, so a rule written once
/// answers wherever the model turns up. The old code arrived at that by having its DeepSeek
/// function call the open weights function, which is one of the loops this rebuild is undoing.
///
/// The distills are the case the whole priority question came from. They are Llama and Qwen
/// checkpoints fine-tuned on R1 answers, so they carry "r1" in their name and would be read as R1
/// itself -- which would promise the tool calling they lost together with R1's chat template. Here
/// the rule for them is the R1 rule with one condition more, and that alone decides it.
///
/// Point releases behind a dot need a line of their own, as everywhere: "deepseek-v4" does not
/// answer for "deepseek-v4.1", because a dot separates versions rather than name parts.
/// </remarks>
public sealed class DeepSeekFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.DEEP_SEEK;

    /// <inheritdoc />
    public override ModelSource Source => new("https://api-docs.deepseek.com/quick_start/pricing", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.DeepSeek.cs and the DeepSeek block of ProviderExtensions.OpenSource.cs. The pricing page states a 1M window for the V4 models; the older lines are served at different sizes depending on who serves them, so no window is stated for them.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("deepseek").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        // The V3 line answers directly and calls functions:
        builder.Rule("deepseek-v3").AsPrefix().Inherits()
            .Capabilities(FUNCTION_CALLING);

        //
        // From V3.1 on there is a thinking mode which the request turns on, and V3.2 added tool
        // calling inside it. The gateways write these either as "deepseek-v3.1" or as
        // "deepseek-chat-v3.1", so the version alone is what is looked for.
        //
        builder.Rule("deepseek").AsSegment().AlsoContains("v3.1").InheritsFrom("deepseek-v3")
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("deepseek").AsSegment().AlsoContains("v3.2").InheritsFrom("deepseek-v3")
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("deepseek-r1").AsPrefix().InheritsFrom("deepseek-v3")
            .Reasoning(ReasoningSupport.ALWAYS);

        // The distills kept the chat template of the model they were built from, so none of the
        // tool calling R1 itself was trained for survived:
        builder.Rule("deepseek-r1").AsPrefix().AlsoContains("distill").Inherits()
            .Removes(FUNCTION_CALLING);

        builder.Rule("deepseek-v4").AsPrefix().InheritsFrom("deepseek-v3")
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT)
            .ContextWindow(1_000_000);

        builder.Rule("deepseek-v4").AsPrefix().AlsoContains("vision").Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT);

        //
        // The two aliases of DeepSeek's own platform. They name a mode rather than a model: both
        // point at the current flash model, one with thinking and one without. Exactly these
        // names and no others -- "deepseek-chat-v3.1" is a gateway's name for a version, not this
        // alias.
        //
        builder.Rule("deepseek-chat").AsExact().InheritsFrom("deepseek-v3");

        builder.Rule("deepseek-reasoner").AsExact().InheritsFrom("deepseek-v3")
            .Reasoning(ReasoningSupport.ALWAYS);
    }
}