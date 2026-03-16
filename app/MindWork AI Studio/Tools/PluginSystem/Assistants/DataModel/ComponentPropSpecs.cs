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
                optional: [
                    "IsIconButton", "Variant", "Color", "IsFullWidth", "Size",
                    "StartIcon", "EndIcon", "IconColor", "IconSize",  "Class", "Style"
                ]
            ),
            [AssistantComponentType.BUTTON_GROUP] = new(
                required: [],
                optional: ["Variant", "Color", "Size", "OverrideStyles", "Vertical", "DropShadow", "Class", "Style"]
            ),
            [AssistantComponentType.DROPDOWN] = new(
                required: ["Name", "Label", "Default", "Items"],
                optional: [
                    "UserPrompt", "IsMultiselect", "HasSelectAll", "SelectAllText", "HelperText", "ValueType",
                    "OpenIcon", "CloseIcon", "IconColor", "IconPositon", "Variant", "Class", "Style"
                ]
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
                required: ["Name", "Label", "Value"],
                optional: [ 
                    "OnChanged", "LabelOn", "LabelOff", "LabelPlacement", "Icon", "IconColor", "UserPrompt", 
                    "CheckedColor", "UncheckedColor", "Disabled", "Class", "Style",
                ]
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
                optional: [
                    "Placeholder", "ShowAlpha", "ShowToolbar", "ShowModeSwitch", 
                    "PickerVariant", "UserPrompt", "Class", "Style"
                ]
            ),
            [AssistantComponentType.DATE_PICKER] = new(
                required: ["Name", "Label"],
                optional: [
                    "Value", "Placeholder", "HelperText", "DateFormat",
                    "PickerVariant", "UserPrompt", "Class", "Style"
                ]
            ),
            [AssistantComponentType.DATE_RANGE_PICKER] = new(
                required: ["Name", "Label"],
                optional: [
                    "Value", "PlaceholderStart", "PlaceholderEnd", "HelperText", "DateFormat",
                    "PickerVariant", "UserPrompt", "Class", "Style"
                ]
            ),
            [AssistantComponentType.TIME_PICKER] = new(
                required: ["Name", "Label"],
                optional: [
                    "Value", "Placeholder", "HelperText", "TimeFormat", "AmPm",
                    "PickerVariant", "UserPrompt", "Class", "Style"
                ]
            ),
            [AssistantComponentType.LAYOUT_ITEM] = new(
                required: ["Name"],
                optional: ["Xs", "Sm", "Md", "Lg", "Xl", "Xxl", "Class", "Style"]
            ),
            [AssistantComponentType.LAYOUT_GRID] = new(
                required: ["Name"],
                optional: ["Justify", "Spacing", "Class", "Style"]
            ),
            [AssistantComponentType.LAYOUT_PAPER] = new(
                required: ["Name"],
                optional: [
                    "Elevation", "Height", "MaxHeight", "MinHeight", "Width", "MaxWidth", "MinWidth", 
                    "IsOutlined", "IsSquare", "Class", "Style"
                ]
            ),
            [AssistantComponentType.LAYOUT_STACK] = new(
                required: ["Name"],
                optional: [
                    "IsRow", "IsReverse", "Breakpoint", "Align", "Justify", "Stretch", 
                    "Wrap", "Spacing", "Class", "Style",
                ]
            ),
            [AssistantComponentType.LAYOUT_ACCORDION] = new(
                required: ["Name"],
                optional: [
                    "AllowMultiSelection", "IsDense", "HasOutline", "IsSquare", "Elevation", 
                    "HasSectionPaddings", "Class", "Style",
                ]
            ),
            [AssistantComponentType.LAYOUT_ACCORDION_SECTION] = new(
                required: ["Name", "HeaderText"],
                optional: [
                    "IsDisabled", "IsExpanded", "IsDense", "HasInnerPadding", "HideIcon", "HeaderIcon", "HeaderColor",
                    "HeaderTypo", "HeaderAlign", "MaxHeight","ExpandIcon", "Class", "Style",
                ]
            ),
        };
}
