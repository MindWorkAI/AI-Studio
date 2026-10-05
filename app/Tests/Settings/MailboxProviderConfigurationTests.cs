using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how the mail servers of an organization are read from a configuration plugin.
/// </summary>
/// <remarks>
/// Users connect with their password to whatever server such an entry names, so an entry is taken
/// exactly as written or not at all. Leaving out an invalid field instead would connect another way
/// than the organization meant, e.g., to the default port of another encryption.
/// </remarks>
[TestFixture]
public sealed class MailboxProviderConfigurationTests
{
    private static readonly Guid PLUGIN_ID = new("33333333-3333-3333-3333-333333333333");

    private const string ID = "6f1c2d3e-4a5b-4c6d-8e7f-9a0b1c2d3e4f";

    private static readonly IReadOnlyDictionary<string, string?> REQUIRED_FIELDS = new Dictionary<string, string?>
    {
        ["Id"] = $"\"{ID}\"",
        ["Name"] = "\"Exchange\"",
        ["Host"] = "\"imap.example.org\"",
    };

    [Test]
    public async Task AFullEntryIsReadAsWritten()
    {
        var provider = await ReadAsync(new Dictionary<string, string?>
        {
            ["Name"] = "\" Exchange (headquarters) \"",
            ["TransportSecurity"] = "\"STARTTLS\"",
            ["Port"] = "1143",
            ["UsernameHint"] = "\"Your account as DOMAIN\\\\username\"",
            ["HelpUrl"] = "\"https://intranet.example.org/mail/imap\"",
        });

        Assert.That(provider, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(provider!.Id, Is.EqualTo(ID));
            Assert.That(provider.EnterpriseConfigurationPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(provider.Name, Is.EqualTo("Exchange (headquarters)"));
            Assert.That(provider.Host, Is.EqualTo("imap.example.org"));
            Assert.That(provider.TransportSecurity, Is.EqualTo(MailboxTransportSecurity.STARTTLS));
            Assert.That(provider.Port, Is.EqualTo(1143));
            Assert.That(provider.UsernameHint, Is.EqualTo(@"Your account as DOMAIN\username"));
            Assert.That(provider.HelpUrl, Is.EqualTo("https://intranet.example.org/mail/imap"));
        });
    }

    [Test]
    public async Task WithoutAPortTheEncryptionDecidesIt()
    {
        var withoutEncryption = await ReadAsync(new Dictionary<string, string?>());
        var withStartTls = await ReadAsync(new Dictionary<string, string?> { ["TransportSecurity"] = "\"STARTTLS\"" });

        Assert.Multiple(() =>
        {
            Assert.That(withoutEncryption?.TransportSecurity, Is.EqualTo(MailboxTransportSecurity.SSL_ON_CONNECT), "TLS from the first byte on is the safer of the two ways.");
            Assert.That(withoutEncryption?.Port, Is.EqualTo(993));
            Assert.That(withStartTls?.Port, Is.EqualTo(143));
            Assert.That(withoutEncryption?.UsernameHint, Is.Empty);
            Assert.That(withoutEncryption?.HelpUrl, Is.Empty);
        });
    }

    [Test]
    public async Task AHostWithUmlautsIsKeptAsWritten()
    {
        var provider = await ReadAsync(new Dictionary<string, string?> { ["Host"] = "\" imap.müller.example \"" });

        Assert.That(provider?.Host, Is.EqualTo("imap.müller.example"), "The dialog shows the host the way the organization wrote it; only the connection uses its ASCII form.");
    }

    [TestCase("Id", null)]
    [TestCase("Id", "\"not a GUID\"")]
    [TestCase("Name", null)]
    [TestCase("Name", "\"  \"")]
    [TestCase("Host", null)]
    [TestCase("Host", "\"\"")]
    [TestCase("Host", "\"imaps://imap.example.org\"")]
    [TestCase("Host", "\"imap.example.org:993\"")]
    [TestCase("TransportSecurity", "\"NONE\"")]
    [TestCase("TransportSecurity", "\"UNKNOWN\"")]
    [TestCase("TransportSecurity", "\"SSL_ON_CONNECT, STARTTLS\"")]
    [TestCase("Port", "0")]
    [TestCase("Port", "65536")]
    [TestCase("HelpUrl", "\"ftp://intranet.example.org/mail\"")]
    [TestCase("HelpUrl", "\"/mail/imap\"")]
    public async Task AnEntryWithAnInvalidFieldIsLeftOut(string field, string? value)
    {
        var provider = await ReadAsync(new Dictionary<string, string?> { [field] = value });

        Assert.That(provider, Is.Null);
    }

    /// <summary>
    /// Reads an entry made of the required fields, changed by the given ones.
    /// </summary>
    /// <param name="changedFields">The fields to add or to replace, as Lua expressions; null removes a field.</param>
    private static async Task<DataMailboxProvider?> ReadAsync(IReadOnlyDictionary<string, string?> changedFields)
    {
        var fields = new Dictionary<string, string?>(REQUIRED_FIELDS);
        foreach (var (name, value) in changedFields)
            fields[name] = value;

        var entry = string.Join(",\n", fields.Where(field => field.Value is not null).Select(field => $"[\"{field.Key}\"] = {field.Value}"));

        var state = LuaState.Create();
        state.OpenBasicLibrary();

        await state.DoStringAsync($$"""
                                    PROVIDER = {
                                        {{entry}}
                                    }
                                    """);

        if (!state.Environment["PROVIDER"].TryRead<LuaTable>(out var table))
            throw new InvalidOperationException("The entry of this test is not a Lua table.");

        return DataMailboxProvider.TryParseConfiguration(1, table, PLUGIN_ID, NullLogger.Instance, out var provider) ? provider : null;
    }
}