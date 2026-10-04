using AIStudio.Tools.Mail;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows the folders of a mailbox as the server lists them, picks one of them, and creates new ones.
/// </summary>
/// <remarks>
/// The whole mailbox is the root of the tree and stands for no folder at all, i.e., an empty full
/// name. The trash and the junk folder cannot be picked: the synchronization leaves them out, so a
/// mailbox restricted to one of them would never hold a mail.
/// </remarks>
public partial class MailFolderPicker : MSGComponentBase
{
    /// <summary>
    /// The folders of the mailbox, as the server lists them.
    /// </summary>
    /// <remarks>
    /// The tree is built anew whenever another list arrives, so pass a new list instead of changing this one.
    /// </remarks>
    [Parameter]
    public IReadOnlyList<MailServerFolder> Folders { get; set; } = [];

    /// <summary>
    /// The full name of the picked folder, or empty for the whole mailbox.
    /// </summary>
    [Parameter]
    public string SelectedFolder { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SelectedFolderChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Creates a folder on the server, from the full name of the folder to create it in (empty at
    /// the top level) and the name of the new folder.
    /// </summary>
    /// <remarks>
    /// Returns the new folder, or null when the server did not create it, in which case the caller
    /// tells the user why. Without it, there is no way to create a folder here.
    /// </remarks>
    [Parameter]
    public Func<string, string, Task<MailServerFolder?>>? CreateFolder { get; set; }

    private IReadOnlyCollection<TreeItemData<string>> treeItems = [];
    private IReadOnlyList<MailServerFolder>? treeFolders;
    private Dictionary<string, MailServerFolder> foldersByName = new(StringComparer.Ordinal);
    private string newFolderName = string.Empty;
    private bool isCreatingFolder;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(this.treeFolders, this.Folders))
        {
            this.treeFolders = this.Folders;
            this.foldersByName = IndexByName(this.Folders);
            this.treeItems = BuildTree(this.Folders, this.SelectedFolder, T("Whole mailbox"));
        }

        base.OnParametersSet();
    }

    #endregion

    private char NewFolderSeparator => this.foldersByName.TryGetValue(this.SelectedFolder, out var parent)
        ? parent.DirectorySeparator
        : this.Folders.FirstOrDefault()?.DirectorySeparator ?? '\0';

    private bool IsNewFolderNameValid => ImapMailboxConnector.IsValidFolderName(this.newFolderName.Trim(), this.NewFolderSeparator);

    private bool CanCreateFolder => !this.isCreatingFolder && this.IsNewFolderNameValid;

