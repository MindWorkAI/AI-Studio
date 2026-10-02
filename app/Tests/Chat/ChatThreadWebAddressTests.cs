using AIStudio.Chat;
using AIStudio.Tools.Web;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks which web addresses count as given to the model in a chat.
/// </summary>
/// <remarks>
/// A chat restricted by a mailbox may read a web page only when its address was given to the
/// model: by the user, or by a tool. An address the model wrote itself never counts, its own
/// answers included, because it could have put anything of the chat into it.
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