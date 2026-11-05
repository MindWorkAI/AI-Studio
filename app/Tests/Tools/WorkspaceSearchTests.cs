using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what the chat search finds in stored chats, and how it tells where.
/// </summary>
/// <remarks>
/// Workspaces and chats are found each on their own. The words of a search may be spread over a chat's
/// own name and what the user sees of the chat: the text of its messages, the names of its attached
/// files, and the titles and addresses of its sources. The sidebar shows where the chat holds them, so
/// the search has to say which of these it was.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceSearchTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public async Task PrepareDataDirectory()
    {
        //
        // The data directory and the cache of the workspace tree are static state of the whole
        // application, which is why this fixture does not run alongside others. See
        // WorkspaceChatStoreTests for the details.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-chat-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;

        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;
        WorkspaceBehaviour.TestDataDirectory = null;
        WorkspaceBehaviour.InvalidateWorkspaceTreeCache();

        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    private static ContentBlock Message(ChatRole role, string text, List<FileAttachment>? files = null, List<Source>? sources = null, bool isHidden = false) => new()
    {
        Time = DateTimeOffset.Now,
        ContentType = ContentType.TEXT,
        Role = role,
        HideFromUser = isHidden,
        Content = new ContentText
        {
            Text = text,
            FileAttachments = files ?? [],
            Sources = sources ?? [],
        },
    };

    private async Task<Guid> StoreChatAsync(string workspaceName, string chatName, params ContentBlock[] blocks)
    {
        var workspace = await WorkspaceBehaviour.TryCreateWorkspaceAsync(workspaceName);
        var workspaceId = workspace.Success ? workspace.Workspace.WorkspaceId : await WorkspaceBehaviour.ResolveOrCreateWorkspaceIdByNameAsync(workspaceName);
        var chat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = chatName, Blocks = [..blocks] };

        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, "The chat could not be stored.");
        return chat.ChatId;
    }

    private static async Task<WorkspaceSearchResult?> FindAsync(string searchText, Guid chatId)
    {
        var results = await WorkspaceBehaviour.SearchWorkspaceChatsAsync(searchText, includeThreadContents: true);
        return results.Workspaces.SelectMany(workspace => workspace.Chats).Concat(results.TemporaryChats).Cast<WorkspaceSearchResult?>().FirstOrDefault(result => result!.Value.Chat.ChatId == chatId);
    }

    [Test]
    public async Task AChatIsFoundByTheNameOfAnAttachedFile()
    {
        var file = new FileAttachment(FileAttachmentType.DOCUMENT, "budget-2026.xlsx", Path.Combine(this.testDirectory, "budget-2026.xlsx"), 1024);
        var chatId = await this.StoreChatAsync("Projects", "Planning", Message(ChatRole.USER, "Please check this.", files: [file]));

        var result = await FindAsync("budget", chatId);

        Assert.That(result?.Passage, Is.EqualTo(new ChatSearchPassage(ChatSearchPassageKind.ATTACHED_FILE, "budget-2026.xlsx")));
    }

    [Test]
    public async Task AChatIsFoundByTheWholeAddressOfASource()
    {
        var source = new Source("Annual report", "https://example.com/search?q=budget", SourceOrigin.LLM);
        var chatId = await this.StoreChatAsync("Projects", "Planning", Message(ChatRole.AI, "Here you go.", sources: [source]));

        var result = await FindAsync("budget", chatId);

        Assert.That(result?.Passage, Is.EqualTo(new ChatSearchPassage(ChatSearchPassageKind.SOURCE, "Annual report (https://example.com/search?q=budget)")));
    }

    [Test]
    public async Task AMessageTellsMoreThanTheNameOfAFile()
    {
        var file = new FileAttachment(FileAttachmentType.DOCUMENT, "budget-2026.xlsx", Path.Combine(this.testDirectory, "budget-2026.xlsx"), 1024);
        var chatId = await this.StoreChatAsync("Projects", "Planning",
            Message(ChatRole.USER, "Please check this.", files: [file]),
            Message(ChatRole.AI, "The budget looks fine."));

        var result = await FindAsync("budget", chatId);

        Assert.That(result?.Passage?.Kind, Is.EqualTo(ChatSearchPassageKind.MESSAGE));
    }

    [Test]
    public async Task WordsMaySpreadOverTheNameOfAChatAndItsMessages()
    {
        var chatId = await this.StoreChatAsync("Projects", "Kickoff notes", Message(ChatRole.AI, "We fixed the budget at 40k."));

        var result = await FindAsync("kickoff budget", chatId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null, "The name holds one word and the messages the other.");
            Assert.That(result?.NameMatched, Is.False);
            Assert.That(result?.Passage?.Kind, Is.EqualTo(ChatSearchPassageKind.MESSAGE));
        }
    }

    [Test]
    public async Task TheNameOfTheWorkspaceDoesNotCountForItsChats()
    {
        var chatId = await this.StoreChatAsync("Project Alpha", "Kickoff notes", Message(ChatRole.AI, "We fixed the budget at 40k."));

        var result = await FindAsync("alpha budget", chatId);

        Assert.That(result, Is.Null, "Only the workspace holds the word alpha, and the workspace is found on its own.");
    }

    [Test]
    public async Task AWorkspaceIsFoundByItsNameWithoutItsChats()
    {
        var plainChatId = await this.StoreChatAsync("Budget 2026", "Kickoff notes", Message(ChatRole.AI, "Welcome."));
        var budgetChatId = await this.StoreChatAsync("Budget 2026", "Budget plan", Message(ChatRole.AI, "Welcome."));

        var results = await WorkspaceBehaviour.SearchWorkspaceChatsAsync("budget", includeThreadContents: true);
        var workspace = results.Workspaces.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.NameMatched, Is.True);
            Assert.That(workspace.Chats.Select(result => result.Chat.ChatId), Does.Not.Contain(plainChatId), "Its chats count only by what they hold themselves.");
            Assert.That(workspace.Chats.Select(result => result.Chat.ChatId), Does.Contain(budgetChatId));
        }
    }

    [Test]
    public async Task AChatFoundByItsNameTellsThatItsMessagesHoldTheWordsAsWell()
    {
        var chatId = await this.StoreChatAsync("Projects", "Budget plan", Message(ChatRole.AI, "The budget looks fine."));

        var result = await FindAsync("budget", chatId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result?.NameMatched, Is.True);
            Assert.That(result?.Passage?.Kind, Is.EqualTo(ChatSearchPassageKind.MESSAGE));
        }
    }

    [Test]
    public async Task HiddenMessagesDoNotCount()
    {
        var chatId = await this.StoreChatAsync("Projects", "Planning", Message(ChatRole.SYSTEM, "The secret budget.", isHidden: true));

        var result = await FindAsync("secret", chatId);

        Assert.That(result, Is.Null);
    }
}