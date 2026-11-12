namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public static class AssistantComponentTypeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantComponentTypeExtensions).Namespace, nameof(AssistantComponentTypeExtensions));

    extension(AssistantComponentType type)
    {
        public string DisplayName => type switch
        {
            AssistantComponentType.FORM => TB("Root"),
            AssistantComponentType.TEXT_AREA => TB("Text Area"),
            AssistantComponentType.BUTTON => TB("Button"),
            AssistantComponentType.BUTTON_GROUP => TB("Button group"),
            AssistantComponentType.DROPDOWN => TB("Dropdown"),
            AssistantComponentType.PROVIDER_SELECTION => TB("Provider Selection"),
            AssistantComponentType.PROFILE_SELECTION => TB("Profile Selection"),
            AssistantComponentType.SWITCH => TB("Switch"),
            AssistantComponentType.HEADING => TB("Heading"),
            AssistantComponentType.TEXT => TB("Text"),
            AssistantComponentType.LIST => TB("List"),
            AssistantComponentType.WEB_CONTENT_READER => TB("Web Content Reader"),
            AssistantComponentType.FILE_CONTENT_READER => TB("File Content Reader"),
            AssistantComponentType.FILE_ATTACHMENTS => TB("File Attachments"),
            AssistantComponentType.IMAGE => TB("Image"),
            AssistantComponentType.COLOR_PICKER => TB("Color Selection"),
            AssistantComponentType.DATE_PICKER => TB("Date Selection"),
            AssistantComponentType.DATE_RANGE_PICKER => TB("Date Range Selection"),
            AssistantComponentType.TIME_PICKER => TB("Time Selection"),
            AssistantComponentType.LAYOUT_ITEM => TB("Grid Item"),
            AssistantComponentType.LAYOUT_GRID => TB("Grid"),
            AssistantComponentType.LAYOUT_PAPER => TB("Container"),
            AssistantComponentType.LAYOUT_STACK => TB("Stack"),
            AssistantComponentType.LAYOUT_ACCORDION => TB("Accordion"),
            AssistantComponentType.LAYOUT_ACCORDION_SECTION => TB("Accordion Section"),
            _ => TB("Unknown Element")
        };

        public string Icon => type switch
        {
            AssistantComponentType.BUTTON => AppIcons.COMPONENT_BUTTON,
            AssistantComponentType.BUTTON_GROUP => AppIcons.COMPONENT_BUTTON_GROUP,
            AssistantComponentType.DROPDOWN => AppIcons.COMPONENT_DROPDOWN,
            AssistantComponentType.PROVIDER_SELECTION => AppIcons.LLM_PROVIDER,
            AssistantComponentType.PROFILE_SELECTION => AppIcons.PROFILE,
            AssistantComponentType.SWITCH => AppIcons.SWITCH,
            AssistantComponentType.HEADING => AppIcons.HEADING,
            AssistantComponentType.TEXT => AppIcons.TEXT_INPUT,
            AssistantComponentType.TEXT_AREA => AppIcons.TEXT_AREA,
            AssistantComponentType.LIST => AppIcons.LIST,
            AssistantComponentType.WEB_CONTENT_READER => AppIcons.WEB,
            AssistantComponentType.FILE_CONTENT_READER => AppIcons.ATTACH_FILE,
            AssistantComponentType.FILE_ATTACHMENTS => AppIcons.ATTACH_FILE,
            AssistantComponentType.IMAGE => AppIcons.IMAGE,
            AssistantComponentType.COLOR_PICKER => AppIcons.COLOR,
            AssistantComponentType.DATE_PICKER => AppIcons.DATE,
            AssistantComponentType.DATE_RANGE_PICKER => AppIcons.DATE_RANGE,
            AssistantComponentType.TIME_PICKER => AppIcons.TIME,
            AssistantComponentType.LAYOUT_ITEM => AppIcons.COMPONENT_LAYOUT_ITEM,
            AssistantComponentType.LAYOUT_GRID => AppIcons.ASSISTANT_COMPONENT_GRID,
            AssistantComponentType.LAYOUT_PAPER => AppIcons.COMPONENT_LAYOUT_PAPER,
            AssistantComponentType.LAYOUT_STACK => AppIcons.COMPONENT_LAYOUT_STACK,
            AssistantComponentType.LAYOUT_ACCORDION => AppIcons.COMPONENT_ACCORDION,
            AssistantComponentType.LAYOUT_ACCORDION_SECTION => AppIcons.ASSISTANT_COMPONENT_ACCORDION_SECTION,
            _ => AppIcons.ASSISTANT_COMPONENTS,
        };
    }
}
