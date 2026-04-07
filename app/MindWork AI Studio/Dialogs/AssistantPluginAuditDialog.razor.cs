using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class AssistantPluginAuditDialog : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantPluginAuditDialog).Namespace, nameof(AssistantPluginAuditDialog));

    [CascadingParameter] 
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private AssistantPluginAuditService AssistantPluginAuditService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Parameter] public Guid PluginId { get; set; }

    private PluginAssistants? plugin;
    private PluginAssistantAudit? audit;
    private string promptPreview = string.Empty;
    private string promptFallbackPreview = string.Empty;
    private string componentSummary = string.Empty;
    private ImmutableDictionary<string, string> luaFiles = ImmutableDictionary.Create<string, string>();
    private IReadOnlyCollection<TreeItemData<ITreeItem>> componentTreeItems = [];
    private IReadOnlyCollection<TreeItemData<ITreeItem>> fileSystemTreeItems = [];
    private CultureInfo currentCultureInfo = CultureInfo.InvariantCulture;
    private bool isAuditing;

    private AIStudio.Settings.Provider CurrentProvider => this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, null, true);

    private string ProviderLabel => this.CurrentProvider == AIStudio.Settings.Provider.NONE
        ? this.T("No provider configured")
        : $"{this.CurrentProvider.InstanceName} ({this.CurrentProvider.UsedLLMProvider.ToName()})";

    private DataAssistantPluginAudit AuditSettings => this.SettingsManager.ConfigurationData.AssistantPluginAudit;

    private AssistantAuditLevel MinimumLevel => this.SettingsManager.ConfigurationData.AssistantPluginAudit.MinimumLevel;

    private string MinimumLevelLabel => this.MinimumLevel.GetName();

    private bool CanRunAudit => this.plugin is not null && this.CurrentProvider != AIStudio.Settings.Provider.NONE && !this.isAuditing;

    private bool IsAuditBelowMinimum => this.audit is not null && this.audit.Level < this.MinimumLevel;

    private bool IsActivationBlockedBySettings => this.audit is null || this.IsAuditBelowMinimum && this.AuditSettings.BlockActivationBelowMinimum;

    private bool RequiresActivationConfirmation => this.audit is not null && this.IsAuditBelowMinimum && !this.AuditSettings.BlockActivationBelowMinimum;

    private bool CanEnablePlugin => this.audit is not null && !this.isAuditing && !this.IsActivationBlockedBySettings;

    private Color EnableButtonColor => this.RequiresActivationConfirmation ? Color.Warning : Color.Success;
    private bool justAudited;

    private const ushort BYTES_PER_KILOBYTE = 1024;

    protected override async Task OnInitializedAsync()
    {
        var activeLanguagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        this.currentCultureInfo = CommonTools.DeriveActiveCultureOrInvariant(activeLanguagePlugin.IETFTag);

        this.plugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>()
            .FirstOrDefault(x => x.Id == this.PluginId);
        if (this.plugin is not null)
        {
            this.promptPreview = await this.plugin.BuildAuditPromptPreviewAsync();
            this.promptFallbackPreview = this.plugin.BuildAuditPromptFallbackPreview();
            this.componentSummary = this.plugin.CreateAuditComponentSummary();
            this.componentTreeItems = this.CreateAuditTreeItems(this.plugin.RootComponent);
            this.fileSystemTreeItems = this.CreatePluginFileSystemTreeItems(this.plugin.PluginPath);
            this.luaFiles = this.plugin.ReadAllLuaFiles();
        }

        await base.OnInitializedAsync();
    }

    private async Task RunAudit()
    {
        if (this.plugin is null || this.isAuditing)
            return;

        this.isAuditing = true;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            this.audit = await this.AssistantPluginAuditService.RunAuditAsync(this.plugin);
        }
        finally
        {
            this.isAuditing = false;
            this.justAudited = true;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void CloseWithoutActivation()
    {
        if (this.audit is null)
        {
            this.MudDialog.Cancel();
            return;
        }

        this.MudDialog.Close(DialogResult.Ok(new AssistantPluginAuditDialogResult(this.audit, false)));
    }

    private async Task EnablePlugin()
    {
        if (this.audit is null)
            return;

        if (this.IsActivationBlockedBySettings)
            return;

        if (this.RequiresActivationConfirmation && !await this.ConfirmActivationBelowMinimumAsync())
            return;

        this.MudDialog.Close(DialogResult.Ok(new AssistantPluginAuditDialogResult(this.audit, true)));
    }

    private async Task<bool> ConfirmActivationBelowMinimumAsync()
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Message,
                string.Format(
                    T("The assistant plugin \"{0}\" was audited with the level \"{1}\", which is below the required safety level \"{2}\". Your current settings still allow activation, but this may be unsafe. Do you really want to enable this plugin?"),
                    this.plugin?.Name ?? T("Unknown plugin"),
                    this.audit?.Level.GetName() ?? T("Unknown"),
                    this.MinimumLevelLabel)
            },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Potentially Dangerous Plugin"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is not null && !dialogResult.Canceled;
    }

    private Severity GetAuditResultSeverity() => this.audit?.Level switch
    {
        AssistantAuditLevel.DANGEROUS => Severity.Error,
        AssistantAuditLevel.CAUTION => Severity.Warning,
        AssistantAuditLevel.SAFE => Severity.Success,
        _ => Severity.Normal,
    };

    /// <summary>
    /// Creates the full audit tree for the assistant component hierarchy.
    /// The dialog owns this mapping because it is pure presentation logic for the audit UI.
    /// </summary>
    private IReadOnlyCollection<TreeItemData<ITreeItem>> CreateAuditTreeItems(IAssistantComponent? rootComponent)
    {
        if (rootComponent is null)
            return [];

        return [this.CreateComponentTreeItem(rootComponent, index: 0, depth: 0)];
    }

    /// <summary>
    /// Maps one assistant component into a tree node and recursively appends its value, props and child components.
    /// </summary>
    private TreeItemData<ITreeItem> CreateComponentTreeItem(IAssistantComponent component, int index, int depth)
    {
        var children = new List<TreeItemData<ITreeItem>>();

        if (component.Props.TryGetValue("Value", out var value))
            children.Add(this.CreateValueTreeItem(TB("Value"), value, depth + 1));

        if (component.Props.Count > 0)
            children.Add(this.CreatePropsTreeItem(component.Props, depth + 1));

        children.AddRange(component.Children.Select((child, childIndex) =>
            this.CreateComponentTreeItem(child, childIndex, depth + 1)));

        return new TreeItemData<ITreeItem>
        {
            Expanded = depth < 2,
            Expandable = children.Count > 0,
            Value = new AssistantAuditTreeItem
            {
                Text = this.GetComponentTreeItemText(component),
                Caption = this.GetComponentTreeItemCaption(component, index),
                Icon = component.Type.GetIcon(),
                Expandable = children.Count > 0,
            },
            Children = children,
        };
    }

    /// <summary>
    /// Groups all props of a component under a single "Props" branch to keep the component nodes compact.
    /// </summary>
    private TreeItemData<ITreeItem> CreatePropsTreeItem(IReadOnlyDictionary<string, object> props, int depth)
    {
        var children = props
            .OrderBy(prop => prop.Key, StringComparer.Ordinal)
            .Select(prop => this.CreateValueTreeItem(prop.Key, prop.Value, depth + 1))
            .ToList();

        return new TreeItemData<ITreeItem>
        {
            Expanded = depth < 2,
            Expandable = children.Count > 0,
            Value = new AssistantAuditTreeItem
            {
                Text = TB("Properties"),
                Caption = string.Format(TB("Count: {0}"), props.Count),
                Icon = Icons.Material.Filled.Code,
                Expandable = children.Count > 0,
                IsComponent = false,
            },
            Children = children,
        };
    }

    /// <summary>
    /// Converts a scalar or structured prop value into a tree node.
    /// Scalars stay on one line, while structured values recursively expose their children.
    /// </summary>
    private TreeItemData<ITreeItem> CreateValueTreeItem(string label, object? value, int depth)
    {
        var children = this.CreateValueChildren(value, depth + 1);
        return new TreeItemData<ITreeItem>
        {
            Expanded = depth < 2,
            Expandable = children.Count > 0,
            Value = new AssistantAuditTreeItem
            {
                Text = label,
                Caption = children.Count == 0 ? this.FormatScalarValue(value) : this.GetStructuredValueCaption(value),
                Icon = this.GetValueIcon(value),
                Expandable = children.Count > 0,
                IsComponent = false,
            },
            Children = children,
        };
    }

    /// <summary>
    /// Recursively expands structured values for the tree.
    /// Lists, dictionaries and known DTO-style assistant values become nested tree branches.
    /// </summary>
    private List<TreeItemData<ITreeItem>> CreateValueChildren(object? value, int depth)
    {
        if (value is null || IsScalarValue(value))
            return [];

        if (value is IDictionary dictionary)
            return this.CreateDictionaryChildren(dictionary, depth);

        if (value is IEnumerable enumerable && value is not string)
            return this.CreateEnumerableChildren(enumerable, depth);

        return this.CreateObjectChildren(value, depth);
    }

    private List<TreeItemData<ITreeItem>> CreateDictionaryChildren(IDictionary dictionary, int depth)
    {
        var children = new List<TreeItemData<ITreeItem>>();
        foreach (DictionaryEntry entry in dictionary)
        {
            var keyText = entry.Key.ToString() ?? TB("Unknown key");
            children.Add(this.CreateValueTreeItem(keyText, entry.Value, depth));
        }

        return children;
    }

    /// <summary>
    /// Creates a tree for the plugin directory so the audit can show unexpected folders and files, while excluding irrelevant dependency folders.
    /// </summary>
    private IReadOnlyCollection<TreeItemData<ITreeItem>> CreatePluginFileSystemTreeItems(string pluginPath)
    {
        if (string.IsNullOrWhiteSpace(pluginPath) || !Directory.Exists(pluginPath))
            return [];

        return [this.CreateDirectoryTreeItem(pluginPath, pluginPath, depth: 0)];
    }

    private TreeItemData<ITreeItem> CreateDirectoryTreeItem(string directoryPath, string rootPath, int depth)
    {
        var childDirectories = Directory.EnumerateDirectories(directoryPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => this.CreateDirectoryTreeItem(path, rootPath, depth + 1))
            .ToList();

        var childFiles = Directory.EnumerateFiles(directoryPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => this.CreateFileTreeItem(path, depth + 1))
            .ToList();

        var children = new List<TreeItemData<ITreeItem>>(childDirectories.Count + childFiles.Count);
        children.AddRange(childDirectories);
        children.AddRange(childFiles);

        var relativePath = Path.GetRelativePath(rootPath, directoryPath);
        var displayName = depth == 0
            ? Path.GetFileName(directoryPath)
            : relativePath.Split(Path.DirectorySeparatorChar).Last();

        return new TreeItemData<ITreeItem>
        {
            Expanded = depth < 2,
            Expandable = children.Count > 0,
            Value = new AssistantAuditTreeItem
            {
                Text = string.IsNullOrWhiteSpace(displayName) ? directoryPath : displayName,
                Caption = depth == 0 ? TB("Plugin root") : string.Format(TB("Items: {0}"), children.Count),
                Icon = children.Count > 0 ? Icons.Material.Filled.FolderCopy : Icons.Material.Filled.Folder,
                Expandable = children.Count > 0,
                IsComponent = false,
            },
            Children = children,
        };
    }

    private TreeItemData<ITreeItem> CreateFileTreeItem(string filePath, int depth)
    {
        var fileInfo = new FileInfo(filePath);

        return new TreeItemData<ITreeItem>
        {
            Expanded = depth < 2,
            Expandable = false,
            Value = new AssistantAuditTreeItem
            {
                Text = Path.GetFileName(filePath),
                Caption = string.Empty,
                Icon = this.GetFileIcon(filePath),
                Expandable = false,
                IsComponent = false,
            },
        };
    }

    private string GetFileIcon(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.ToLowerInvariant() switch
        {
            ".lua" => Icons.Material.Filled.Code,
            ".md" => Icons.Material.Filled.Article,
            ".json" => Icons.Material.Filled.DataObject,
            ".png" or ".jpg" or ".jpeg" or ".svg" or ".webp" => Icons.Material.Filled.Image,
            _ => Icons.Material.Filled.InsertDriveFile,
        };
    }

    private List<TreeItemData<ITreeItem>> CreateEnumerableChildren(IEnumerable enumerable, int depth)
    {
        var children = new List<TreeItemData<ITreeItem>>();
        var index = 0;

        foreach (var item in enumerable)
        {
            children.Add(this.CreateValueTreeItem($"[{index}]", item, depth));
            index++;
        }

        return children;
    }

    /// <summary>
    /// Falls back to public instance properties for simple DTO-style values such as dropdown items.
    /// Getter failures are treated defensively so the audit dialog never crashes because of a problematic property.
    /// </summary>
    private List<TreeItemData<ITreeItem>> CreateObjectChildren(object value, int depth)
    {
        var children = new List<TreeItemData<ITreeItem>>();

        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
                continue;

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (Exception)
            {
                propertyValue = TB("Unavailable");
            }

            children.Add(this.CreateValueTreeItem(property.Name, propertyValue, depth));
        }

        return children;
    }

    private string GetComponentTreeItemText(IAssistantComponent component)
    {
        var type = component.Type.GetDisplayName();
        if (component is INamedAssistantComponent named && !string.IsNullOrWhiteSpace(named.Name))
            return $"{type}: {named.Name}";

        return type;
    }

    private string GetComponentTreeItemCaption(IAssistantComponent component, int index)
    {
        var details = new List<string> { $"#{index + 1}" };

        if (component is IStatefulAssistantComponent stateful)
            details.Add(string.IsNullOrWhiteSpace(stateful.UserPrompt) ? TB("Prompt: empty") : TB("Prompt: set"));

        if (component.Children.Count > 0)
            details.Add(string.Format(TB("Children: {0}"), component.Children.Count));

        return string.Join(" | ", details);
    }

    private static bool IsScalarValue(object value)
    {
        return value is string or bool or char or Enum
            or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal
            or DateTime or DateTimeOffset or TimeSpan or Guid;
    }

    private string FormatScalarValue(object? value) => value switch
    {
        null => TB("null"),
        string stringValue when string.IsNullOrWhiteSpace(stringValue) => TB("empty"),
        string stringValue => stringValue,
        bool boolValue => boolValue ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private string GetStructuredValueCaption(object? value) => value switch
    {
        null => TB("null"),
        IDictionary dictionary => string.Format(TB("Entries: {0}"), dictionary.Count),
        IEnumerable enumerable when value is not string => string.Format(TB("Items: {0}"),
            enumerable.Cast<object?>().Count()),
        _ => value.GetType().Name,
    };

    private string GetValueIcon(object? value) => value switch
    {
        null => Icons.Material.Filled.Block,
        bool => Icons.Material.Outlined.ToggleOn,
        string => Icons.Material.Outlined.Abc,
        int => Icons.Material.Filled.Numbers,
        Enum => Icons.Material.Filled.Label,
        IDictionary => Icons.Material.Filled.DataObject,
        IEnumerable when value is not string => Icons.Material.Filled.FormatListBulleted,
        _ => Icons.Material.Filled.DataArray,
    };

    private string FormatFileTimestamp(DateTime timestamp) => CommonTools.FormatTimestampToGeneral(timestamp, this.currentCultureInfo);

    private string FormatFileSize(long bytes)
    {
        if (bytes < BYTES_PER_KILOBYTE)
            return string.Format(this.currentCultureInfo, TB("{0} B"), bytes);

        var kilobyte = bytes / (double)BYTES_PER_KILOBYTE;
        if (kilobyte < BYTES_PER_KILOBYTE)
            return string.Format(this.currentCultureInfo, TB("{0:0.##} KB"), kilobyte);

        var megabyte = kilobyte / BYTES_PER_KILOBYTE;
        if (megabyte < BYTES_PER_KILOBYTE)
            return string.Format(this.currentCultureInfo, TB("{0:0.##} MB"), megabyte);

        var gigabyte = megabyte / BYTES_PER_KILOBYTE;
        return string.Format(this.currentCultureInfo, TB("{0:0.##} GB"), gigabyte);
    }
}
