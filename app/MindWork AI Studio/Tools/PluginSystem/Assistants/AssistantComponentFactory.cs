using AIStudio.Tools.PluginSystem.Assistants.DataModel;

namespace AIStudio.Tools.PluginSystem.Assistants;

public class AssistantComponentFactory
{
    private static readonly ILogger<AssistantComponentFactory> LOGGER = Program.LOGGER_FACTORY.CreateLogger<AssistantComponentFactory>();
    
    public static IAssistantComponent CreateComponent(
        AssistantUiCompontentType type,
        Dictionary<string, object> props,
        List<IAssistantComponent> children)
    {
        switch (type)
        {
            case AssistantUiCompontentType.FORM:
                return new AssistantForm { Props = props, Children = children };
            case AssistantUiCompontentType.TEXT_AREA:
                return new AssistantTextArea { Props = props, Children = children };
            case AssistantUiCompontentType.BUTTON:
                return new AssistantButton { Props = props, Children = children};
            case AssistantUiCompontentType.DROPDOWN:
                return new AssistantDropdown { Props = props, Children = children };
            case AssistantUiCompontentType.PROVIDER_SELECTION:
                return new AssistantProviderSelection { Props = props, Children = children };
            default:
                LOGGER.LogError($"Unknown assistant component type!\n{type} is not a supported assistant component type");
                throw new Exception($"Unknown assistant component type: {type}");
        }
    }
}