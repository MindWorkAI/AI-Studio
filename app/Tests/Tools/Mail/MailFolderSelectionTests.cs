using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks which folders of a mailbox the sync works through.
/// </summary>
[TestFixture]
public sealed class MailFolderSelectionTests
{
    private static readonly IReadOnlyList<MailServerFolder> SERVER_FOLDERS =
    [
        Folder("Archive", MailFolderSpecialUse.ARCHIVE),
        Folder("Drafts", MailFolderSpecialUse.DRAFTS),
        Folder("Junk", MailFolderSpecialUse.JUNK),
        Folder("Projects"),
        Folder("Projects/Alpha"),
        Folder("Projects/Alpha/Drafts of the board"),
        Folder("ProjectsOld"),
        Folder("Sent", MailFolderSpecialUse.SENT),
        Folder("Sent/2024"),
        Folder("Starred", MailFolderSpecialUse.FLAGGED),
        Folder("Trash", MailFolderSpecialUse.TRASH),
        Folder("[Gmail]", canSelect: false),
        Folder("[Gmail]/All Mail", MailFolderSpecialUse.ALL),
        Folder("[Gmail]/Important", MailFolderSpecialUse.IMPORTANT),
        Folder("INBOX", isInbox: true),
        Folder("INBOX/Invoices"),
    ];

