using AIStudio.Models.Matching;

namespace AIStudio.Models.Hosting;

/// <summary>
/// The ways a host wraps a model name, and how to take one wrapping off again.
/// </summary>
/// <remarks>
/// A wrapping is worked on the name as the provider reported it, never on the normalized one. That
/// is not a detail: normalizing writes every separator as a hyphen, so "meta-llama/Llama-3.3-70B"
/// and "meta-llama-llama-3.3-70b" are the same text afterwards and nobody can say where the
/// organization ended. The slash, the colon, the at sign, and the spaces are the whole evidence, and
/// they only exist in the original.
/// </remarks>
public static class HostNaming
{
    /// <summary>
    /// What separates the organization from the model on a hub.
    /// </summary>
    private const char ORGANIZATION_SEPARATOR = '/';

    /// <summary>
    /// What separates the model from how a request for it should be routed.
    /// </summary>
    private const char ROUTING_SEPARATOR = ':';

    /// <summary>
    /// What separates the model from the region it should run in.
    /// </summary>
    private const char REGION_SEPARATOR = '@';

    /// <summary>
    /// Takes the organization off a hub style name.
    /// </summary>
    /// <remarks>
    /// Hubs and gateways write "organization/model", and a few hosts put a whole path in front:
    /// Fireworks answers with "accounts/fireworks/models/llama-v3p1-405b-instruct". Taking one
    /// segment at a time is what covers both without a second rule -- the caller keeps asking until
    /// nothing is left to take.
    /// </remarks>
    /// <param name="id">The name as it arrived.</param>
    /// <param name="inner">The name without its first path segment.</param>
    /// <param name="declaredVendor">Who the organization says built the model, when we recognize it.</param>
    /// <returns>True, when there was an organization to take off.</returns>
    public static bool TrySplitOrganization(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
    {
        inner = id;
        declaredVendor = null;

        var separatorIndex = id.Original.IndexOf(ORGANIZATION_SEPARATOR);
        if (separatorIndex is -1)
            return false;

        var model = id.Original[(separatorIndex + 1)..];
        if (string.IsNullOrWhiteSpace(model))
            return false;

        inner = new(model);

        //
        // An organization nobody recognizes says nothing rather than saying "unknown": the rules
        // may still work out who built the model from its name, and a stated vendor would stop
        // them from trying.
        //
        var vendor = VendorOfOrganization(id.Original[..separatorIndex]);
        declaredVendor = vendor is ModelVendor.UNKNOWN ? null : vendor;
        return true;
    }

    /// <summary>
    /// Takes the routing suffix off a name.
    /// </summary>
    /// <remarks>
    /// The suffix says how a request is served, not what the model is: "google/gemma-4-31B-it:novita"
    /// is the same model as "google/gemma-4-31B-it". On the Hugging Face router it names the
    /// inference provider; Requesty writes the service tier there, as in "openai/gpt-6-sol:flex", or
    /// a preset reasoning effort, as in "openai/o4-mini:high". Names at both hosts carry no colon of
    /// their own, so the last one always starts the suffix. This is not true everywhere -- Ollama
    /// writes the variant after a colon, as in "qwen3.8:27b-mlx", and taking that off would throw
    /// away which model it is. That is why only the hosts which put a suffix there ask for this.
    /// </remarks>
    /// <param name="id">The name as it arrived.</param>
    /// <param name="inner">The name without its routing suffix.</param>
    /// <returns>True, when there was a suffix to take off.</returns>
    public static bool TryStripRoutingSuffix(in ModelId id, out ModelId inner) => TryStripSuffix(id, ROUTING_SEPARATOR, out inner);

    /// <summary>
    /// Takes the region suffix off a name.
    /// </summary>
    /// <remarks>
    /// The suffix says where a request runs, not what the model is:
    /// "bedrock/claude-opus-5@eu-central-1" is the same model as "bedrock/claude-opus-5", served from
    /// Frankfurt. Requesty writes names this way, and every suffix it used on the day we checked was a
    /// region: "eu", "us", or a data center of AWS, Azure, or Google. No model name carries an at sign
    /// of its own, so the last one always starts the suffix.
    /// </remarks>
    /// <param name="id">The name as it arrived.</param>
    /// <param name="inner">The name without its region suffix.</param>
    /// <returns>True, when there was a suffix to take off.</returns>
    public static bool TryStripRegionSuffix(in ModelId id, out ModelId inner) => TryStripSuffix(id, REGION_SEPARATOR, out inner);

    /// <summary>
    /// Takes off everything from the last separator on.
    /// </summary>
    /// <param name="id">The name as it arrived.</param>
    /// <param name="separator">The character which starts the suffix.</param>
    /// <param name="inner">The name without its suffix.</param>
    /// <returns>True, when there was a suffix to take off.</returns>
    private static bool TryStripSuffix(in ModelId id, char separator, out ModelId inner)
    {
        inner = id;

        var separatorIndex = id.Original.LastIndexOf(separator);
        if (separatorIndex is -1)
            return false;

        var model = id.Original[..separatorIndex];
        if (string.IsNullOrWhiteSpace(model))
            return false;

        inner = new(model);
        return true;
    }

    /// <summary>
    /// Takes the position in a menu off a name.
    /// </summary>
    /// <remarks>
    /// Blablador answers with the line a person would read in a list: "1 - Llama3 405 the best
    /// general model". The leading number is where the model sits in that list, and it changes
    /// whenever the operator adds one.
    ///
    /// The spaces around the hyphen are what makes this safe to ask. A number followed directly by
    /// a hyphen is an ordinary part of a name -- "70b-instruct" would lose the size it is named
    /// after -- so only the spaced form counts as a menu position.
    /// </remarks>
    /// <param name="id">The name as it arrived.</param>
    /// <param name="inner">The name without its leading number.</param>
    /// <returns>True, when there was a menu position to take off.</returns>
    public static bool TryStripMenuPosition(in ModelId id, out ModelId inner)
    {
        inner = id;

        var text = id.Original.AsSpan();
        var digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits]))
            digits++;

        if (digits is 0)
            return false;

        var afterDigits = text[digits..];
        if (afterDigits.IsEmpty || afterDigits[0] is not ' ')
            return false;

        var afterSpace = afterDigits.TrimStart();
        if (afterSpace.IsEmpty || afterSpace[0] is not '-')
            return false;

        var afterHyphen = afterSpace[1..];
        if (afterHyphen.IsEmpty || afterHyphen[0] is not ' ')
            return false;

        var model = afterHyphen.TrimStart();
        if (model.IsEmpty)
            return false;

        inner = new(model.ToString());
        return true;
    }

    /// <summary>
    /// Who an organization on a hub stands for.
    /// </summary>
    /// <remarks>
    /// Hubs name the organization which published the weights, which is who built the model. The
    /// spellings are theirs, not ours, which is why several of them appear twice: the same vendor
    /// publishes under one name on one hub and another name on the next. Anything not listed is
    /// somebody we have no rules for yet, and saying so is the honest answer.
    /// </remarks>
    /// <param name="organization">The organization as the host wrote it, in any casing.</param>
    /// <returns>The vendor, or unknown.</returns>
    public static ModelVendor VendorOfOrganization(string organization) => organization.ToLowerInvariant() switch
    {
        "openai" => ModelVendor.OPEN_AI,
        "anthropic" => ModelVendor.ANTHROPIC,
        "google" => ModelVendor.GOOGLE,
        "mistral" or "mistralai" => ModelVendor.MISTRAL_AI,
        "meta" or "meta-llama" => ModelVendor.META,
        "alibaba" or "qwen" => ModelVendor.ALIBABA,
        "deepseek" or "deepseek-ai" => ModelVendor.DEEP_SEEK,
        "perplexity" => ModelVendor.PERPLEXITY,
        "x-ai" or "xai" => ModelVendor.XAI,
        "microsoft" => ModelVendor.MICROSOFT,
        "nvidia" => ModelVendor.NVIDIA,
        "ibm-granite" => ModelVendor.IBM,
        "cohere" or "coherelabs" or "cohereforai" => ModelVendor.COHERE,
        "moonshot" or "moonshotai" => ModelVendor.MOONSHOT_AI,
        "tencent" or "tencent-hunyuan" => ModelVendor.TENCENT,
        "z-ai" or "zai" or "zai-org" => ModelVendor.Z_AI,
        "minimax" or "minimaxai" or "minimaxi" => ModelVendor.MINIMAX,
        "ai2" or "allenai" => ModelVendor.AI2,
        "bytedance" or "bytedance-seed" => ModelVendor.BYTE_DANCE,
        "tii" or "tiiuae" => ModelVendor.TII,
        "inclusionai" => ModelVendor.INCLUSION_AI,
        "baidu" or "baidu-ernie" => ModelVendor.BAIDU,
        "huggingfacetb" => ModelVendor.HUGGING_FACE,
        "servicenow" or "servicenow-ai" => ModelVendor.SERVICE_NOW,
        "internlm" or "opengvlab" or "shanghai-ai-laboratory" => ModelVendor.SHANGHAI_AI_LAB,
        "swiss-ai" => ModelVendor.SWISS_AI,

        _ => ModelVendor.UNKNOWN,
    };
}