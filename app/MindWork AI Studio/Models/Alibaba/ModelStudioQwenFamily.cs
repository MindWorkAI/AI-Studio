using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// The Qwen models Alibaba Cloud Model Studio serves.
/// </summary>
/// <remarks>
/// Everything in this folder is bound to Alibaba Cloud, and that is the point of it. Model Studio
/// sells commercial models -- qwen-max, qwen3.7-max, qwq-plus -- which carry the family names of
/// the open weights without being them, and it answers differently for several names the open
/// weights share with it. The old rules kept the two apart by having two functions; here they are
/// kept apart by saying which provider a rule speaks for.
///
/// The first rule is the catalog's own fallback, and it is written as a plain substring on purpose:
/// a substring is the weakest thing a rule can be, so every other rule here beats it without anyone
/// arranging that. It also has to be one, because "qwen2.5-72b-instruct" does not contain "qwen" as
/// a whole name part -- the version grows straight out of the family name.
/// </remarks>
public sealed class ModelStudioQwenFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Alibaba.cs, which follow Alibaba's own list of models that call functions.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("qwen").AsSubstring().OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        // Qwen 3 thinks when asked to:
        builder.Rule("qwen3").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).Inherits()
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("qwen3.5").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).InheritsFrom("qwen3")
            .Capabilities(MULTIPLE_IMAGE_INPUT);

        builder.Rule("qwen3.6").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).InheritsFrom("qwen3")
            .Capabilities(MULTIPLE_IMAGE_INPUT | VIDEO_INPUT)
            .Reasoning(ReasoningSupport.ALWAYS);

        //
        // Qwen 3.7 thinks unless it is told not to, and it started out reading nothing but text.
        // Vision arrived in the middle of the series, so only the June snapshot may be told that
        // it sees: the rolling max alias still answers as the May one.
        //
        builder.Rule("qwen3.7").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).InheritsFrom("qwen3")
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("qwen3.7").AsPrefix().AlsoContains("preview").OnlyOn(LLMProviders.ALIBABA_CLOUD).Inherits()
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("qwen3.7").AsPrefix().AlsoContains("2026-05-17").OnlyOn(LLMProviders.ALIBABA_CLOUD).Inherits();

        builder.Rule("qwen3.7").AsPrefix().AlsoContains("2026-06-08").OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | VIDEO_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        // Qwen 3.8, whose 27B checkpoint is what the tier without a size resolves to:
        builder.Rule("qwen3.8").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).InheritsFrom("qwen3")
            .Capabilities(MULTIPLE_IMAGE_INPUT)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("qwen3.8-flash").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).Inherits()
            .Capabilities(VIDEO_INPUT);

        //
        // Unlike the open-weight checkpoint of the same name, the Max model keeps its vision when
        // it is reached through Model Studio:
        //
        builder.Rule("qwen3.8-max").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD).InheritsFrom("qwen3.8-flash")
            .Reasoning(ReasoningSupport.ALWAYS);
    }
}