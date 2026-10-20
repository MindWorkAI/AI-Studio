using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks from where Read Web Page reads documents such as PDFs.
/// </summary>
/// <remarks>
/// Much of what an organization knows lies in the documents of its intranet and its wiki, so these
/// are read by default. A public document stays out by default: it is one more way for a stranger
/// to reach the model with text of their choosing.
/// </remarks>
[TestFixture]
public sealed class ReadWebPageDocumentTests
{
    private static readonly Uri WIKI = new("https://wiki.example.org/confluence/");

    [TestCase(DocumentAccess.OFF, true, false)]
    [TestCase(DocumentAccess.OFF, false, false)]
    [TestCase(DocumentAccess.INTERNAL_ONLY, true, true)]
    [TestCase(DocumentAccess.INTERNAL_ONLY, false, false)]
    [TestCase(DocumentAccess.ALL_ADDRESSES, true, true)]
    [TestCase(DocumentAccess.ALL_ADDRESSES, false, true)]
    public void TheSettingDecidesFromWhereDocumentsAreRead(DocumentAccess documentAccess, bool isInternal, bool expected)
    {
        Assert.That(ReadWebPageTool.IsDocumentAllowed(documentAccess, isInternal), Is.EqualTo(expected));
    }

    [TestCase(null, DocumentAccess.INTERNAL_ONLY)]
    [TestCase("", DocumentAccess.INTERNAL_ONLY)]
    [TestCase("ALL_ADDRESSES", DocumentAccess.ALL_ADDRESSES)]
    [TestCase("OFF", DocumentAccess.OFF)]
    [TestCase("EVERYTHING", DocumentAccess.OFF)]
    [TestCase("2", DocumentAccess.OFF)]
    public void AValueWhichNamesNoChoiceReadsAsOff(string? configuredValue, DocumentAccess expected)
    {
        Assert.That(ReadWebPageTool.ReadDocumentAccess(configuredValue), Is.EqualTo(expected), "A guess too loose would let the AI read documents nobody allowed.");
    }

    [TestCase("https://wiki.example.org/confluence/download/attachments/42/guideline.pdf", false, true)]
    [TestCase("https://example.org/report.pdf", false, false)]
    [TestCase("https://intranet.example.org/files/form.docx", true, true)]
    public void ADocumentOfTheWikiIsInternalWhateverItsAddress(string address, bool reachedNonPublicAddress, bool expected)
    {
        Assert.That(ReadWebPageTool.IsInternalAddress(new Uri(address), reachedNonPublicAddress, WIKI), Is.EqualTo(expected));
    }

    [Test]
    public void TheInstructionsNameWhereDocumentsAreRead()
    {
        var internalOnly = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.UNRESTRICTED, WIKI, ["intranet.example.org"], documentAccess: DocumentAccess.INTERNAL_ONLY);
        var allAddresses = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.UNRESTRICTED, WIKI, documentAccess: DocumentAccess.ALL_ADDRESSES);
        var off = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.UNRESTRICTED, WIKI, documentAccess: DocumentAccess.OFF);

        Assert.Multiple(() =>
        {
            Assert.That(internalOnly, Does.Contain("return their text only when they come from the wiki and the internal hosts configured in AI Studio"));
            Assert.That(allAddresses, Does.Contain("return their text as well"));
            Assert.That(off, Does.Contain("cannot be read"));
        });
    }

    [Test]
    public void WithoutWikiAndAllowedHostsNoDocumentIsInternal()
    {
        var instructions = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.UNRESTRICTED, wiki: null, documentAccess: DocumentAccess.INTERNAL_ONLY);
        Assert.That(instructions, Does.Contain("cannot be read"), "The model must not spend its calls on documents which are refused anyway.");
    }

    [TestCase("documentAccess", "EVERYTHING")]
    [TestCase("maxDocumentMegabytes", "500")]
    [TestCase("maxDocumentMegabytes", "0")]
    public async Task AnInvalidDocumentSettingIsReported(string fieldName, string value)
    {
        var tool = new ReadWebPageTool(null!, null!, null!, NullLogger<ReadWebPageTool>.Instance);
        var state = await tool.ValidateConfigurationAsync(tool.GetDefinition(), new Dictionary<string, string> { [fieldName] = value });

        Assert.That(state?.IsConfigured, Is.False);
    }

    [TestCase("documentAccess", "ALL_ADDRESSES")]
    [TestCase("maxDocumentMegabytes", "200")]
    public async Task AValidDocumentSettingIsAccepted(string fieldName, string value)
    {
        var tool = new ReadWebPageTool(null!, null!, null!, NullLogger<ReadWebPageTool>.Instance);
        var state = await tool.ValidateConfigurationAsync(tool.GetDefinition(), new Dictionary<string, string> { [fieldName] = value });

        Assert.That(state, Is.Null);
    }
}