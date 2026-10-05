using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Metadata;

using SharedTools;

namespace AIStudio.Tools;

/// <summary>
/// The User-Agent which AI Studio sends to self-hosted servers, when the user allows it.
/// </summary>
/// <remarks>
/// It lets the operators of a self-hosted server or a gateway such as LiteLLM tell requests from
/// AI Studio apart from other clients, e.g., in their logs, and see which feature sent them.<br/><br/>
/// Who receives it is decided in IsAllowedFor, and nowhere else: only self-hosted servers, and only
/// when the user or their organization switched on sharing the feature usage. Without that, AI Studio
/// sends no User-Agent at all, the same as before this existed. Cloud providers never receive it.
/// </remarks>
public static class AppUserAgent
{
    private static readonly MetaDataAttribute META_DATA = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()!;

    /// <summary>
    /// The User-Agent naming the app, its version, and the platform, e.g., <c>MindWorkAIStudio/26.9.1 (osx-arm64)</c>.
    /// </summary>
    public static readonly string BASE = CreateBase();

    /// <summary>
    /// Whether requests to the given provider may carry the User-Agent.
    /// </summary>
    /// <param name="provider">The provider which receives the requests.</param>
    /// <param name="settingsManager">The settings, which say whether the user shares the feature usage.</param>
    /// <returns>True when the provider runs on a self-hosted server and sharing is switched on; otherwise, false.</returns>
    public static bool IsAllowedFor(LLMProviders provider, SettingsManager settingsManager) =>
        provider.RunsOnSelfHostedServer() && settingsManager.ConfigurationData.App.ShareFeatureUsageWithSelfHostedServerOperators;

    /// <summary>
    /// Sets the User-Agent as default header of the given HTTP client.
    /// </summary>
    /// <param name="httpClient">The HTTP client, whose requests should carry the User-Agent.</param>
    public static void Apply(HttpClient httpClient) => httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(BASE);

    /// <summary>
    /// Replaces the User-Agent of a single request by one naming the component of the given chat thread.
    /// </summary>
    /// <param name="headers">The headers of the request.</param>
    /// <param name="chatThread">The chat thread, whose component sent the request.</param>
    public static void ApplyComponent(HttpRequestHeaders headers, ChatThread chatThread)
    {
        headers.Remove("User-Agent");
        headers.TryAddWithoutValidation("User-Agent", ForComponent(chatThread.RuntimeComponent, chatThread.RuntimeAssistantName));
    }

    /// <summary>
    /// Creates the User-Agent naming the given component, e.g., <c>MindWorkAIStudio/26.9.1 (osx-arm64) Component/TRANSLATION_ASSISTANT</c>.
    /// </summary>
    /// <param name="component">The component which sends the request.</param>
    /// <param name="assistantName">The name of the assistant plugin, if any.</param>
    /// <returns>The User-Agent.</returns>
    public static string ForComponent(Components component, string assistantName = "")
    {
        if (component is Components.NONE)
            return BASE;

        var userAgent = $"{BASE} Component/{component}";
        var assistantToken = ToProductToken(assistantName);
        return string.IsNullOrWhiteSpace(assistantToken)
            ? userAgent
            : $"{userAgent} Assistant/{assistantToken}";
    }

    /// <summary>
    /// Converts a free-text name into a valid product token of a User-Agent.
    /// </summary>
    /// <remarks>
    /// Header values are ASCII only, and a product token must not contain separators like spaces,
    /// slashes, or parentheses. Diacritics are dropped (Ü becomes U), everything else is replaced by a dash.
    /// </remarks>
    private static string ToProductToken(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        const int MAX_LENGTH = 64;
        var normalized = name.Trim().Replace("ß", "ss").Normalize(NormalizationForm.FormD);
        var token = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
                token.Append(character);
            else if (token.Length > 0 && token[^1] is not '-')
                token.Append('-');

            if (token.Length >= MAX_LENGTH)
                break;
        }

        return token.ToString().TrimEnd('-');
    }

    private static string CreateBase()
    {
        var rid = RIDExtensions.GetCurrentRID().AsMicrosoftRid();
        return string.IsNullOrWhiteSpace(rid)
            ? $"MindWorkAIStudio/{META_DATA.Version}"
            : $"MindWorkAIStudio/{META_DATA.Version} ({rid})";
    }
}