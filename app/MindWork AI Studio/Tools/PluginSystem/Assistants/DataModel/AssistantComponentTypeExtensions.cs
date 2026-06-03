namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public static class AssistantComponentTypeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantComponentTypeExtensions).Namespace, nameof(AssistantComponentTypeExtensions));

    public static string GetDisplayName(this AssistantComponentType type) => type switch
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
    
    public static string GetIcon(this AssistantComponentType type) => type switch
    {
        AssistantComponentType.BUTTON => MudBlazor.Icons.Material.Filled.AdsClick,
        AssistantComponentType.BUTTON_GROUP => MudBlazor.Icons.Material.Filled.LinearScale,
        AssistantComponentType.DROPDOWN => MudBlazor.Icons.Material.Filled.Rule,
        AssistantComponentType.PROVIDER_SELECTION => MudBlazor.Icons.Material.Filled.Memory,
        AssistantComponentType.PROFILE_SELECTION => MudBlazor.Icons.Material.Filled.Badge,
        AssistantComponentType.SWITCH => MudBlazor.Icons.Material.Filled.ToggleOn,
        AssistantComponentType.HEADING => MudBlazor.Icons.Material.Filled.Title,
        AssistantComponentType.TEXT => MudBlazor.Icons.Material.Filled.TextFields,
        AssistantComponentType.TEXT_AREA => MudBlazor.Icons.Material.Filled.Wysiwyg,
        AssistantComponentType.LIST => MudBlazor.Icons.Material.Filled.List,
        AssistantComponentType.WEB_CONTENT_READER => MudBlazor.Icons.Material.Filled.Public,
        AssistantComponentType.FILE_CONTENT_READER => MudBlazor.Icons.Material.Filled.AttachFile,
        AssistantComponentType.IMAGE => MudBlazor.Icons.Material.Filled.Image,
        AssistantComponentType.COLOR_PICKER => MudBlazor.Icons.Material.Filled.Palette,
        AssistantComponentType.DATE_PICKER => MudBlazor.Icons.Material.Filled.CalendarMonth,
        AssistantComponentType.DATE_RANGE_PICKER => MudBlazor.Icons.Material.Filled.DateRange,
        AssistantComponentType.TIME_PICKER => MudBlazor.Icons.Material.Filled.Schedule,
        AssistantComponentType.LAYOUT_ITEM => MudBlazor.Icons.Material.Filled.DashboardCustomize,
        AssistantComponentType.LAYOUT_GRID => MudBlazor.Icons.Material.Filled.GridView,
        AssistantComponentType.LAYOUT_PAPER => MudBlazor.Icons.Material.Filled.Inbox,
        AssistantComponentType.LAYOUT_STACK => MudBlazor.Icons.Material.Filled.Layers,
        AssistantComponentType.LAYOUT_ACCORDION => MudBlazor.Icons.Material.Filled.CalendarViewDay,
        AssistantComponentType.LAYOUT_ACCORDION_SECTION => MudBlazor.Icons.Material.Filled.HorizontalSplit,
        AssistantComponentType.FORM => MudBlazor.Icons.Material.Filled.AccountTree,
        _ => MudBlazor.Icons.Material.Filled.AccountTree,
    };
}