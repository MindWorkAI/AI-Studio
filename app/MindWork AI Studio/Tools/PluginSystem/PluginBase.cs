using Lua;

// ReSharper disable MemberCanBePrivate.Global
namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents the base of any AI Studio plugin.
/// </summary>
public abstract partial class PluginBase : IPluginMetadata
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginBase).Namespace, nameof(PluginBase));
    
    private readonly IReadOnlyCollection<string> baseIssues;
    protected readonly LuaState state;

    protected readonly List<string> pluginIssues = [];

    /// <inheritdoc />
    public string IconSVG { get; }
    
    /// <inheritdoc />
    public PluginType Type { get; }
    
    /// <inheritdoc />
    public Guid Id { get; }
    
    /// <inheritdoc />
    public string Name { get; } = string.Empty;
    
    /// <inheritdoc />
    public string Description { get; } = string.Empty;
    
    /// <inheritdoc />
    public PluginVersion Version { get; }

    /// <inheritdoc />
    public string[] Authors { get; } = [];
    
    /// <inheritdoc />
    public string SupportContact { get; } = string.Empty;
    
    /// <inheritdoc />
    public string SourceURL { get; } = string.Empty;
    
    /// <inheritdoc />
    public PluginCategory[] Categories { get; } = [];
    
    /// <inheritdoc />
    public PluginTargetGroup[] TargetGroups { get; } = [];
    
    /// <inheritdoc />
    public bool IsMaintained { get; }
    
    /// <inheritdoc />
    public string DeprecationMessage { get; } = string.Empty;
    
    /// <inheritdoc />
    public bool IsInternal { get; }
    
    /// <summary>
    /// The issues that occurred during the initialization of this plugin.
    /// </summary>
    public IEnumerable<string> Issues => this.baseIssues.Concat(this.pluginIssues);
    
    /// <summary>
    /// True, when the plugin is valid.
    /// </summary>
    /// <remarks>
    /// False means that there were issues during the initialization of the plugin.
    /// Please check the Issues property for more information.
    /// </remarks>
    public bool IsValid => this is not NoPlugin && this.baseIssues.Count == 0 && this.pluginIssues.Count == 0;

    protected PluginBase(bool isInternal, LuaState state, PluginType type, string parseError = "")
    {
        this.state = state;
        this.Type = type;
        
        var issues = new List<string>();
        if(!string.IsNullOrWhiteSpace(parseError))
            issues.Add(parseError);

        // Notice: when no icon is specified, the default icon will be used.
        this.TryInitIconSVG(out _, out var iconSVG);
        this.IconSVG = iconSVG;
        
        if(this.TryInitId(out var issue, out var id))
        {
            this.Id = id;
            this.IsInternal = isInternal;
        }
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitName(out issue, out var name))
            this.Name = name;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitDescription(out issue, out var description))
            this.Description = description;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitVersion(out issue, out var version))
            this.Version = version;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitAuthors(out issue, out var authors))
            this.Authors = authors;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitSupportContact(out issue, out var contact))
            this.SupportContact = contact;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitSourceURL(out issue, out var url))
            this.SourceURL = url;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitCategories(out issue, out var categories))
            this.Categories = categories;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitTargetGroups(out issue, out var targetGroups))
            this.TargetGroups = targetGroups;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitIsMaintained(out issue, out var isMaintained))
            this.IsMaintained = isMaintained;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        if(this.TryInitDeprecationMessage(out issue, out var deprecationMessage))
            this.DeprecationMessage = deprecationMessage;
        else if(this is not NoPlugin)
            issues.Add(issue);
        
        this.baseIssues = issues;
    }

    #region Initialization-related methods

    /// <summary>
    /// Tries to read the ID of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the ID could not be read.</param>
    /// <param name="id">The read ID.</param>
    /// <returns>True, when the ID could be read successfully.</returns>
    private bool TryInitId(out string message, out Guid id) 
    {
        if (!this.state.Environment["ID"].TryRead<string>(out var idText))
        {
            message = TB("The field ID does not exist or is not a valid string.");
            id = Guid.Empty;
            return false;
        }

        if (!Guid.TryParse(idText, out id))
        {
            message = TB("The field ID is not a valid GUID / UUID. The ID must be formatted in the 8-4-4-4-12 format (XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX).");
            id = Guid.Empty;
            return false;
        }
        
        if(id == Guid.Empty)
        {
            message = TB("The field ID is empty. The ID must be formatted in the 8-4-4-4-12 format (XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX).");
            return false;
        }

        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the name of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the name could not be read.</param>
    /// <param name="name">The read name.</param>
    /// <returns>True, when the name could be read successfully.</returns>
    private bool TryInitName(out string message, out string name)
    {
        if (!this.state.Environment["NAME"].TryRead(out name))
        {
            message = TB("The field NAME does not exist or is not a valid string.");
            name = string.Empty;
            return false;
        }
        
        if(string.IsNullOrWhiteSpace(name))
        {
            message = TB("The field NAME is empty. The name must be a non-empty string.");
            return false;
        }

        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the description of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the description could not be read.</param>
    /// <param name="description">The read description.</param>
    /// <returns>True, when the description could be read successfully.</returns>
    private bool TryInitDescription(out string message, out string description)
    {
        if (!this.state.Environment["DESCRIPTION"].TryRead(out description))
        {
            message = TB("The field DESCRIPTION does not exist or is not a valid string.");
            description = string.Empty;
            return false;
        }
        
        if(string.IsNullOrWhiteSpace(description))
        {
            message = TB("The field DESCRIPTION is empty. The description must be a non-empty string.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the version of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the version could not be read.</param>
    /// <param name="version">The read version.</param>
    /// <returns>True, when the version could be read successfully.</returns>
    private bool TryInitVersion(out string message, out PluginVersion version)
    {
        if (!this.state.Environment["VERSION"].TryRead<string>(out var versionText))
        {
            message = TB("The field VERSION does not exist or is not a valid string.");
            version = PluginVersion.NONE;
            return false;
        }

        if (!PluginVersion.TryParse(versionText, out version))
        {
            message = TB("The field VERSION is not a valid version number. The version number must be formatted as string in the major.minor.patch format (X.X.X).");
            version = PluginVersion.NONE;
            return false;
        }
        
        if(version == PluginVersion.NONE)
        {
            message = TB("The field VERSION is empty. The version number must be formatted as string in the major.minor.patch format (X.X.X).");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the authors of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the authors could not be read.</param>
    /// <param name="authors">The read authors.</param>
    /// <returns>True, when the authors could be read successfully.</returns>
    private bool TryInitAuthors(out string message, out string[] authors)
    {
        if (!this.state.Environment["AUTHORS"].TryRead<LuaTable>(out var authorsTable))
        {
            authors = [];
            message = TB("The table AUTHORS does not exist or is using an invalid syntax.");
            return false;
        }
        
        var authorList = new List<string>();
        foreach(var author in authorsTable.GetArraySpan())
            if(author.TryRead<string>(out var authorName))
                authorList.Add(authorName);
        
        authors = authorList.ToArray();
        if(authorList.Count == 0)
        {
            message = TB("The table AUTHORS is empty. At least one author must be specified.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the support contact for the plugin.
    /// </summary>
    /// <param name="message">The error message, when the support contact could not be read.</param>
    /// <param name="contact">The read support contact.</param>
    /// <returns>True, when the support contact could be read successfully.</returns>
    private bool TryInitSupportContact(out string message, out string contact)
    {
        if (!this.state.Environment["SUPPORT_CONTACT"].TryRead(out contact))
        {
            contact = string.Empty;
            message = TB("The field SUPPORT_CONTACT does not exist or is not a valid string.");
            return false;
        }
        
        if(string.IsNullOrWhiteSpace(contact))
        {
            message = TB("The field SUPPORT_CONTACT is empty. The support contact must be a non-empty string.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Try to read the source URL of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the source URL could not be read.</param>
    /// <param name="url">The read source URL.</param>
    /// <returns>True, when the source URL could be read successfully.</returns>
    private bool TryInitSourceURL(out string message, out string url)
    {
        if (!this.state.Environment["SOURCE_URL"].TryRead(out url))
        {
            url = string.Empty;
            message = TB("The field SOURCE_URL does not exist or is not a valid string.");
            return false;
        }

        if (!url.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
        {
            url = string.Empty;
            message = TB("The field SOURCE_URL is not a valid URL. The URL must start with 'http://' or 'https://'.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the categories of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the categories could not be read.</param>
    /// <param name="categories">The read categories.</param>
    /// <returns>True, when the categories could be read successfully.</returns>
    private bool TryInitCategories(out string message, out PluginCategory[] categories)
    {
        if (!this.state.Environment["CATEGORIES"].TryRead<LuaTable>(out var categoriesTable))
        {
            categories = [];
            message = TB("The table CATEGORIES does not exist or is using an invalid syntax.");
            return false;
        }
        
        var categoryList = new List<PluginCategory>();
        foreach(var luaCategory in categoriesTable.GetArraySpan())
            if(luaCategory.TryRead<string>(out var categoryName))
                if(Enum.TryParse<PluginCategory>(categoryName, out var category) && category != PluginCategory.NONE)
                    categoryList.Add(category);
        
        categories = categoryList.ToArray();
        if(categoryList.Count == 0)
        {
            message = string.Format(TB("The table CATEGORIES is empty. At least one category is necessary. Valid categories are: {0}."), CommonTools.GetAllEnumValues(PluginCategory.NONE));
            return false;
        }
        
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries to read the intended target groups for the plugin.
    /// </summary>
    /// <param name="message">The error message, when the target groups could not be read.</param>
    /// <param name="targetGroups">The read target groups.</param>
    /// <returns>True, when the target groups could be read successfully.</returns>
    private bool TryInitTargetGroups(out string message, out PluginTargetGroup[] targetGroups)
    {
        if (!this.state.Environment["TARGET_GROUPS"].TryRead<LuaTable>(out var targetGroupsTable))
        {
            targetGroups = [];
            message = TB("The table TARGET_GROUPS does not exist or is using an invalid syntax.");
            return false;
        }

        var targetGroupList = new List<PluginTargetGroup>();
        foreach(var luaTargetGroup in targetGroupsTable.GetArraySpan())
            if(luaTargetGroup.TryRead<string>(out var targetGroupName))
                if(Enum.TryParse<PluginTargetGroup>(targetGroupName, out var targetGroup) && targetGroup != PluginTargetGroup.NONE)
                    targetGroupList.Add(targetGroup);
        
        targetGroups = targetGroupList.ToArray();
        if(targetGroups.Length == 0)
        {
            message = string.Format(TB("The table TARGET_GROUPS is empty or is not a valid table of strings. Valid target groups are: {0}."), CommonTools.GetAllEnumValues(PluginTargetGroup.NONE));
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the maintenance status of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the maintenance status could not be read.</param>
    /// <param name="isMaintained">The read maintenance status.</param>
    /// <returns>True, when the maintenance status could be read successfully.</returns>
    private bool TryInitIsMaintained(out string message, out bool isMaintained)
    {
        if (!this.state.Environment["IS_MAINTAINED"].TryRead(out isMaintained))
        {
            isMaintained = false;
            message = TB("The field IS_MAINTAINED does not exist or is not a valid boolean.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Tries to read the deprecation message of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the deprecation message could not be read.</param>
    /// <param name="deprecationMessage">The read deprecation message.</param>
    /// <returns>True, when the deprecation message could be read successfully.</returns>
    private bool TryInitDeprecationMessage(out string message, out string deprecationMessage)
    {
        if (!this.state.Environment["DEPRECATION_MESSAGE"].TryRead(out deprecationMessage))
        {
            deprecationMessage = string.Empty;
            message = TB("The field DEPRECATION_MESSAGE does not exist, is not a valid string. This message is optional: use an empty string to indicate that the plugin is not deprecated.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries to initialize the UI text content of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the UI text content could not be read.</param>
    /// <param name="pluginContent">The read UI text content.</param>
    /// <returns>True, when the UI text content could be read successfully.</returns>
    protected bool TryInitUITextContent(out string message, out Dictionary<string, string> pluginContent)
    {
        if (!this.state.Environment["UI_TEXT_CONTENT"].TryRead<LuaTable>(out var textTable))
        {
            message = TB("The UI_TEXT_CONTENT table does not exist or is not a valid table.");
            pluginContent = [];
            return false;
        }

        this.ReadTextTable("root", textTable, out pluginContent);
        
        message = string.Empty;
        return true;
    }
    
    /// <summary>
    /// Reads a flat or hierarchical text table.
    /// </summary>
    /// <param name="parent">The parent key(s).</param>
    /// <param name="table">The table to read.</param>
    /// <param name="tableContent">The read table content.</param>
    protected void ReadTextTable(string parent, LuaTable table, out Dictionary<string, string> tableContent)
    {
        tableContent = [];
        var lastKey = LuaValue.Nil;
        while (table.TryGetNext(lastKey, out var pair))
        {
            var keyText = pair.Key.ToString();
            if (pair.Value.TryRead<string>(out var value))
                tableContent[$"{parent}::{keyText}"] = value;
            
            else if (pair.Value.TryRead<LuaTable>(out var t))
            {
                this.ReadTextTable($"{parent}::{keyText}", t, out var subContent);
                foreach (var (k, v) in subContent)
                    tableContent[k] = v;
            }

            lastKey = pair.Key;
        }
    }

    #endregion
}