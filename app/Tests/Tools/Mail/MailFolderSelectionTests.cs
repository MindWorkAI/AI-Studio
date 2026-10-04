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
        Folder("Junk", MailFolderSpecialUse.JUNK),
        Folder("Projects"),
        Folder("Projects/Alpha"),
        Folder("Projects/Alpha/Drafts of the board"),
        Folder("ProjectsOld"),
        Folder("Sent", MailFolderSpecialUse.SENT),
        Folder("Starred", MailFolderSpecialUse.FLAGGED),
        Folder("Trash", MailFolderSpecialUse.TRASH),
        Folder("[Gmail]", canSelect: false),
        Folder("[Gmail]/All Mail", MailFolderSpecialUse.ALL),
        Folder("[Gmail]/Important", MailFolderSpecialUse.IMPORTANT),
        Folder("INBOX", isInbox: true),
        Folder("INBOX/Invoices"),
    ];

    [Test]
    public void TheWholeMailboxLeavesOutWhatWasThrownAway()
    {
        var selection = MailFolderSelection.Select(SERVER_FOLDERS, string.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(selection.RootFolderFound, Is.True);
            Assert.That(selection.Folders.Select(folder => folder.FullName), Is.EqualTo(new[]
            {
                "INBOX",
                "Archive",
                "INBOX/Invoices",
                "Projects",
                "Projects/Alpha",
                "Projects/Alpha/Drafts of the board",
                "ProjectsOld",
                "Sent",
                "[Gmail]/All Mail",
            }), "The inbox comes first; the trash, the junk folder, the virtual folders and the grouping folder stay out.");
        });
    }

    [Test]
    public void ARootFolderTakesItsSubfoldersAlongAndNothingElse() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "Projects").Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Projects", "Projects/Alpha", "Projects/Alpha/Drafts of the board" }), "A folder merely starting with the same name got in.");

    [Test]
    public void TheInboxIsMatchedWhateverItsCase() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "inbox").Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "INBOX", "INBOX/Invoices" }));

    [Test]
    public void AVirtualFolderCountsWhenItIsTheRootFolder() =>
        Assert.That(MailFolderSelection.Select(SERVER_FOLDERS, "Starred").Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Starred" }));

    [Test]
    public void TheTrashNeverCountsNotEvenAsRootFolder()
    {
        var selection = MailFolderSelection.Select(SERVER_FOLDERS, "Trash");
        Assert.Multiple(() =>
        {
            Assert.That(selection.Folders, Is.Empty);
            Assert.That(selection.RootFolderFound, Is.True);
        });
    }

    [Test]
    public void ARootFolderTheServerNoLongerListsIsReported()
    {
        var selection = MailFolderSelection.Select(SERVER_FOLDERS, "Projects/Beta");
        Assert.Multiple(() =>
        {
            Assert.That(selection.RootFolderFound, Is.False, "A renamed root folder would read as a mailbox which became empty.");
            Assert.That(selection.Folders, Is.Empty);
        });
    }

    [Test]
    public void AServerWithoutHierarchyHasNoSubfolders()
    {
        IReadOnlyList<MailServerFolder> flatFolders = [Folder("Projects", separator: '\0'), Folder("Projects.Alpha", separator: '\0')];
        Assert.That(MailFolderSelection.Select(flatFolders, "Projects").Folders.Select(folder => folder.FullName), Is.EqualTo(new[] { "Projects" }));
    }

    private static MailServerFolder Folder(string fullName, MailFolderSpecialUse specialUse = MailFolderSpecialUse.NONE, bool isInbox = false, bool canSelect = true, char separator = '/')
    {
        var separatorIndex = separator is '\0' ? -1 : fullName.LastIndexOf(separator);
        var parentFullName = separatorIndex < 0 ? string.Empty : fullName[..separatorIndex];
        var name = separatorIndex < 0 ? fullName : fullName[(separatorIndex + 1)..];
        return new(fullName, name, parentFullName, separator, specialUse, isInbox, canSelect);
    }
}