    private static readonly DateTimeOffset RECEIVED_SINCE = new(2025, 10, 10, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheWholeMailboxLeavesOutWhatWasThrownAway()
    {
        var selection = MailFolderSelection.Select(SERVER_FOLDERS, string.Empty, true);
        Assert.Multiple(() =>
        {
            Assert.That(selection.RootFolderFound, Is.True);
            Assert.That(selection.Folders.Select(folder => folder.FullName), Is.EqualTo(new[]
            {
                "INBOX",
                "Sent",
                "Drafts",
                "Archive",
                "INBOX/Invoices",
                "Projects",
                "Projects/Alpha",
                "Projects/Alpha/Drafts of the board",
                "ProjectsOld",
                "Sent/2024",
                "[Gmail]/All Mail",
            }), "The inbox comes first, then the sent mails and the drafts; the trash, the junk folder, the virtual folders and the grouping folder stay out.");
        });
    }

    [Test]
    public void WithoutTheSentMailsAndDraftsTheWholeMailboxStaysWhole() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, string.Empty, false).Folders, Is.EqualTo(MailFolderSelection.Select(SERVER_FOLDERS, string.Empty, true).Folders), "Without a root folder, the sent mails and the drafts belong to the whole mailbox anyway.");

    [Test]
    public void ARootFolderTakesItsSubfoldersAlongAndNothingElse() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "Projects", false).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Projects", "Projects/Alpha", "Projects/Alpha/Drafts of the board" }), "A folder merely starting with the same name got in.");

    [TestCase(true, new[] { "INBOX", "Sent", "Drafts", "INBOX/Invoices" })]
    [TestCase(false, new[] { "INBOX", "INBOX/Invoices" })]
    public void SentMailsAndDraftsComeAlongFromOutsideTheRootFolderWhenIncluded(bool includeSentAndDrafts, string[] expectedFolders) =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "INBOX", includeSentAndDrafts).Folders.Select(folder => folder.FullName), Is.EqualTo(expectedFolders), "The subfolders of the sent mails must stay out, since the server marks only the folder itself.");

    [TestCase(true)]
    [TestCase(false)]
    public void SentMailsAndDraftsBelowTheRootFolderCountOnce(bool includeSentAndDrafts)
    {
        // Some servers keep every folder below the inbox, e.g. Dovecot with the namespace prefix "INBOX.":
        IReadOnlyList<MailServerFolder> serverFolders =
        [
            Folder("INBOX", isInbox: true),
            Folder("INBOX/Drafts", MailFolderSpecialUse.DRAFTS),
            Folder("INBOX/Invoices"),
            Folder("INBOX/Sent", MailFolderSpecialUse.SENT),
        ];

        Assert.That(MailFolderSelection.Select(serverFolders, "INBOX", includeSentAndDrafts).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "INBOX", "INBOX/Sent", "INBOX/Drafts", "INBOX/Invoices" }));
    }

    [Test]
    public void SentMailsAsRootFolderCountOnce() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "Sent", true).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Sent", "Drafts", "Sent/2024" }));

    [Test]
    public void FoldersTheServerDoesNotMarkAreNeverToldByTheirNames()
    {
        IReadOnlyList<MailServerFolder> serverFolders = [Folder("INBOX", isInbox: true), Folder("Drafts"), Folder("Sent")];
        Assert.That(MailFolderSelection.Select(serverFolders, "INBOX", true).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "INBOX" }), "A server without the special-use marks leaves nothing to tell these folders by.");
    }

    [Test]
    public void TheInboxIsMatchedWhateverItsCase() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "inbox", false).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "INBOX", "INBOX/Invoices" }));

    [Test]
    public void AVirtualFolderCountsWhenItIsTheRootFolder() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "Starred", false).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Starred" }));

    [Test]
    public void TheTrashNeverCountsNotEvenAsRootFolder()
    {
        var selection = MailFolderSelection.Select(SERVER_FOLDERS, "Trash", false);
        Assert.Multiple(() =>
        {
            Assert.That(selection.Folders, Is.Empty);
            Assert.That(selection.RootFolderFound, Is.True);
        });
    }

    [Test]
    public void ARootFolderTheServerNoLongerListsIsReported()
    {
        var selection = MailFolderSelection.Select(SERVER_FOLDERS, "Projects/Beta", true);
        Assert.Multiple(() =>
        {
            Assert.That(selection.RootFolderFound, Is.False, "A renamed root folder would read as a mailbox which became empty, even when the sent mails and the drafts are still there.");
            Assert.That(selection.Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Sent", "Drafts" }));
        });
    }

    [Test]
    public void AServerWithoutHierarchyHasNoSubfolders()
    {
        IReadOnlyList<MailServerFolder> flatFolders = [Folder("Projects", separator: '\0'), Folder("Projects.Alpha", separator: '\0')];
        Assert.That(MailFolderSelection.Select(flatFolders, "Projects", true).Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Projects" }));
    }

    [TestCase(MailFolderSpecialUse.DRAFTS, false)]
    [TestCase(MailFolderSpecialUse.SENT, true)]
    [TestCase(MailFolderSpecialUse.ALL, true)]
    [TestCase(MailFolderSpecialUse.NONE, true)]
    public void OnlyDraftsCountWhateverTheirAge(MailFolderSpecialUse specialUse, bool keepsThePeriod)
    {
        var expected = keepsThePeriod ? RECEIVED_SINCE : (DateTimeOffset?)null;
        Assert.That(MailFolderSelection.GetReceivedSince(Folder("Folder", specialUse), RECEIVED_SINCE), Is.EqualTo(expected));
    }

    [Test]
    public void AMailboxWithoutPeriodKeepsAllMailsOfEveryFolder() =>
        Assert.That(MailFolderSelection.GetReceivedSince(Folder("INBOX", isInbox: true), null), Is.Null);

    private static MailServerFolder Folder(string fullName, MailFolderSpecialUse specialUse = MailFolderSpecialUse.NONE, bool isInbox = false, bool canSelect = true, char separator = '/')
    {
        var separatorIndex = separator is '\0' ? -1 : fullName.LastIndexOf(separator);
        var parentFullName = separatorIndex < 0 ? string.Empty : fullName[..separatorIndex];
        var name = separatorIndex < 0 ? fullName : fullName[(separatorIndex + 1)..];
        return new(fullName, name, parentFullName, separator, specialUse, isInbox, canSelect);
    }
}