using AIStudio.Components;
using AIStudio.Tools.Mail;

using MudBlazor;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks how the folder tree of a mailbox is built from what the server lists.
/// </summary>
/// <remarks>
/// Servers differ in how they lay out their folders: one separates with a slash, another with a
/// dot, one keeps everything below the inbox, another lists a folder whose parent it never lists.
/// Whatever it lists has to show up exactly once, in the place its parent gives it.
/// </remarks>
[TestFixture]
public sealed class MailFolderPickerTests
{
    private const string WHOLE_MAILBOX = "Whole mailbox";

    [Test]
    public void FoldersNestBelowTheWholeMailboxWithTheInboxFirst()
    {
        var root = BuildRoot(
            Folder("Projects"),
            Folder("Projects/Alpha", "Projects"),
            Folder("Archive", specialUse: MailFolderSpecialUse.ARCHIVE),
            Folder("INBOX", isInbox: true));

        Assert.Multiple(() =>
        {
            Assert.That(root.Value, Is.Empty, "The root does not stand for the whole mailbox.");
            Assert.That(root.Text, Is.EqualTo(WHOLE_MAILBOX));
            Assert.That(ValuesOf(root), Is.EqualTo(new[] { "INBOX", "Archive", "Projects" }), "The top level is not ordered with the inbox first.");
            Assert.That(ValuesOf(Child(root, "Projects")), Is.EqualTo(new[] { "Projects/Alpha" }), "The subfolder did not land below its parent.");
        });
    }

    [Test]
    public void FoldersBelowTheInboxNestThere()
    {
        var root = BuildRoot(
            Folder("INBOX", isInbox: true, separator: '.'),
            Folder("INBOX.Sent", "INBOX", separator: '.', specialUse: MailFolderSpecialUse.SENT),
            Folder("INBOX.Drafts", "INBOX", separator: '.', specialUse: MailFolderSpecialUse.DRAFTS));

        Assert.Multiple(() =>
        {
            Assert.That(ValuesOf(root), Is.EqualTo(new[] { "INBOX" }));
            Assert.That(ValuesOf(Child(root, "INBOX")), Is.EqualTo(new[] { "INBOX.Drafts", "INBOX.Sent" }));
        });
    }

    [Test]
    public void AFolderWhoseParentIsNotListedGoesToTheTopLevel()
    {
        var root = BuildRoot(Folder("INBOX", isInbox: true), Folder("[Gmail]/Sent Mail", "[Gmail]", specialUse: MailFolderSpecialUse.SENT));
        Assert.That(ValuesOf(root), Is.EqualTo(new[] { "INBOX", "[Gmail]/Sent Mail" }));
    }

    [Test]
    public void AFolderListedTwiceShowsUpOnce()
    {
        var root = BuildRoot(Folder("Projects"), Folder("Projects"));
        Assert.That(ValuesOf(root), Is.EqualTo(new[] { "Projects" }));
    }

    [Test]
    public void FoldersWhichAreEachOthersParentDoNotHangTheTree()
    {
        var tree = MailFolderPicker.BuildTree([Folder("A", "B"), Folder("B", "A")], "A", WHOLE_MAILBOX);
        Assert.That(tree.Single().Children, Is.Empty);
    }

    [Test]
    public void TheWayToThePickedFolderIsExpanded()
    {
        var folders = new[]
        {
            Folder("Projects"),
            Folder("Projects/Alpha", "Projects"),
            Folder("Projects/Alpha/Reports", "Projects/Alpha"),
            Folder("Private"),
            Folder("Private/Travel", "Private"),
        };

        var root = MailFolderPicker.BuildTree(folders, "Projects/Alpha/Reports", WHOLE_MAILBOX).Single();
        Assert.Multiple(() =>
        {
            Assert.That(root.Expanded, Is.True, "The whole mailbox is collapsed.");
            Assert.That(Child(root, "Projects").Expanded, Is.True, "The way to the picked folder is collapsed.");
            Assert.That(Child(Child(root, "Projects"), "Projects/Alpha").Expanded, Is.True, "The way to the picked folder is collapsed.");
            Assert.That(Child(root, "Private").Expanded, Is.False, "A folder off the way to the picked folder is expanded.");
        });
    }

    [TestCase(MailFolderSpecialUse.NONE, ExpectedResult = true)]
    [TestCase(MailFolderSpecialUse.ARCHIVE, ExpectedResult = true)]
    [TestCase(MailFolderSpecialUse.SENT, ExpectedResult = true)]
    [TestCase(MailFolderSpecialUse.ALL, ExpectedResult = true)]
    [TestCase(MailFolderSpecialUse.TRASH, ExpectedResult = false)]
    [TestCase(MailFolderSpecialUse.JUNK, ExpectedResult = false)]
    public bool TheTrashAndTheJunkFolderCannotBePicked(MailFolderSpecialUse specialUse) => MailFolderPicker.CanBeSelected(Folder("Folder", specialUse: specialUse));

    private static MailServerFolder Folder(string fullName, string parentFullName = "", char separator = '/', MailFolderSpecialUse specialUse = MailFolderSpecialUse.NONE, bool isInbox = false)
    {
        var name = fullName[(fullName.LastIndexOf(separator) + 1)..];
        return new MailServerFolder(fullName, name, parentFullName, separator, specialUse, isInbox, true);
    }

    private static TreeItemData<string> BuildRoot(params MailServerFolder[] folders) => MailFolderPicker.BuildTree(folders, string.Empty, WHOLE_MAILBOX).Single();

    private static IEnumerable<string?> ValuesOf(TreeItemData<string> item) => item.Children?.Select(child => child.Value) ?? [];

    private static TreeItemData<string> Child(TreeItemData<string> item, string fullName) => item.Children!.Single(child => child.Value == fullName);
}