namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public static class ComponentPropSpecs
{
    public static readonly IReadOnlyDictionary<AssistantComponentType, PropSpec> SPECS =
        new Dictionary<AssistantComponentType, PropSpec>
        {
            [AssistantComponentType.FORM] = new(
                required: ["Children"],
                optional: ["Class", "Style"]
            ),
            [AssistantComponentType.TEXT_AREA] = new(
                required: ["Name", "Label"],
                optional: [
                    "HelperText", "HelperTextOnFocus", "UserPrompt", "PrefillText", 
                    "ReadOnly", "IsSingleLine", "Counter", "MaxLength", "IsImmediate",
                    "Adornment", "AdornmentIcon", "AdornmentText", "AdornmentColor", "Class", "Style",
                ]
            ),
            [AssistantComponentType.BUTTON] = new(
                required: ["Name", "Text", "Action"],
                optional: ["Class", "Style"]
            ),
            [AssistantComponentType.DROPDOWN] = new(
                required: ["Name", "Label", "Default", "Items"],
                optional: ["UserPrompt", "Class", "Style"]
            ),
            [AssistantComponentType.PROVIDER_SELECTION] = new(
                required: ["Name", "Label"],
                optional: ["Class", "Style"]
            ),
            [AssistantComponentType.PROFILE_SELECTION] = new(
                required: [],
                optional: ["ValidationMessage", "Class", "Style"]
            ),
            [AssistantComponentType.SWITCH] = new(
                required: ["Name", "Label", "LabelOn", "LabelOff", "Value"],
                optional: ["UserPrompt"]
            ),
            [AssistantComponentType.HEADING] = new(
                required: ["Text", "Level"],
                optional: ["Class", "Style"]
            ),
            [AssistantComponentType.TEXT] = new(
                required: ["Content"],
                optional: ["Class", "Style"]
            ),
            [AssistantComponentType.LIST] = new(
                required: ["Items"],
                optional: ["Class", "Style"]
            ),
            [AssistantComponentType.WEB_CONTENT_READER] = new(
                required: ["Name"],
                optional: ["UserPrompt", "Preselect", "PreselectContentCleanerAgent", "Class", "Style"]
            ),
            [AssistantComponentType.FILE_CONTENT_READER] = new(
                required: ["Name"],
                optional: ["UserPrompt", "Class", "Style"]
            ),
            [AssistantComponentType.IMAGE] = new(
                required: ["Src"],
                optional: ["Alt", "Caption", "Class", "Style"]
            ),
            [AssistantComponentType.COLOR_PICKER] = new(
                required: ["Name", "Label"],
                optional: ["Placeholder", "ShowAlpha", "ShowToolbar", "ShowModeSwitch", "PickerVariant", "UserPrompt", "Class", "Style"]
            ),
        };
}
