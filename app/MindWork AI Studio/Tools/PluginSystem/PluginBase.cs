using Lua;

// ReSharper disable MemberCanBePrivate.Global
namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents the base of any AI Studio plugin.
/// </summary>
public abstract class PluginBase
{
    private readonly string parseError;
    
    protected readonly LuaState state;
    protected readonly Guid pluginId;
    protected readonly string pluginName;
    protected readonly PluginType pluginType;
    protected readonly string pluginDescription;
    protected readonly PluginVersion pluginVersion;
    protected readonly string[] pluginAuthors;
    protected readonly string supportContact;
    protected readonly string sourceURL;
    protected readonly PluginCategory[] pluginCategories;
    protected readonly PluginTargetGroup[] pluginTargetGroups;
    
    private readonly bool isInitialized;
    private bool isValid;

    protected PluginBase(LuaState state, PluginType type, string parseError = "")
    {
        this.state = state;
        this.pluginType = type;
        this.pluginId = this.Id();
        this.pluginName = this.Name();
        this.pluginDescription = this.Description();
        this.pluginVersion = this.Version();
        this.pluginAuthors = this.Authors();
        this.supportContact = this.SupportContact();
        this.sourceURL = this.SourceURL();
        this.pluginCategories = this.Categories();
        this.pluginTargetGroups = this.TargetGroups();
        this.parseError = parseError;
        
        // For security reasons, we don't want to allow the plugin to load modules:
        this.state.ModuleLoader = new NoModuleLoader();
        
        //
        // Check if the plugin is valid:
        //
        if(!string.IsNullOrWhiteSpace(this.parseError))
            this.isValid = false;
        
        if(this is NoPlugin)
            this.isValid = false;
        
        this.isInitialized = true;
    }

    /// <summary>
    /// Checks if the plugin is valid.
    /// </summary>
    /// <returns>The state of the plugin, which may contain an error message.</returns>
    public PluginState IsValid()
    {
        if(!string.IsNullOrWhiteSpace(this.parseError))
        {
            this.isValid = false;
            return new(false, this.parseError);
        }

        if(this is NoPlugin)
        {
            this.isValid = false;
            return new(false, "Plugin is not valid.");
        }

        if(this.Id() == Guid.Empty)
        {
            this.isValid = false;
            return new(false, "The field ID does not exist, is empty, or is not a valid GUID / UUID. The ID must be formatted in the 8-4-4-4-12 format (XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX).");
        }
        
        if(string.IsNullOrWhiteSpace(this.Name()))
        {
            this.isValid = false;
            return new(false, "The field NAME does not exist, is empty, or is not a valid string.");
        }
        
        if(string.IsNullOrWhiteSpace(this.Description()))
        {
            this.isValid = false;
            return new(false, "The field DESCRIPTION does not exist, is empty, or is not a valid string.");
        }
        
        if(this.Version() == PluginVersion.NONE)
        {
            this.isValid = false;
            return new(false, "The field VERSION does not exist, is empty, or is not a valid version number. The version number must be formatted as string in the major.minor.patch format (X.X.X).");
        }
        
        if(this.pluginType == PluginType.NONE)
        {
            this.isValid = false;
            return new(false, $"The field TYPE does not exist, is empty, or is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues(PluginType.NONE)}.");
        }
        
        if(this.Authors().Length == 0)
        {
            this.isValid = false;
            return new(false, "The table AUTHORS does not exist, is empty, or is not a valid table of strings.");
        }
        
        if(string.IsNullOrWhiteSpace(this.SupportContact()))
        {
            this.isValid = false;
            return new(false, "The field SUPPORT_CONTACT does not exist, is empty, or is not a valid string.");
        }
        
        if(string.IsNullOrWhiteSpace(this.SourceURL()))
        {
            this.isValid = false;
            return new(false, "The field SOURCE_URL does not exist, is empty, or is not a valid string. Additional, it must start with 'http://' or 'https://'.");
        }
        
        if(this.Categories().Length == 0)
        {
            this.isValid = false;
            return new(false, $"The table CATEGORIES does not exist, is empty, or is not a valid table of strings. Valid categories are: {CommonTools.GetAllEnumValues(PluginCategory.NONE)}.");
        }
        
        if(this.TargetGroups().Length == 0)
        {
            this.isValid = false;
            return new(false, $"The table TARGET_GROUPS does not exist, is empty, or is not a valid table of strings. Valid target groups are: {CommonTools.GetAllEnumValues(PluginTargetGroup.NONE)}.");
        }

        this.isValid = true;
        return new(true, string.Empty);
    }

