using Lua;
using Lua.Standard;

// ReSharper disable MemberCanBePrivate.Global
namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents the base of any AI Studio plugin.
/// </summary>
public abstract class PluginBase
{
    private readonly IReadOnlyCollection<string> baseIssues;
    protected readonly LuaState state;

    protected List<string> pluginIssues = [];
    
    /// <summary>
    /// The type of this plugin.
    /// </summary>
    public PluginType Type { get; }
    
    /// <summary>
    /// The ID of this plugin.
    /// </summary>
    public Guid Id { get; }
    
    /// <summary>
    /// The name of this plugin.
    /// </summary>
    public string Name { get; } = string.Empty;
    
    /// <summary>
    /// The description of this plugin.
    /// </summary>
    public string Description { get; } = string.Empty;
    
    /// <summary>
    /// The version of this plugin.
    /// </summary>
    public PluginVersion Version { get; }

    /// <summary>
    /// The authors of this plugin.
    /// </summary>
    public string[] Authors { get; } = [];
    
    /// <summary>
    /// The support contact for this plugin.
    /// </summary>
    public string SupportContact { get; } = string.Empty;
    
    /// <summary>
    /// The source URL of this plugin.
    /// </summary>
    public string SourceURL { get; } = string.Empty;
    
    /// <summary>
    /// The categories of this plugin.
    /// </summary>
    public PluginCategory[] Categories { get; } = [];
    
    /// <summary>
    /// The target groups of this plugin.
    /// </summary>
    public PluginTargetGroup[] TargetGroups { get; } = [];

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

    protected PluginBase(LuaState state, PluginType type, string parseError = "")
    {
        this.state = state;
        this.Type = type;
     
        // For security reasons, we don't want to allow the plugin to load modules:
        this.state.ModuleLoader = new NoModuleLoader();
        
        // Add some useful libraries:
        this.state.OpenModuleLibrary();
        this.state.OpenStringLibrary();
        this.state.OpenTableLibrary();
        this.state.OpenMathLibrary();
        this.state.OpenBitwiseLibrary();
        this.state.OpenCoroutineLibrary();
        
        var issues = new List<string>();
        if(!string.IsNullOrWhiteSpace(parseError))
            issues.Add(parseError);
        
        if(this.TryInitId(out var issue, out var id))
            this.Id = id;
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
        
        this.baseIssues = issues;
    }

    #region Initialization-related methods

    /// <summary>
    /// Tries to read the ID of the plugin.
    /// </summary>
    /// <param name="message">The error message, when the ID could not be read.</param>
    /// <param name="id">The read ID.</param>
    /// <returns>True, when the ID could be read successfully.</returns>
    public bool TryInitId(out string message, out Guid id) 
    {
        if (!this.state.Environment["ID"].TryRead<string>(out var idText))
        {
            message = "The field ID does not exist or is not a valid string.";
            id = Guid.Empty;
            return false;
        }

        if (!Guid.TryParse(idText, out id))
        {
            message = "The field ID is not a valid GUID / UUID. The ID must be formatted in the 8-4-4-4-12 format (XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX).";
            id = Guid.Empty;
            return false;
        }
        
        if(id == Guid.Empty)
        {
            message = "The field ID is empty. The ID must be formatted in the 8-4-4-4-12 format (XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX).";
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
    public bool TryInitName(out string message, out string name)
    {
        if (!this.state.Environment["NAME"].TryRead(out name))
        {
            message = "The field NAME does not exist or is not a valid string.";
            name = string.Empty;
            return false;
        }
        
        if(string.IsNullOrWhiteSpace(name))
        {
            message = "The field NAME is empty. The name must be a non-empty string.";
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
    public bool TryInitDescription(out string message, out string description)
    {
        if (!this.state.Environment["DESCRIPTION"].TryRead(out description))
        {
            message = "The field DESCRIPTION does not exist or is not a valid string.";
            description = string.Empty;
            return false;
        }
        
        if(string.IsNullOrWhiteSpace(description))
        {
            message = "The field DESCRIPTION is empty. The description must be a non-empty string.";
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
    public bool TryInitVersion(out string message, out PluginVersion version)
    {
        if (!this.state.Environment["VERSION"].TryRead<string>(out var versionText))
        {
            message = "The field VERSION does not exist or is not a valid string.";
            version = PluginVersion.NONE;
            return false;
        }

        if (!PluginVersion.TryParse(versionText, out version))
        {
            message = "The field VERSION is not a valid version number. The version number must be formatted as string in the major.minor.patch format (X.X.X).";
            version = PluginVersion.NONE;
            return false;
        }
        
        if(version == PluginVersion.NONE)
        {
            message = "The field VERSION is empty. The version number must be formatted as string in the major.minor.patch format (X.X.X).";
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
    public bool TryInitAuthors(out string message, out string[] authors)
    {
        if (!this.state.Environment["AUTHORS"].TryRead<LuaTable>(out var authorsTable))
        {
            authors = [];
            message = "The table AUTHORS does not exist or is using an invalid syntax.";
            return false;
        }
        
        var authorList = new List<string>();
        foreach(var author in authorsTable.GetArraySpan())
            if(author.TryRead<string>(out var authorName))
                authorList.Add(authorName);
        
        authors = authorList.ToArray();
        if(authorList.Count == 0)
        {
            message = "The table AUTHORS is empty. At least one author must be specified.";
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
    public bool TryInitSupportContact(out string message, out string contact)
    {
        if (!this.state.Environment["SUPPORT_CONTACT"].TryRead(out contact))
        {
            contact = string.Empty;
            message = "The field SUPPORT_CONTACT does not exist or is not a valid string.";
            return false;
        }
        
        if(string.IsNullOrWhiteSpace(contact))
        {
            message = "The field SUPPORT_CONTACT is empty. The support contact must be a non-empty string.";
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
    public bool TryInitSourceURL(out string message, out string url)
    {
        if (!this.state.Environment["SOURCE_URL"].TryRead(out url))
        {
            url = string.Empty;
            message = "The field SOURCE_URL does not exist or is not a valid string.";
            return false;
        }

        if (!url.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
        {
            url = string.Empty;
            message = "The field SOURCE_URL is not a valid URL. The URL must start with 'http://' or 'https://'.";
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
    public bool TryInitCategories(out string message, out PluginCategory[] categories)
    {
        if (!this.state.Environment["CATEGORIES"].TryRead<LuaTable>(out var categoriesTable))
        {
            categories = [];
            message = "The table CATEGORIES does not exist or is using an invalid syntax.";
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
            message = $"The table CATEGORIES is empty. At least one category is necessary. Valid categories are: {CommonTools.GetAllEnumValues(PluginCategory.NONE)}.";
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
            message = "The table TARGET_GROUPS does not exist or is using an invalid syntax.";
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
            message = "The table TARGET_GROUPS is empty or is not a valid table of strings. Valid target groups are: {CommonTools.GetAllEnumValues(PluginTargetGroup.NONE)}.";
            return false;
        }
        
        message = string.Empty;
        return true;
    }

    #endregion
}