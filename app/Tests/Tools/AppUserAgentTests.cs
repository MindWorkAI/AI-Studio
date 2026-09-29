using System.Net.Http.Headers;

using AIStudio.Chat;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the User-Agent which AI Studio sends to LLM providers.
/// </summary>
/// <remarks>
/// Operators of a gateway such as LiteLLM read it in their logs to see which assistant or agent sent a
/// request. It has to stay a valid header value whatever a plugin calls its assistant, because an
/// invalid one would either break the request or silently vanish from the logs.
/// </remarks>
[TestFixture]
public sealed class AppUserAgentTests
{
    [Test]
    public void TheBaseNamesTheApp()
    {
        Assert.That(AppUserAgent.BASE, Does.StartWith("MindWorkAIStudio/"));
        Assert.That(ProductInfoHeaderValue.TryParse(AppUserAgent.BASE.Split(' ')[0], out _), Is.True);
    }

    [Test]
    public void ABuiltInComponentIsNamedByItsEnumValue()
    {
        var userAgent = AppUserAgent.ForComponent(AIStudio.Tools.Components.TRANSLATION_ASSISTANT);
        Assert.That(userAgent, Is.EqualTo($"{AppUserAgent.BASE} Component/TRANSLATION_ASSISTANT"));
    }

    [Test]
    public void NoComponentKeepsTheBase()
    {
        Assert.That(AppUserAgent.ForComponent(AIStudio.Tools.Components.NONE), Is.EqualTo(AppUserAgent.BASE));
    }

    [TestCase("Übersetzung für Straßen", "Ubersetzung-fur-Strassen")]
    [TestCase("  My (cool) Assistant/v2  ", "My-cool-Assistant-v2")]
    [TestCase("already_valid-name.1", "already_valid-name.1")]
    [TestCase("日本語", "")]
    public void APluginNameBecomesAValidProductToken(string pluginName, string expectedToken)
    {
        var userAgent = AppUserAgent.ForComponent(AIStudio.Tools.Components.DYNAMIC_ASSISTANT, pluginName);
        var expected = string.IsNullOrEmpty(expectedToken)
            ? $"{AppUserAgent.BASE} Component/DYNAMIC_ASSISTANT"
            : $"{AppUserAgent.BASE} Component/DYNAMIC_ASSISTANT Assistant/{expectedToken}";

        Assert.That(userAgent, Is.EqualTo(expected));
    }

    [Test]
    public void ARequestHeaderReplacesTheDefault()
    {
        using var request = new HttpRequestMessage();
        request.Headers.UserAgent.ParseAdd(AppUserAgent.BASE);

        AppUserAgent.ApplyComponent(request.Headers, new ChatThread { RuntimeComponent = AIStudio.Tools.Components.EMAIL_ASSISTANT });

        Assert.That(request.Headers.UserAgent.ToString(), Is.EqualTo($"{AppUserAgent.BASE} Component/EMAIL_ASSISTANT"));
        Assert.That(request.Headers.UserAgent.Count, Is.EqualTo(3), "The header must still parse as product, platform comment, and component.");
    }
}