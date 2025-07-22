using System.Xml.XPath;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants;

public sealed class PluginAssistants : PluginBase
{
    private static string TB(string fallbackEN) =>
        I18N.I.T(fallbackEN, typeof(PluginAssistants).Namespace, nameof(PluginAssistants));

    private static readonly ILogger<PluginAssistants> LOGGER = Program.LOGGER_FACTORY.CreateLogger<PluginAssistants>();

    public AssistantForm RootComponent { get; set; }
    public string AssistantTitle { get; set; } = string.Empty;
    private string AssistantDescription { get; set; } = string.Empty;

    public PluginAssistants(bool isInternal, LuaState state, PluginType type) : base(isInternal, state, type)
    {
    }

    /// <summary>
    /// Tries to parse the assistant table into our internal assistant render tree data model. It follows this process:
    /// <list type="number">
    /// <item><description>ASSISTANT → Title/Description → UI</description></item>
    /// <item><description>UI: Root element → required Children → Components</description></item>
    /// <item><description>Components: Type → Props → Children (recursively)</description></item>
    /// </list>
    /// </summary>
    /// <param name="message">The error message, when parameters from the table could not be read.</param>
    /// <returns>True, when the assistant could be read successfully indicating the data model is populated.</returns>
    private bool TryProcessAssistant(out string message)
    {
        message = string.Empty;
        
        // Ensure that the main ASSISTANT table exists and is a valid Lua table:
        if (!this.state.Environment["ASSISTANT"].TryRead<LuaTable>(out var assistantTable))
        {
            message = TB("The ASSISTANT table does not exist or is not a valid table.");
            return false;
        }
        
        if (!assistantTable.TryGetValue("Title", out var assistantTitleValue) ||
            !assistantTitleValue.TryRead<string>(out var assistantTitle))
        {
            message = TB("The ASSISTANT table does not contain a valid title.");
            return false;
        }

        if (!assistantTable.TryGetValue("Description", out var assistantDescriptionValue) ||
            !assistantDescriptionValue.TryRead<string>(out var assistantDescription))
        {
            message = TB("The ASSISTANT table does not contain a valid description.");
            return false;
        }

        this.AssistantTitle = assistantTitle;
        this.AssistantDescription = assistantDescription;

        // Ensure that the UI table exists nested in the ASSISTANT table and is a valid Lua table:
        if (!assistantTable.TryGetValue("UI", out var uiVal) || !uiVal.TryRead<LuaTable>(out var uiTable))
        {
            message = TB("The ASSISTANT table does not contain a valid UI section.");
            return false;
        }
        
        if (!this.TryReadRenderTree(uiTable, out var rootComponent))
        {
            message = TB("Failed to parse the UI render tree.");
            return false;
        }

        this.RootComponent = (AssistantForm)rootComponent;
        return true;
    }

    /// <summary>
    /// Parses the root <c>FORM</c> component and start to parse its required children (main ui components)
    /// </summary>
    /// <param name="uiTable">The <c>LuaTable</c> containing all UI components</param>
    /// <param name="root">Outputs the root <c>FORM</c> component, if the parsing is successful. </param>
    /// <returns>True, when the UI table could be read successfully.</returns>
    private bool TryReadRenderTree(LuaTable uiTable, out IAssistantComponent root)
    {
        root = null!;

        if (!uiTable.TryGetValue("Type", out var typeVal)
            || !typeVal.TryRead<string>(out var typeText)
            || !Enum.TryParse<AssistantUiCompontentType>(typeText, true, out var type)
            || type != AssistantUiCompontentType.FORM)
        {
            LOGGER.LogWarning("UI table of the ASSISTANT table has no valid Form type.");
            return false;
        }

        if (!uiTable.TryGetValue("Children", out var childrenVal) ||
            !childrenVal.TryRead<LuaTable>(out var childrenTable))
        {
            LOGGER.LogWarning("Form has no valid Children table.");
            return false;
        }

        var children = new List<IAssistantComponent>();
        var count = childrenTable.ArrayLength;
        for (var idx = 1; idx <= count; idx++)
        {
            var childVal = childrenTable[idx];
            if (!childVal.TryRead<LuaTable>(out var childTable))
            {
                LOGGER.LogWarning($"Child #{idx} is not a table.");
                continue;
            }

            if (!this.TryReadComponentTable(idx, childTable, out var comp))
            {
                LOGGER.LogWarning($"Child #{idx} could not be parsed.");
                continue;
            }

            children.Add(comp);
        }

        root = AssistantComponentFactory.CreateComponent(
            AssistantUiCompontentType.FORM,
            new Dictionary<string, object>(), 
            children);
        return true;
    }

