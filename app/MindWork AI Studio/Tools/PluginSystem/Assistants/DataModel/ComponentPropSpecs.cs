namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public static class ComponentPropSpecs
{
    public static readonly IReadOnlyDictionary<AssistantComponentType, PropSpec> SPECS =
        new Dictionary<AssistantComponentType, PropSpec>
        {
            [AssistantComponentType.FORM] = new(
                required: ["Children"],
                optional: []
            ),
            [AssistantComponentType.TEXT_AREA] = new(
                required: ["Name", "Label"],
                optional: ["UserPrompt", "PrefillText", "ReadOnly", "IsSingleLine"]
            ),
            [AssistantComponentType.BUTTON] = new(
                required: ["Name", "Text", "Action"],
                optional: []
            ),
            [AssistantComponentType.DROPDOWN] = new(
                required: ["Name", "Label", "Default", "Items"],
                optional: ["UserPrompt"]
            ),
            [AssistantComponentType.PROVIDER_SELECTION] = new(
                required: ["Name", "Label"],
                optional: []
            ),
            [AssistantComponentType.PROFILE_SELECTION] = new(
                required: [],
                optional: ["ValidationMessage"]
            ),
            [AssistantComponentType.SWITCH] = new(
                required: ["Name", "Label", "LabelOn", "LabelOff", "Value"],
                optional: ["UserPrompt"]
            ),
            [AssistantComponentType.HEADING] = new(
                required: ["Text", "Level"],
                optional: []
            ),
            [AssistantComponentType.TEXT] = new(
                required: ["Content"],
                optional: []
            ),
            [AssistantComponentType.LIST] = new(
                required: ["Items"],
                optional: []
            ),
            [AssistantComponentType.WEB_CONTENT_READER] = new(
                required: ["Name"],
                optional: ["UserPrompt", "Preselect", "PreselectContentCleanerAgent"]
            ),
            [AssistantComponentType.FILE_CONTENT_READER] = new(
                required: ["Name"],
                optional: ["UserPrompt"]
            ),
            [AssistantComponentType.IMAGE] = new(
                required: ["Src"],
                optional: ["Alt", "Caption"]
            ),
        };
}