    /// <summary>
    /// Returns the intended target groups for the plugin.
    /// </summary>
    /// <returns>The target groups.</returns>
    public PluginTargetGroup[] TargetGroups()
    {
        if(this.isInitialized)
            return this.pluginTargetGroups;
        
        if(!this.isValid)
            return [];
        
        if (!this.state.Environment["TARGET_GROUPS"].TryRead<LuaTable>(out var targetGroups))
            return [];
        
        var targetGroupList = new List<PluginTargetGroup>();
        foreach(var luaTargetGroup in targetGroups.GetArraySpan())
            if(luaTargetGroup.TryRead<string>(out var targetGroupName))
                if(Enum.TryParse<PluginTargetGroup>(targetGroupName, out var targetGroup) && targetGroup != PluginTargetGroup.NONE)
                    targetGroupList.Add(targetGroup);
        
        return targetGroupList.ToArray();
    }

    /// <summary>
    /// Returns the plugin categories.
    /// </summary>
    /// <returns>The plugin categories.</returns>
    public PluginCategory[] Categories()
    {
        if(this.isInitialized)
            return this.pluginCategories;
        
        if(!this.isValid)
            return [];
        
        if (!this.state.Environment["CATEGORIES"].TryRead<LuaTable>(out var categories))
            return [];
        
        var categoryList = new List<PluginCategory>();
        foreach(var luaCategory in categories.GetArraySpan())
            if(luaCategory.TryRead<string>(out var categoryName))
                if(Enum.TryParse<PluginCategory>(categoryName, out var category) && category != PluginCategory.NONE)
                    categoryList.Add(category);
        
        return categoryList.ToArray();
    }
    
    /// <summary>
    /// Returns the source URL of the plugin.
    /// </summary>
    /// <returns>The source URL.</returns>
    public string SourceURL()
    {
        if(this.isInitialized)
            return this.sourceURL;
        
        if(!this.isValid)
            return string.Empty;
        
        if (!this.state.Environment["SOURCE_URL"].TryRead<string>(out var url))
            return string.Empty;
        
        if(!url.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
            return string.Empty;
        
        return url;
    }
    
    /// <summary>
    /// Returns the support contact of the plugin.
    /// </summary>
    /// <returns>The support contact.</returns>
    public string SupportContact()
    {
        if(this.isInitialized)
            return this.supportContact;
        
        if(!this.isValid)
            return string.Empty;
        
        if (!this.state.Environment["SUPPORT_CONTACT"].TryRead<string>(out var contact))
            return string.Empty;
        
        return contact;
    }
    
    /// <summary>
    /// Returns the ID of the plugin.
    /// </summary>
    /// <returns>The plugin ID.</returns>
    public Guid Id() 
    {
        if(this.isInitialized)
            return this.pluginId;
        
        if(!this.isValid)
            return Guid.Empty;
        
        if (!this.state.Environment["ID"].TryRead<string>(out var idText))
            return Guid.Empty;
        
        if (!Guid.TryParse(idText, out var id))
            return Guid.Empty;
        
        return id;
    }

    /// <summary>
    /// Returns the name of the plugin.
    /// </summary>
    /// <returns>The plugin name.</returns>
    public string Name()
    {
        if(this.isInitialized)
            return this.pluginName;
        
        if(!this.isValid)
            return string.Empty;
        
        if (!this.state.Environment["NAME"].TryRead<string>(out var name))
            return string.Empty;

        return name;
    }
    
    /// <summary>
    /// Returns the description of the plugin.
    /// </summary>
    /// <returns>The plugin description.</returns>
    public string Description()
    {
        if(this.isInitialized)
            return this.pluginDescription;
        
        if(!this.isValid)
            return string.Empty;
        
        if (!this.state.Environment["DESCRIPTION"].TryRead<string>(out var description))
            return string.Empty;
        
        return description;
    }
    
    /// <summary>
    /// Returns the version of the plugin.
    /// </summary>
    /// <returns>The plugin version.</returns>
    public PluginVersion Version()
    {
        if(this.isInitialized)
            return this.pluginVersion;
        
        if(!this.isValid)
            return PluginVersion.NONE;
        
        if (!this.state.Environment["VERSION"].TryRead<string>(out var versionText))
            return PluginVersion.NONE;
        
        if (!PluginVersion.TryParse(versionText, out var version))
            return PluginVersion.NONE;
        
        return version;
    }
    
    /// <summary>
    /// Returns the authors of the plugin.
    /// </summary>
    /// <returns>The plugin authors.</returns>
    public string[] Authors()
    {
        if(this.isInitialized)
            return this.pluginAuthors;

        if (!this.isValid)
            return [];
        
        if (!this.state.Environment["AUTHORS"].TryRead<LuaTable>(out var authors))
            return [];
        
        var authorList = new List<string>();
        foreach(var author in authors.GetArraySpan())
            if(author.TryRead<string>(out var authorName))
                authorList.Add(authorName);
        
        return authorList.ToArray();
    }
}