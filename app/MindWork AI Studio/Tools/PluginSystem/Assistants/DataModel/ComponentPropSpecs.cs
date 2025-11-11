namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public static class ComponentPropSpecs
{
    public static readonly IReadOnlyDictionary<AssistantUiCompontentType, PropSpec> SPECS =
        new Dictionary<AssistantUiCompontentType, PropSpec>
        {
            [AssistantUiCompontentType.FORM] = new(
                required: ["Children"],
                optional: []
            ),
            [AssistantUiCompontentType.TEXT_AREA] = new(
                required: ["Name", "Label"],
                optional: ["UserPrompt", "PrefillText", "ReadOnly", "IsSingleLine"]
            ),
            [AssistantUiCompontentType.BUTTON] = new(
                required: ["Name", "Text", "Action"],
                optional: []
            ),
            [AssistantUiCompontentType.DROPDOWN] = new(
                required: ["Name", "Label", "Default", "Items"],
                optional: ["UserPrompt"]
            ),
            [AssistantUiCompontentType.PROVIDER_SELECTION] = new(
                required: ["Name", "Label"],
                optional: []
            ),
        };
}