    private string NewFolderHelperText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(this.newFolderName) && !this.IsNewFolderNameValid)
                return T("This name is too long, or it contains a character the server reserves for folder paths.");

            return string.IsNullOrEmpty(this.SelectedFolder)
                ? T("Creates the folder on the server right away, at the top level of the mailbox.")
                : string.Format(T("Creates the folder on the server right away, inside '{0}'."), this.SelectedFolder);
        }
    }

    private bool CanBeSelected(string? fullName)
    {
        if (fullName is null)
            return false;

        if (fullName.Length == 0)
            return true;

        return this.foldersByName.TryGetValue(fullName, out var folder) && CanBeSelected(folder);
    }

    private async Task SelectFolder(string? fullName)
    {
        if (!this.CanBeSelected(fullName) || fullName == this.SelectedFolder)
            return;

        this.SelectedFolder = fullName!;
        await this.SelectedFolderChanged.InvokeAsync(this.SelectedFolder);
    }

    private async Task CreateNewFolder()
    {
        if (this.CreateFolder is null || !this.CanCreateFolder)
            return;

        this.isCreatingFolder = true;
        try
        {
            var createdFolder = await this.CreateFolder(this.SelectedFolder, this.newFolderName.Trim());
            if (createdFolder is null)
                return;

            this.newFolderName = string.Empty;
            this.SelectedFolder = createdFolder.FullName;
            await this.SelectedFolderChanged.InvokeAsync(this.SelectedFolder);
        }
        finally
        {
            this.isCreatingFolder = false;
        }
    }

    /// <summary>
    /// Whether a folder can be picked, cf. the remarks of this component.
    /// </summary>
    internal static bool CanBeSelected(MailServerFolder folder) => folder.SpecialUse is not (MailFolderSpecialUse.TRASH or MailFolderSpecialUse.JUNK);

    /// <summary>
    /// Builds the tree of folders below the whole mailbox, with the way to the picked folder expanded.
    /// </summary>
    /// <remarks>
    /// A folder whose parent the server did not list goes to the top level, so that nothing listed
    /// gets lost. Each level starts with the inbox, and the other folders follow by name.
    /// </remarks>
    /// <param name="folders">The folders as the server lists them.</param>
    /// <param name="selectedFolder">The full name of the picked folder, or empty for the whole mailbox.</param>
    /// <param name="wholeMailboxText">The text of the root, which stands for the whole mailbox.</param>
    /// <returns>The root of the tree, as the only item.</returns>
    internal static List<TreeItemData<string>> BuildTree(IReadOnlyList<MailServerFolder> folders, string selectedFolder, string wholeMailboxText)
    {
        var foldersByName = IndexByName(folders);
        var childrenByParent = foldersByName.Values
            .ToLookup(folder => foldersByName.ContainsKey(folder.ParentFullName) && folder.ParentFullName != folder.FullName ? folder.ParentFullName : string.Empty, StringComparer.Ordinal);

        //
        // Every folder on the way to the picked one is expanded. Should a server list two folders as
        // each other's parent, the way ends where it meets itself:
        //
        var expandedFolders = new HashSet<string>(StringComparer.Ordinal);
        var current = selectedFolder;
        while (foldersByName.TryGetValue(current, out var folder) && expandedFolders.Add(folder.ParentFullName))
            current = folder.ParentFullName;

        var topLevelFolders = BuildChildren(string.Empty);
        return
        [
            new TreeItemData<string>
            {
                Value = string.Empty,
                Text = wholeMailboxText,
                Icon = Icons.Material.Filled.Mail,
                Expanded = true,
                Expandable = topLevelFolders.Count > 0,
                Children = topLevelFolders,
            },
        ];

        List<TreeItemData<string>> BuildChildren(string parentFullName) => childrenByParent[parentFullName]
            .OrderByDescending(folder => folder.IsInbox)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(folder => folder.FullName, StringComparer.Ordinal)
            .Select(folder =>
            {
                var children = BuildChildren(folder.FullName);
                return new TreeItemData<string>
                {
                    Value = folder.FullName,
                    Text = folder.Name,
                    Icon = GetIcon(folder),
                    Expanded = expandedFolders.Contains(folder.FullName),
                    Expandable = children.Count > 0,
                    Children = children,
                };
            })
            .ToList();
    }

    /// <remarks>
    /// The empty full name belongs to the whole mailbox, so a folder listed under it would become
    /// its own child.
    /// </remarks>
    private static Dictionary<string, MailServerFolder> IndexByName(IEnumerable<MailServerFolder> folders)
    {
        var foldersByName = new Dictionary<string, MailServerFolder>(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            if (folder.FullName.Length > 0)
                foldersByName.TryAdd(folder.FullName, folder);
        }

        return foldersByName;
    }

    private static string GetIcon(MailServerFolder folder)
    {
        if (folder.IsInbox)
            return Icons.Material.Filled.Inbox;

        return folder.SpecialUse switch
        {
            MailFolderSpecialUse.ALL => Icons.Material.Filled.AllInbox,
            MailFolderSpecialUse.ARCHIVE => Icons.Material.Filled.Archive,
            MailFolderSpecialUse.DRAFTS => Icons.Material.Filled.Drafts,
            MailFolderSpecialUse.FLAGGED => Icons.Material.Filled.Flag,
            MailFolderSpecialUse.IMPORTANT => Icons.Material.Filled.LabelImportant,
            MailFolderSpecialUse.JUNK => Icons.Material.Filled.Report,
            MailFolderSpecialUse.SENT => Icons.Material.Filled.Send,
            MailFolderSpecialUse.TRASH => Icons.Material.Filled.Delete,

            _ => Icons.Material.Filled.Folder,
        };
    }
}