    /// <summary>
    /// Parses the components' table containing all members and properties.
    /// Recursively calls itself, if the component has a children table
    /// </summary>
    /// <param name="idx">Current index inside the <c>FORM</c> children</param>
    /// <param name="componentTable">The <c>LuaTable</c> containing all component properties</param>
    /// <param name="component">Outputs the component if the parsing is successful</param>
    /// <returns>True, when the component table could be read successfully.</returns>
    private bool TryReadComponentTable(int idx, LuaTable componentTable, out IAssistantComponent component)
    {
        component = null!;

        if (!componentTable.TryGetValue("Type", out var typeVal)
            || !typeVal.TryRead<string>(out var typeText)
            || !Enum.TryParse<AssistantUiCompontentType>(typeText, true, out var type))
        {
            LOGGER.LogWarning($"Component #{idx} missing valid Type.");
            return false;
        }

        Dictionary<string, object> props = new();
        if (componentTable.TryGetValue("Props", out var propsVal)
            && propsVal.TryRead<LuaTable>(out var propsTable))
        {
            if (!this.TryReadComponentProps(type, propsTable, out props))
                LOGGER.LogWarning($"Component #{idx} Props could not be fully read.");
        }

        var children = new List<IAssistantComponent>();
        if (componentTable.TryGetValue("Children", out var childVal)
            && childVal.TryRead<LuaTable>(out var childTable))
        {
            var cnt = childTable.ArrayLength;
            for (var i = 1; i <= cnt; i++)
            {
                var cv = childTable[i];
                if (cv.TryRead<LuaTable>(out var ct)
                    && this.TryReadComponentTable(i, ct, out var childComp))
                {
                    children.Add(childComp);
                }
            }
        }

        component = AssistantComponentFactory.CreateComponent(type, props, children);
        return true;
    }

    private bool TryReadComponentProps(
        AssistantUiCompontentType type,
        LuaTable propsTable,
        out Dictionary<string, object> props)
    {
        props = new Dictionary<string, object>();

        if (!ComponentPropSpecs.SPECS.TryGetValue(type, out var spec))
        {
            LOGGER.LogWarning($"No PropSpec defined for component type {type}");
            return false;
        }

        foreach (var key in spec.Required)
        {
            if (!propsTable.TryGetValue(key, out var luaVal))
            {
                LOGGER.LogWarning($"Component {type} missing required prop '{key}'.");
                return false;
            }
            if (!this.TryConvertLuaValue(luaVal, out var dotNetVal))
            {
                LOGGER.LogWarning($"Component {type}: prop '{key}' has wrong type.");
                return false;
            }
            props[key] = dotNetVal;
        }

        foreach (var key in spec.Optional)
        {
            if (!propsTable.TryGetValue(key, out var luaVal))
                continue;

            if (!this.TryConvertLuaValue(luaVal, out var dotNetVal))
            {
                LOGGER.LogWarning($"Component {type}: optional prop '{key}' has wrong type, skipping.");
                continue;
            }
            props[key] = dotNetVal;
        }

        return true;
    }
    
    private bool TryConvertLuaValue(LuaValue val, out object result)
    {
        if (val.TryRead<string>(out var s))
        {
            result = s;
            return true;
        }
        if (val.TryRead<bool>(out var b))
        {
            result = b;
            return true;
        }
        if (val.TryRead<double>(out var d))
        {
            result = d;
            return true;
        }
        
        result = null!;
        return false;
    }
}