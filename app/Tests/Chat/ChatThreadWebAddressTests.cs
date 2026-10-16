using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Services;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks which web addresses count as given to the model in a chat.
/// </summary>
/// <remarks>
/// Read Web Page reads a web page only when its address was given to the model, as long as its
/// free address choice is off or a mailbox restricts the chat: in the system prompt, by the user,
/// in a document the user attached, or by a tool. An address the model wrote itself never counts,
/// its own answers included, because it could have put anything of the chat into it.
/// </remarks>
[TestFixture]
public sealed class ChatThreadWebAddressTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void AnAddressTheUserWroteIsGiven()
    {
        var thread = Thread(Block(ChatRole.USER, "Please summarize https://example.org/report for me.", 1));

        Assert.Multiple(() =>
        {
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/report")), Is.True);
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://EXAMPLE.org/report#summary")), Is.True, "The server gets the same request.");
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/report?budget=2026")), Is.False, "The query would carry something the user never wrote.");
        });
    }

    [Test]
    public void AnAddressWithoutTlsTheUserWroteIsGiven()
    {
        // An internal service such as a simple Python web server for the results of a data science run:
        var thread = Thread(Block(ChatRole.USER, "The results of run 17 are on http://10.20.30.40:8000/run-17/, please compare them with run 16.", 1));

        Assert.Multiple(() =>
        {
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("http://10.20.30.40:8000/run-17/")), Is.True);
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("http://10.20.30.40:8000/run-16/")), Is.False, "The user named run 16, but not its address.");
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://10.20.30.40:8000/run-17/")), Is.False, "Another scheme is another request.");
        });
    }

    [Test]
    public void AnAddressInAHiddenMessageOfTheUserIsGiven()
    {
        // The prompt an assistant sends into a chat on behalf of the user:
        var thread = Thread(Block(ChatRole.USER, "Read https://example.org/handbook and answer.", 1, hidden: true));
        Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/handbook")), Is.True);
    }

    [Test]
    public void AnAddressInAnAnswerOfTheModelIsNotGiven()
    {
        var thread = Thread(
            Block(ChatRole.USER, "Where can I read more?", 1),
            Block(ChatRole.AI, "You can read more at https://example.org/more.", 2));

        Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/more")), Is.False, "The model wrote it, and it could have written a mail into it as well.");
    }

    [Test]
    public void AnAddressAToolReturnedIsGiven()
    {
        var thread = Thread(Block(ChatRole.USER, "What does the newsletter link to?", 1));
        thread.RuntimeWebAddressesFromTools.Add(WebAddresses.CreateRequestKey(new Uri("https://example.org/newsletter")));

        Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/newsletter")), Is.True);
    }

    [Test]
    public void AnAddressInTheSystemPromptIsGivenOnceTheRequestWasPrepared()
    {
        // A chat template written by the user, and the content of a data source the RAG process found:
        var thread = Thread(Block(ChatRole.USER, "Where are the travel rules?", 1));
        thread.SystemPrompt = "You answer questions about our handbook at https://handbook.example.org/.";
        thread.AugmentedData = "The travel rules stand on https://intranet.example.org/travel.";

        using var rustService = new RustService("1", "unused");
        thread.PrepareSystemPrompt(new SettingsManager(NullLogger<SettingsManager>.Instance, rustService));

        Assert.Multiple(() =>
        {
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://handbook.example.org/")), Is.True);
            Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://intranet.example.org/travel")), Is.True, "The RAG process found it in a data source, the model did not choose it.");
        });
    }

    [Test]
    public void AnAddressInADocumentTheUserAttachedIsGiven()
    {
        var message = new ContentText
        {
            Text = "Please check the links in the attached report.",
            RuntimeAttachmentWebAddresses = new HashSet<string>(StringComparer.Ordinal) { WebAddresses.CreateRequestKey(new Uri("https://example.org/sources/2026")) },
        };

        var thread = Thread(new ContentBlock { Time = START, ContentType = ContentType.TEXT, Content = message, Role = ChatRole.USER });

        Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/sources/2026")), Is.True, "The document is read only when the message is sent, which is when its addresses are collected.");
    }

    [Test]
    public void AnAddressInADocumentOfTheModelIsNotGiven()
    {
        var answer = new ContentText
        {
            Text = "Here is a summary.",
            RuntimeAttachmentWebAddresses = new HashSet<string>(StringComparer.Ordinal) { WebAddresses.CreateRequestKey(new Uri("https://example.org/chosen")) },
        };

        var thread = Thread(new ContentBlock { Time = START, ContentType = ContentType.TEXT, Content = answer, Role = ChatRole.AI });

        Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/chosen")), Is.False);
    }

    private static ChatThread Thread(params ContentBlock[] blocks) => new() { Blocks = [..blocks] };

    private static ContentBlock Block(ChatRole role, string text, int minute, bool hidden = false) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text },
        Role = role,
        HideFromUser = hidden,
    };
}