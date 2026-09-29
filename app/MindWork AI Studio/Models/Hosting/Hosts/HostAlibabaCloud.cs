using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Alibaba Cloud Model Studio.
/// </summary>
/// <remarks>
/// Worth knowing about this one: several names mean a different model here than they do anywhere
/// else. "qwq" is the commercial qwq-plus on Model Studio and the open weights everywhere else.
/// That is not settled here but in the rules, which can bind themselves to a provider -- this host
/// exists so that they have a provider to bind to.
/// </remarks>
public sealed class HostAlibabaCloud : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.ALIBABA_CLOUD;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/compatibility-of-openai-with-dashscope", new DateOnly(2026, 9, 11), "Models are named plainly, and the app reaches them through the OpenAI-compatible endpoint.");
}