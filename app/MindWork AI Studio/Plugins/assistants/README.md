# Assistant Plugin Reference

This folder keeps the Lua manifest (`plugin.lua`) that defines a custom assistant. Treat it as the single source of truth for how AI Studio renders your assistant UI and builds the submitted prompt.

## Table of Contents
- [Assistant Plugin Reference](#assistant-plugin-reference)
  - [How to Use This Documentation](#how-to-use-this-documentation)
  - [Directory Structure](#directory-structure)
  - [Structure](#structure)
    - [Minimal Requirements Assistant Table](#example-minimal-requirements-assistant-table)
    - [Supported types (matching the Blazor UI components):](#supported-types-matching-the-blazor-ui-components)
  - [Component References](#component-references)
    - [`TEXT_AREA` reference](#text_area-reference)
    - [`DROPDOWN` reference](#dropdown-reference)
    - [`BUTTON` reference](#button-reference)
      - [`Action(input)` interface](#actioninput-interface)
    - [`BUTTON_GROUP` reference](#button_group-reference)
    - [`SWITCH` reference](#switch-reference)
    - [`COLOR_PICKER` reference](#color_picker-reference)
  - [Prompt Assembly - UserPrompt Property](#prompt-assembly---userprompt-property)
  - [Advanced Prompt Assembly - BuildPrompt()](#advanced-prompt-assembly---buildprompt)
    - [Interface](#interface)
    - [`input` table shape](#input-table-shape)
    - [Using `meta` inside BuildPrompt](#using-meta-inside-buildprompt)
      - [Example: iterate all fields with labels and include their values](#example-iterate-all-fields-with-labels-and-include-their-values)
      - [Example: handle types differently](#example-handle-types-differently)
    - [Using `profile` inside BuildPrompt](#using-profile-inside-buildprompt)
      - [Example: Add user profile context to the prompt](#example-add-user-profile-context-to-the-prompt)
  - [Advanced Layout Options](#advanced-layout-options)
    - [`LAYOUT_GRID` reference](#layout_grid-reference)
    - [`LAYOUT_ITEM` reference](#layout_item-reference)
    - [`LAYOUT_PAPER` reference](#layout_paper-reference)
    - [`LAYOUT_STACK` reference](#layout_stack-reference)
    - [`LAYOUT_ACCORDION` reference](#layout_accordion-reference)
    - [`LAYOUT_ACCORDION_SECTION` reference](#layout_accordion_section-reference)
  - [Useful Lua Functions](#useful-lua-functions)
    - [Included lua libraries](#included-lua-libraries)
    - [Logging helpers](#logging-helpers)
      - [Example: Use Logging in lua functions](#example-use-logging-in-lua-functions)
    - [Date/time helpers (assistant plugins only)](#datetime-helpers-assistant-plugins-only)
      - [Example: Use Logging in lua functions](#example-use-logging-in-lua-functions)
  - [General Tips](#general-tips)
  - [Useful Resources](#useful-resources)

## How to Use This Documentation
Use this README in layers. The early sections are a quick reference for the overall assistant manifest shape and the available component types, while the later `... reference` sections are the full detail for each component and advanced behavior.

When you build a plugin, start with the directory layout and the `Structure` section, then jump to the component references you actually use. The resource links at the end are the primary sources for Lua and MudBlazor behavior, and the `General Tips` section collects the practical rules and gotchas that matter most while authoring `plugin.lua`.

## Directory Structure
Each assistant plugin lives in its own directory under the assistants plugin root. In practice, you usually keep the manifest in `plugin.lua`, optional icon rendering in `icon.lua`, and any bundled media in `assets/`.

```
.
└── com.github.mindwork-ai.ai-studio/
    └── data/
        └── plugins/
            └── assistants/
                └── your-assistant-directory/
                    ├── assets/
                    │   └── your-media-files.jpg
                    ├── icon.lua
                    └── plugin.lua
```

## Structure
- `ASSISTANT` is the root table. It must contain `Title`, `Description`, `SystemPrompt`, `SubmitText`, `AllowProfiles`, and the nested `UI` definition.
- `UI.Type` is always `"FORM"` and `UI.Children` is a list of component tables.
- Each component table declares `Type`, an optional `Children` array, and a `Props` table that feeds the component’s parameters.

### Example: Minimal Requirements Assistant Table
```lua
ASSISTANT = {
    ["Title"] = "",
    ["Description"] = "",
    ["SystemPrompt"] = "",
    ["SubmitText"] = "",
    ["AllowProfiles"] = true,
    ["UI"] = {
        ["Type"] = "FORM",
        ["Children"] = {
          -- Components
        }
    },
}
```


#### Supported types (matching the Blazor UI components):

- `TEXT_AREA`: user input field based on `MudTextField`; requires `Name`, `Label`, and may include `HelperText`, `HelperTextOnFocus`, `Adornment`, `AdornmentIcon`, `AdornmentText`, `AdornmentColor`, `Counter`, `MaxLength`, `IsImmediate`, `UserPrompt`, `PrefillText`, `IsSingleLine`, `ReadOnly`, `Class`, `Style`.
- `DROPDOWN`: selects between variants; `Props` must include `Name`, `Label`, `Default`, `Items`, and optionally `ValueType` plus `UserPrompt`.
- `BUTTON`: invokes a Lua callback; `Props` must include `Name`, `Text`, `Action`, and may include `Variant`, `Color`, `IsFullWidth`, `Size`, `StartIcon`, `EndIcon`, `IconColor`, `IconSize`, `Class`, `Style`.
- `BUTTON_GROUP`: groups multiple `BUTTON` children in a `MudButtonGroup`; `Children` must contain only `BUTTON` components and `Props` may include `Variant`, `Color`, `Size`, `OverrideStyles`, `Vertical`, `DropShadow`, `Class`, `Style`.
- `LAYOUT_GRID`: renders a `MudGrid`; `Children` must contain only `LAYOUT_ITEM` components and `Props` may include `Justify`, `Spacing`, `Class`, `Style`.
- `LAYOUT_ITEM`: renders a `MudItem`; use it inside `LAYOUT_GRID` and configure breakpoints with `Xs`, `Sm`, `Md`, `Lg`, `Xl`, `Xxl`, plus optional `Class`, `Style`.
- `LAYOUT_PAPER`: renders a `MudPaper`; may include `Elevation`, `Height`, `MaxHeight`, `MinHeight`, `Width`, `MaxWidth`, `MinWidth`, `IsOutlined`, `IsSquare`, `Class`, `Style`.
- `LAYOUT_STACK`: renders a `MudStack`; may include `IsRow`, `IsReverse`, `Breakpoint`, `Align`, `Justify`, `Stretch`, `Wrap`, `Spacing`, `Class`, `Style`.
- `LAYOUT_ACCORDION`: renders a `MudExpansionPanels`; may include `AllowMultiSelection`, `IsDense`, `HasOutline`, `IsSquare`, `Elevation`, `HasSectionPaddings`, `Class`, `Style`.
- `LAYOUT_ACCORDION_SECTION`: renders a `MudExpansionPanel`; requires `Name`, `HeaderText`, and may include `IsDisabled`, `IsExpanded`, `IsDense`, `HasInnerPadding`, `HideIcon`, `HeaderIcon`, `HeaderColor`, `HeaderTypo`, `HeaderAlign`, `MaxHeight`, `ExpandIcon`, `Class`, `Style`.
- `SWITCH`: boolean option; requires `Name`, `Label`, `Value`, and may include `Disabled`, `UserPrompt`, `LabelOn`, `LabelOff`, `LabelPlacement`, `Icon`, `IconColor`, `CheckedColor`, `UncheckedColor`, `Class`, `Style`.
- `COLOR_PICKER`: color input based on `MudColorPicker`; requires `Name`, `Label`, and may include `Placeholder`, `ShowAlpha`, `ShowToolbar`, `ShowModeSwitch`, `PickerVariant`, `UserPrompt`, `Class`, `Style`.
- `PROVIDER_SELECTION` / `PROFILE_SELECTION`: hooks into the shared provider/profile selectors.
- `WEB_CONTENT_READER`: renders `ReadWebContent`; include `Name`, `UserPrompt`, `Preselect`, `PreselectContentCleanerAgent`.
- `FILE_CONTENT_READER`: renders `ReadFileContent`; include `Name`, `UserPrompt`.
- `IMAGE`: embeds a static illustration; `Props` must include `Src` plus optionally `Alt` and `Caption`. `Src` can be an HTTP/HTTPS URL, a `data:` URI, or a plugin-relative path (`plugin://assets/your-image.png`). The runtime will convert plugin-relative paths into `data:` URLs (base64).
- `HEADING`, `TEXT`, `LIST`: descriptive helpers.

Images referenced via the `plugin://` scheme must exist in the plugin directory (e.g., `assets/example.png`). Drop the file there and point `Src` at it. The component will read the file at runtime, encode it as Base64, and render it inside the assistant UI.

| Component                  | Required Props                      | Optional Props                                                                                                                                                                                                       | Renders                                                                                                                       |
|----------------------------|-------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| `TEXT_AREA`                | `Name`, `Label`                     | `HelperText`, `HelperTextOnFocus`, `Adornment`, `AdornmentIcon`, `AdornmentText`, `AdornmentColor`, `Counter`, `MaxLength`, `IsImmediate`, `UserPrompt`, `PrefillText`, `IsSingleLine`, `ReadOnly`, `Class`, `Style` | [MudTextField](https://www.mudblazor.com/components/textfield)                                                                |
| `DROPDOWN`                 | `Name`, `Label`, `Default`, `Items` | `IsMultiselect`, `HasSelectAll`, `SelectAllText`, `HelperText`, `OpenIcon`, `CloseIcon`, `IconColor`, `IconPositon`, `Variant`, `ValueType`, `UserPrompt`                                                            | [MudSelect](https://www.mudblazor.com/components/select)                                                                      |
| `BUTTON`                   | `Name`, `Text`, `Action`            | `Variant`, `Color`, `IsFullWidth`, `Size`, `StartIcon`, `EndIcon`, `IconColor`, `IconSize`, `Class`, `Style`                                                                                                         | [MudButton](https://www.mudblazor.com/components/button)                                                                      |
| `SWITCH`                   | `Name`, `Label`, `Value`            | `Disabled`, `UserPrompt`, `LabelOn`, `LabelOff`, `LabelPlacement`, `Icon`, `IconColor`, `CheckedColor`, `UncheckedColor`, `Class`, `Style`                                                                           | [MudSwitch](https://www.mudblazor.com/components/switch)                                                                      |
| `PROVIDER_SELECTION`       | `None`                              | `None`                                                                                                                                                                                                               | [`internal`](https://github.com/MindWorkAI/AI-Studio/blob/main/app/MindWork%20AI%20Studio/Components/ProviderSelection.razor) |
| `PROFILE_SELECTION`        | `None`                              | `None`                                                                                                                                                                                                               | [`internal`](https://github.com/MindWorkAI/AI-Studio/blob/main/app/MindWork%20AI%20Studio/Components/ProfileSelection.razor)  |
| `FILE_CONTENT_READER`      | `Name`                              | `UserPrompt`                                                                                                                                                                                                         | [`internal`](https://github.com/MindWorkAI/AI-Studio/blob/main/app/MindWork%20AI%20Studio/Components/ReadFileContent.razor)   |
| `WEB_CONTENT_READER`       | `Name`                              | `UserPrompt`                                                                                                                                                                                                         | [`internal`](https://github.com/MindWorkAI/AI-Studio/blob/main/app/MindWork%20AI%20Studio/Components/ReadWebContent.razor)    |
| `COLOR_PICKER`             | `Name`, `Label`                     | `Placeholder`, `ShowAlpha`, `ShowToolbar`, `ShowModeSwitch`, `PickerVariant`, `UserPrompt`, `Class`, `Style`                                                                                                         | [MudColorPicker](https://www.mudblazor.com/components/colorpicker)                                                            |
| `HEADING`                  | `Text`                              | `Level`                                                                                                                                                                                                              | [MudText Typo="Typo.<h2\|h3\|h4\|h5\|h6>"](https://www.mudblazor.com/components/typography)                                   |
| `TEXT`                     | `Content`                           | `None`                                                                                                                                                                                                               | [MudText Typo="Typo.body1"](https://www.mudblazor.com/components/typography)                                                  |
| `LIST`                     | `Type`, `Text`                      | `Href`                                                                                                                                                                                                               | [MudList](https://www.mudblazor.com/componentss/list)                                                                         |
| `IMAGE`                    | `Src`                               | `Alt`, `Caption`,`Src`                                                                                                                                                                                               | [MudImage](https://www.mudblazor.com/components/image)                                                                        |
| `BUTTON_GROUP`             | `None`                              | `Variant`, `Color`, `Size`, `OverrideStyles`, `Vertical`, `DropShadow`, `Class`, `Style`                                                                                                                             | [MudButtonGroup](https://www.mudblazor.com/components/buttongroup)                                                            |
| `LAYOUT_PAPER`             | `None`                              | `Elevation`, `Height`, `MaxHeight`, `MinHeight`, `Width`, `MaxWidth`, `MinWidth`, `IsOutlined`, `IsSquare`, `Class`, `Style`                                                                                         | [MudPaper](https://www.mudblazor.com/components/paper)                                                                        |
| `LAYOUT_ITEM`              | `None`                              | `Xs`, `Sm`, `Md`, `Lg`, `Xl`, `Xxl`, `Class`, `Style`                                                                                                                                                                | [MudItem](https://www.mudblazor.com/api/MudItem)                                                                              |
| `LAYOUT_STACK`             | `None`                              | `IsRow`, `IsReverse`, `Breakpoint`, `Align`, `Justify`, `Stretch`, `Wrap`, `Spacing`, `Class`, `Style`                                                                                                               | [MudStack](https://www.mudblazor.com/components/stack)                                                                        |
| `LAYOUT_GRID`              | `None`                              | `Justify`, `Spacing`, `Class`, `Style`                                                                                                                                                                               | [MudGrid](https://www.mudblazor.com/components/grid)                                                                          |
| `LAYOUT_ACCORDION`         | `None`                              | `AllowMultiSelection`, `IsDense`, `HasOutline`, `IsSquare`, `Elevation`, `HasSectionPaddings`, `Class`, `Style`                                                                                                      | [MudExpansionPanels](https://www.mudblazor.com/components/expansionpanels)                                                    |
| `LAYOUT_ACCORDION_SECTION` | `Name`, `HeaderText`                | `IsDisabled`, `IsExpanded`, `IsDense`, `HasInnerPadding`, `HideIcon`, `HeaderIcon`, `HeaderColor`, `HeaderTypo`, `HeaderAlign`, `MaxHeight`, `ExpandIcon`, `Class`, `Style`                                          | [MudExpansionPanel](https://www.mudblazor.com/components/expansionpanels)                                                     |
More information on rendered components can be found [here](https://www.mudblazor.com/docs/overview).

## Component References

### `TEXT_AREA` reference
- Use `Type = "TEXT_AREA"` to render a MudBlazor text input or textarea.
- Required props:
  - `Name`: unique state key used in prompt assembly and `BuildPrompt(input.fields)`.
  - `Label`: visible field label.
- Optional props:
  - `HelperText`: helper text rendered below the input.
  - `HelperTextOnFocus`: defaults to `false`; show helper text only while the field is focused.
  - `Adornment`: one of `Start`, `End`, `None`; invalid or omitted values fall back to `Start`.
  - `AdornmentIcon`: MudBlazor icon identifier string for the adornment.
  - `AdornmentText`: plain adornment text. Do not set this together with `AdornmentIcon`.
  - `AdornmentColor`: one of the MudBlazor `Color` enum names such as `Primary`, `Secondary`, `Warning`; invalid or omitted values fall back to `Default`.
  - `Counter`: nullable integer. Omit it to hide the counter entirely. Set `0` to show only the current character count. Set `1` or higher to show `current/max`.
  - `MaxLength`: maximum number of characters allowed; defaults to `524288`.
  - `IsImmediate`: defaults to `false`; updates the bound value on each input event instead of on blur/change.
  - `UserPrompt`: prompt context text for this field.
  - `PrefillText`: initial input value.
  - `IsSingleLine`: defaults to `false`; render as a one-line input instead of a textarea.
  - `ReadOnly`: defaults to `false`; disables editing.
  - `Class`, `Style`: forwarded to the rendered component for layout/styling.

#### Example Textarea component
```lua
{
  ["Type"] = "TEXT_AREA",
  ["Props"] = {
    ["Name"] = "Budget",
    ["Label"] = "Budget",
    ["HelperText"] = "Enter the expected amount.",
    ["Adornment"] = "Start",
    ["AdornmentIcon"] = "Icons.Material.Filled.AttachMoney",
    ["AdornmentColor"] = "Success",
    ["Counter"] = 0,
    ["MaxLength"] = 100,
    ["IsImmediate"] = true,
    ["UserPrompt"] = "Use this budget information in your answer.",
    ["PrefillText"] = "",
    ["IsSingleLine"] = true
  }
}
```
---

### `DROPDOWN` reference
- Use `Type = "DROPDOWN"` to render a MudBlazor select field.
- Required props:
  - `Name`: unique state key used in prompt assembly, button actions, and `BuildPrompt(input.fields)`.
  - `Label`: visible field label.
  - `Default`: dropdown item table with the shape `{ ["Value"] = "<internal value>", ["Display"] = "<visible label>" }`.
  - `Items`: array of dropdown item tables with the same shape as `Default`.
- Optional props:
  - `UserPrompt`: prompt context text for this field.
  - `ValueType`: one of `string`, `int`, `double`, `bool`; currently the dropdown values exposed to prompt building and button actions are handled as the configured item `Value`s, with typical usage being `string`.
  - `IsMultiselect`: defaults to `false`; when `true`, the component allows selecting multiple items.
  - `HasSelectAll`: defaults to `false`; enables MudBlazor's select-all behavior for multiselect dropdowns.
  - `SelectAllText`: custom label for the select-all action in multiselect mode.
  - `HelperText`: helper text rendered below the dropdown.
  - `OpenIcon`: MudBlazor icon identifier used while the dropdown is closed.
  - `CloseIcon`: MudBlazor icon identifier used while the dropdown is open.
  - `IconColor`: one of the MudBlazor `Color` enum names such as `Primary`, `Secondary`, `Warning`; invalid or omitted values fall back to `Default`.
  - `IconPositon`: one of `Start` or `End`; controls where the icon adornment is rendered.
  - `Variant`: one of the MudBlazor `Variant` enum names such as `Text`, `Filled`, `Outlined`; invalid or omitted values fall back to `Outlined`.
  - `Class`, `Style`: forwarded to the rendered component for layout/styling.
- Dropdown item shape:
  - `Value`: the internal raw value stored in component state and passed to prompt building.
  - `Display`: the visible label shown to the user in the menu and selection text.
- Behavior notes:
  - For single-select dropdowns, `input.fields.<Name>` is a single raw value such as `germany`.
  - For multiselect dropdowns, `input.fields.<Name>` is an array-like Lua table of raw values.
  - The UI shows the `Display` text, while prompt assembly and `BuildPrompt(input)` receive the raw `Value`.
  - `Default` should usually also exist in `Items`. If it is missing there, the runtime currently still renders it as an available option.

#### Example Dropdown component
```lua
{
  ["Type"] = "DROPDOWN",
  ["Props"] = {
    ["Name"] = "targetCountries",
    ["Label"] = "Target countries",
    ["UserPrompt"] = "Use the selected countries in your answer.",
    ["ValueType"] = "string",
    ["IsMultiselect"] = true,
    ["HasSelectAll"] = true,
    ["SelectAllText"] = "Select all countries",
    ["HelperText"] = "Pick one or more countries.",
    ["OpenIcon"] = "Icons.Material.Filled.ArrowDropDown",
    ["CloseIcon"] = "Icons.Material.Filled.ArrowDropUp",
    ["IconColor"] = "Secondary",
    ["IconPositon"] = "End",
    ["Variant"] = "Filled",
    ["Default"] = { ["Value"] = "germany", ["Display"] = "Germany" },
    ["Items"] = {
      { ["Value"] = "germany", ["Display"] = "Germany" },
      { ["Value"] = "austria", ["Display"] = "Austria" },
      { ["Value"] = "france", ["Display"] = "France" }
    },
    ["Class"] = "mb-3",
    ["Style"] = "min-width: 16rem;"
  }
}
```
---

### `BUTTON` reference
- Use `Type = "BUTTON"` to render a clickable action button.
- Required props:
  - `Name`: unique identifier used to track execution state and logging.
  - `Text`: visible button label.
  - `Action`: Lua function called on button click.
- Optional props:
  - `Variant`: one of the MudBlazor `Variant` enum names such as `Filled`, `Outlined`, `Text`; omitted values fall back to `Filled`.
  - `Color`: one of the MudBlazor `Color` enum names such as `Default`, `Primary`, `Secondary`, `Info`; omitted values fall back to `Default`.
  - `IsFullWidth`: defaults to `false`; when `true`, the button expands to the available width.
  - `Size`: one of the MudBlazor `Size` enum names such as `Small`, `Medium`, `Large`; omitted values fall back to `Medium`.
  - `StartIcon`: MudBlazor icon identifier string rendered before the button text.
  - `EndIcon`: MudBlazor icon identifier string rendered after the button text.
  - `IconColor`: one of the MudBlazor `Color` enum names; omitted values fall back to `Inherit`.
  - `IconSize`: one of the MudBlazor `Size` enum names; omitted values fall back to `Medium`.
  - `Class`, `Style`: forwarded to the rendered component for layout/styling.

#### `Action(input)` interface
- The function receives the same `input` structure as `ASSISTANT.BuildPrompt(input)`.
- Return `nil` for no state update.
- To update component state, return a table with a `fields` table.
- `fields` keys must reference existing component `Name` values.
- Supported write targets:
  - `TEXT_AREA`, single-select `DROPDOWN`, `WEB_CONTENT_READER`, `FILE_CONTENT_READER`, `COLOR_PICKER`: string values
  - multiselect `DROPDOWN`: array-like Lua table of strings
  - `SWITCH`: boolean values
- Unknown field names and wrong value types are ignored and logged.

#### Example Button component
```lua
{
  ["Type"] = "BUTTON",
  ["Props"] = {
    ["Name"] = "buildEmailOutput",
    ["Text"] = "Build output",
    ["Variant"] = "Filled",
    ["Color"] = "Primary",
    ["IsFullWidth"] = false,
    ["Size"] = "Medium",
    ["StartIcon"] = "Icons.Material.Filled.AutoFixHigh",
    ["EndIcon"] = "Icons.Material.Filled.ArrowForward",
    ["IconColor"] = "Inherit",
    ["IconSize"] = "Medium",
    ["Action"] = function(input)
      local email = input.fields.emailContent or ""
      local translate = input.fields.translateEmail or false
      local output = email

      if translate then
        output = output .. "\n\nTranslate this email:"
      end

      return {
        fields = {
          outputTextField = output
        }
      }
    end,
    ["Class"] = "mb-3",
    ["Style"] = "min-width: 12rem;"
  }
}
```
---

### `BUTTON_GROUP` reference
- Use `Type = "BUTTON_GROUP"` to render multiple `BUTTON` children as a single MudBlazor button group.
- Required structure:
  - `Children`: array of `BUTTON` component tables. Other child component types are ignored.
- Optional props:
  - `Variant`: one of the MudBlazor `Variant` enum names such as `Filled`, `Outlined`, `Text`; omitted values fall back to `Filled`.
  - `Color`: one of the MudBlazor `Color` enum names such as `Default`, `Primary`, `Secondary`, `Info`; omitted values fall back to `Default`.
  - `Size`: one of the MudBlazor `Size` enum names such as `Small`, `Medium`, `Large`; omitted values fall back to `Medium`.
  - `OverrideStyles`: defaults to `false`; enables MudBlazor button-group style overrides.
  - `Vertical`: defaults to `false`; when `true`, buttons are rendered vertically instead of horizontally.
  - `DropShadow`: defaults to `true`; controls the group shadow.
  - `Class`, `Style`: forwarded to the rendered `MudButtonGroup` for layout/styling.
- Child buttons use the existing `BUTTON` props and behavior, including Lua `Action(input)`.

#### Example Button-Group component
```lua
{
  ["Type"] = "BUTTON_GROUP",
  ["Props"] = {
    ["Variant"] = "Filled",
    ["Color"] = "Primary",
    ["Size"] = "Medium",
    ["OverrideStyles"] = false,
    ["Vertical"] = false,
    ["DropShadow"] = true
  },
  ["Children"] = {
    {
      ["Type"] = "BUTTON",
      ["Props"] = {
        ["Name"] = "buildEmailOutput",
        ["Text"] = "Build output",
        ["Action"] = function(input)
          return {
            fields = {
              outputBuffer = input.fields.emailContent or ""
            }
          }
        end,
        ["StartIcon"] = "Icons.Material.Filled.Build"
      }
    },
    {
      ["Type"] = "BUTTON",
      ["Props"] = {
        ["Name"] = "logColor",
        ["Text"] = "Log color",
        ["Action"] = function(input)
          LogError("ColorPicker value: " .. tostring(input.fields.colorPicker or ""))
          return nil
        end,
        ["EndIcon"] = "Icons.Material.Filled.BugReport"
      }
    }
  }
}
```
---

### `SWITCH` reference
- Use `Type = "SWITCH"` to render a boolean toggle.
- Required props:
  - `Name`: unique state key used in prompt assembly and `BuildPrompt(input.fields)`.
  - `Label`: visible label for the switch field.
  - `Value`: initial boolean state (`true` or `false`).
- Optional props:
  - `Disabled`: defaults to `false`; disables user interaction while still allowing the value to be included in prompt assembly.
  - `UserPrompt`: prompt context text for this field.
  - `LabelOn`: text shown when the switch value is `true`.
  - `LabelOff`: text shown when the switch value is `false`.
  - `LabelPlacement`: one of `Bottom`, `End`, `Left`, `Right`, `Start`, `Top`; omitted values follow the renderer default.
  - `Icon`: MudBlazor icon identifier string displayed inside the switch thumb.
  - `IconColor`: one of the MudBlazor `Color` enum names such as `Primary`, `Secondary`, `Warning`; omitted values default to `Inherit`.
  - `CheckedColor`: color used when the switch state is `true`; omitted values default to `Inherit`.
  - `UncheckedColor`: color used when the switch state is `false`; omitted values default to `Inherit`.
  - `Class`, `Style`: forwarded to the rendered component for layout/styling.

#### Example Switch component
```lua
{
  ["Type"] = "SWITCH",
  ["Props"] = {
    ["Name"] = "IncludeSummary",
    ["Label"] = "Include summary",
    ["Value"] = true,
    ["Disabled"] = false,
    ["UserPrompt"] = "Decide whether the final answer should include a short summary.",
    ["LabelOn"] = "Summary enabled",
    ["LabelOff"] = "Summary disabled",
    ["LabelPlacement"] = "End",
    ["Icon"] = "Icons.Material.Filled.Summarize",
    ["IconColor"] = "Primary",
    ["CheckedColor"] = "Success",
    ["UncheckedColor"] = "Default",
    ["Class"] = "mb-6",
  }
}
```
---

### `COLOR_PICKER` reference
- Use `Type = "COLOR_PICKER"` to render a MudBlazor color picker.
- Required props:
  - `Name`: unique state key used in prompt assembly and `BuildPrompt(input.fields)`.
  - `Label`: visible field label.
- Optional props:
  - `Placeholder`: default color hex string (e.g. `#FF10FF`) or initial hint text.
  - `ShowAlpha`: defaults to `true`; enables alpha channel editing.
  - `ShowToolbar`: defaults to `true`; shows picker/grid/palette toolbar.
  - `ShowModeSwitch`: defaults to `true`; allows switching between HEX/RGB(A)/HSL modes.
  - `PickerVariant`: one of `DIALOG`, `INLINE`, `STATIC`; invalid or omitted values fall back to `STATIC`.
  - `UserPrompt`: prompt context text for the selected color.
  - `Class`, `Style`: forwarded to the rendered component for layout/styling.

#### Example Colorpicker component
```lua
{
  ["Type"] = "COLOR_PICKER",
  ["Props"] = {
    ["Name"] = "accentColor",
    ["Label"] = "Accent color",
    ["Placeholder"] = "#FFAA00",
    ["ShowAlpha"] = false,
    ["ShowToolbar"] = true,
    ["ShowModeSwitch"] = true,
    ["PickerVariant"] = "STATIC",
    ["UserPrompt"] = "Use this as the accent color for the generated design."
  }
}
```

## Prompt Assembly - UserPrompt Property
Each component exposes a `UserPrompt` string. When the assistant runs, `AssistantDynamic` recursively iterates over the component tree and, for each component that has a prompt, emits:

```
context:
<UserPrompt>
---
user prompt:
<value extracted from the component>
```

For switches the “value” is the boolean `true/false`; for readers it is the fetched/selected content; for color pickers it is the selected color text (for example `#FFAA00` or `rgba(...)`, depending on the picker mode). Always provide a meaningful `UserPrompt` so the final concatenated prompt remains coherent from the LLM’s perspective.

## Advanced Prompt Assembly - BuildPrompt()
If you want full control over prompt composition, define `ASSISTANT.BuildPrompt` as a Lua function. When present, AI Studio calls it and uses its return value as the final user prompt. The default prompt assembly is skipped.

---
### Interface
- `ASSISTANT.BuildPrompt(LuaTable input) => string` must return a **string**, the complete User Prompt.
- If the function is missing, returns `nil`, or returns a non-string, AI Studio falls back to the default prompt assembly.
- Errors in the function are caught and logged, then fall back to the default prompt assembly.
---
### `input` table shape
The function receives a single `input` Lua table with:
- `input.fields`: values keyed by component `Name`
  - Text area, single-select dropdown, and readers are strings
  - Multiselect dropdown is an array-like Lua table of strings
  - Switch is a boolean
  - Color picker is the selected color as a string
- `input.meta`: per-component metadata keyed by component `Name`
  - `Type` (string, e.g. `TEXT_AREA`, `DROPDOWN`, `SWITCH`, `COLOR_PICKER`)
  - `Label` (string, when provided)
  - `UserPrompt` (string, when provided)
- `input.profile`: selected profile data
  - `Name`, `NeedToKnow`, `Actions`, `Num`
  - When no profile is selected, values match the built-in "Use no profile" entry
```
input = {
  fields = {
    ["<Name>"] = "<string|boolean>",
    ...
  },
  meta = {
    ["<Name>"] = {
      Type = "<TEXT_AREA|DROPDOWN|SWITCH|WEB_CONTENT_READER|FILE_CONTENT_READER|COLOR_PICKER>",
      Label = "<string?>",
      UserPrompt = "<string?>"
    },
    ...
  },
  profile = {
    Name = "<string>",
    NeedToKnow = "<string>",
    Actions = "<string>",
    Num = <number>
  }
}

-- <Name> is the value you set in the components name property
```
---

### Using `meta` inside BuildPrompt
`input.meta` is useful when you want to dynamically build the prompt based on component type or reuse existing UI text (labels/user prompts).

#### Example: iterate all fields with labels and include their values
```lua
ASSISTANT.BuildPrompt = function(input)
  local parts = {}
  for name, value in pairs(input.fields) do
    local meta = input.meta[name]
    if meta and meta.Label and value ~= "" then
      table.insert(parts, meta.Label .. ": " .. tostring(value))
    end
  end
  return table.concat(parts, "\n")
end
```

#### Example: handle types differently
```lua
ASSISTANT.BuildPrompt = function(input)
  local parts = {}
  for name, meta in pairs(input.meta) do
    local value = input.fields[name]
    if meta.Type == "SWITCH" then
      table.insert(parts, name .. ": " .. tostring(value))
    elseif meta.Type == "COLOR_PICKER" and value and value ~= "" then
      table.insert(parts, name .. ": " .. value)
    elseif value and value ~= "" then
      table.insert(parts, name .. ": " .. value)
    end
  end
  return table.concat(parts, "\n")
end
```
--- 
### Using `profile` inside BuildPrompt
Profiles are optional user context (e.g., "NeedToKnow" and "Actions"). You can inject this directly into the user prompt if you want the LLM to always see it.

#### Example: Add user profile context to the prompt
```lua
ASSISTANT.BuildPrompt = function(input)
  local parts = {}
  if input.profile and input.profile.NeedToKnow ~= "" then
    table.insert(parts, "User context:")
    table.insert(parts, input.profile.NeedToKnow)
    table.insert(parts, "")
  end
  table.insert(parts, input.fields.Main or "")
  return table.concat(parts, "\n")
end
```
## Advanced Layout Options

### `LAYOUT_GRID` reference
A 12-column grid system for organizing content with responsive breakpoints for different screen sizes.
```
+------------------------------------------------------------+
|                           12                               |
+------------------------------------------------------------+

+----------------------------+  +----------------------------+
|            6               |  |            6               |
+----------------------------+  +----------------------------+

+------------+  +------------+  +-----------+  +-------------+
|      3     |  |      3     |  |     3     |  |      3      |
+------------+  +------------+  +-----------+  +-------------+

```

- Use `Type = "LAYOUT_GRID"` to render a MudBlazor grid container.
- Required props:
  - `Name`: unique identifier for the layout node.
- Required structure:
  - `Children`: array of `LAYOUT_ITEM` component tables. Other child component types are ignored.
- Optional props:
  - `Justify`: one of the MudBlazor `Justify` enum names such as `FlexStart`, `Center`, `SpaceBetween`; omitted values fall back to `FlexStart`.
  - `Spacing`: integer spacing between grid items; omitted values fall back to `6`.
  - `Class`, `Style`: forwarded to the rendered `MudGrid` for layout/styling.

#### Example: How to define a flexible grid
```lua
{
  ["Type"] = "LAYOUT_GRID",
  ["Props"] = {
    ["Name"] = "mainGrid",
    ["Justify"] = "FlexStart",
    ["Spacing"] = 2
  },
  ["Children"] = {
    {
      ["Type"] = "LAYOUT_ITEM",
      ["Props"] = {
        ["Name"] = "contentColumn",
        ["Xs"] = 12,
        ["Lg"] = 8
      },
      ["Children"] = {
        ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
        ["Props"] = {...},
      },
    },
    {
      ["Type"] = "LAYOUT_ITEM",
      ["Props"] = {
        ["Name"] = "contentColumn2",
        ["Xs"] = 12,
        ["Lg"] = 8
      },
      ["Children"] = {
        ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
        ["Props"] = {...},
      },
    },
    ...
  }
}
```
For a visual example and a full explanation look [here](https://www.mudblazor.com/components/grid#spacing)

---

### `LAYOUT_ITEM` reference
`LAYOUT_ITEM` is used to wrap children components to use them into a grid.
The Breakpoints define how many columns the wrapped components take up in a 12-column grid.
Read more about breakpoint [here](https://www.mudblazor.com/features/breakpoints#breakpoints).

- Use `Type = "LAYOUT_ITEM"` to render a MudBlazor grid item.
- Required props:
  - `Name`: unique identifier for the layout node.
- Intended parent:
  - Use this component inside `LAYOUT_GRID`.
- Optional props:
  - `Xs`, `Sm`, `Md`, `Lg`, `Xl`, `Xxl`: integer breakpoint widths. Omit a breakpoint to leave it unset.
  - `Class`, `Style`: forwarded to the rendered `MudItem` for layout/styling.
- `Children` may contain any other assistant components you want to place inside the item.

#### Example: How to wrap a child component and define its breakpoints
```lua
{
  ["Type"] = "LAYOUT_ITEM",
  ["Props"] = {
    ["Name"] = "contentColumn",
    ["Xs"] = 12,
    ["Lg"] = 8
  },
  ["Children"] = {
    {
      ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
      ["Props"] = {...},
    }
  }
}
```
For a full explanation look [here](https://www.mudblazor.com/api/MudItem#pages)

---

### `LAYOUT_PAPER` reference
- Use `Type = "LAYOUT_PAPER"` to render a MudBlazor paper container.
- Required props:
  - `Name`: unique identifier for the layout node.
- Optional props:
  - `Elevation`: integer elevation; omitted values fall back to `1`.
  - `Height`, `MaxHeight`, `MinHeight`, `Width`, `MaxWidth`, `MinWidth`: CSS size values such as `100%`, `24rem`, `50vh`.
  - `IsOutlined`: defaults to `false`; toggles outlined mode.
  - `IsSquare`: defaults to `false`; removes rounded corners.
  - `Class`, `Style`: forwarded to the rendered `MudPaper` for layout/styling.
- `Children` may contain any other assistant components you want to wrap.

#### Example: How to define a MudPaper wrapping child components
```lua
{
  ["Type"] = "LAYOUT_PAPER",
  ["Props"] = {
    ["Name"] = "contentPaper",
    ["Elevation"] = 2,
    ["Width"] = "100%",
    ["IsOutlined"] = true
  },
  ["Children"] = {
    {
      ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
      ["Props"] = {...},
    },
    ...
  }
}
```
For a visual example and a full explanation look [here](https://www.mudblazor.com/components/paper#material-design)

---

### `LAYOUT_STACK` reference
- Use `Type = "LAYOUT_STACK"` to render a MudBlazor stack layout.
- Required props:
  - `Name`: unique identifier for the layout node.
- Optional props:
  - `IsRow`: defaults to `false`; renders items horizontally.
  - `IsReverse`: defaults to `false`; reverses the visual order.
  - `Breakpoint`: one of the MudBlazor `Breakpoint` enum names such as `Sm`, `Md`, `Lg`; omitted values fall back to `None`.
  - `Align`: one of the MudBlazor `AlignItems` enum names such as `Start`, `Center`, `Stretch`; omitted values fall back to `Stretch`.
  - `Justify`: one of the MudBlazor `Justify` enum names such as `FlexStart`, `Center`, `SpaceBetween`; omitted values fall back to `FlexStart`.
  - `Stretch`: one of the MudBlazor `StretchItems` enum names such as `None`, `Start`, `End`, `Stretch`; omitted values fall back to `None`.
  - `Wrap`: one of the MudBlazor `Wrap` enum names such as `Wrap`, `NoWrap`, `WrapReverse`; omitted values fall back to `Wrap`.
  - `Spacing`: integer spacing between child components; omitted values fall back to `3`.
  - `Class`, `Style`: forwarded to the rendered `MudStack` for layout/styling.
- `Children` may contain any other assistant components you want to arrange.

#### Example: Define a stack of children components
```lua
{
  ["Type"] = "LAYOUT_STACK",
  ["Props"] = {
    ["Name"] = "toolbarRow",
    ["IsRow"] = true,
    ["Align"] = "Center",
    ["Justify"] = "SpaceBetween",
    ["Spacing"] = 2
  },
  ["Children"] = {
    {
      ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
      ["Props"] = {...},
    },
    ...
  }
}
```
For a visual example and a full explanation look [here](https://www.mudblazor.com/components/stack#basic-usage)

---

### `LAYOUT_ACCORDION` reference
- Use `Type = "LAYOUT_ACCORDION"` to render a MudBlazor accordion container (`MudExpansionPanels`).
- Required props:
  - `Name`: unique identifier for the layout node.
- Required structure:
  - `Children`: array of `LAYOUT_ACCORDION_SECTION` component tables. Other child component types are ignored by intent and should be avoided.
- Optional props:
  - `AllowMultiSelection`: defaults to `false`; allows multiple sections to stay expanded at the same time.
  - `IsDense`: defaults to `false`; reduces the visual density of the accordion.
  - `HasOutline`: defaults to `false`; toggles outlined panel styling.
  - `IsSquare`: defaults to `false`; removes rounded corners from the accordion container.
  - `Elevation`: integer elevation; omitted values fall back to `0`.
  - `HasSectionPaddings`: defaults to `false`; toggles the section gutter/padding behavior.
  - `Class`, `Style`: forwarded to the rendered `MudExpansionPanels` for layout/styling.

#### Example: Define an accordion container
```lua
{
  ["Type"] = "LAYOUT_ACCORDION",
  ["Props"] = {
    ["Name"] = "settingsAccordion",
    ["AllowMultiSelection"] = true,
    ["IsDense"] = false,
    ["HasOutline"] = true,
    ["IsSquare"] = false,
    ["Elevation"] = 0,
    ["HasSectionPaddings"] = true
  },
  ["Children"] = {
    {
      ["Type"] = "LAYOUT_ACCORDION_SECTION",
      ["Props"] = {
        ["Name"] = "generalSection",
        ["HeaderText"] = "General"
      },
      ["Children"] = {
        {
          ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
          ["Props"] = {...},
        }
      }
    }
  }
}
```
Use `LAYOUT_ACCORDION` as the outer wrapper and put the actual content into one or more `LAYOUT_ACCORDION_SECTION` children.

---

### `LAYOUT_ACCORDION_SECTION` reference
- Use `Type = "LAYOUT_ACCORDION_SECTION"` to render one expandable section inside `LAYOUT_ACCORDION`.
- Required props:
  - `Name`: unique identifier for the layout node.
  - `HeaderText`: visible header text shown in the section title row.
- Intended parent:
  - Use this component inside `LAYOUT_ACCORDION`.
- Optional props:
  - `IsDisabled`: defaults to `false`; disables user interaction for the section.
  - `IsExpanded`: defaults to `false`; sets the initial expanded state.
  - `IsDense`: defaults to `false`; reduces section density.
  - `HasInnerPadding`: defaults to `true`; controls the inner content gutter/padding.
  - `HideIcon`: defaults to `false`; hides the expand/collapse icon.
  - `HeaderIcon`: MudBlazor icon identifier rendered before the header text.
  - `HeaderColor`: one of the MudBlazor `Color` enum names such as `Primary`, `Secondary`, `Warning`; omitted values fall back to `Inherit`.
  - `HeaderTypo`: one of the MudBlazor `Typo` enum names such as `body1`, `subtitle1`, `h6`; omitted values follow the renderer default.
  - `HeaderAlign`: one of the MudBlazor `Align` enum names such as `Start`, `Center`, `End`; omitted values follow the renderer default.
  - `MaxHeight`: nullable integer max height in pixels for the expanded content area.
  - `ExpandIcon`: MudBlazor icon identifier used for the expand/collapse control.
  - `Class`, `Style`: forwarded to the rendered `MudExpansionPanel` for layout/styling.
- `Children` may contain any other assistant components you want to reveal inside the section.

#### Example: Define an accordion section
```lua
{
  ["Type"] = "LAYOUT_ACCORDION_SECTION",
  ["Props"] = {
    ["Name"] = "advancedOptions",
    ["HeaderText"] = "Advanced options",
    ["IsDisabled"] = false,
    ["IsExpanded"] = true,
    ["IsDense"] = false,
    ["HasInnerPadding"] = true,
    ["HideIcon"] = false,
    ["HeaderIcon"] = "Icons.Material.Filled.Tune",
    ["HeaderColor"] = "Primary",
    ["HeaderTypo"] = "subtitle1",
    ["HeaderAlign"] = "Start",
    ["MaxHeight"] = 320,
    ["ExpandIcon"] = "Icons.Material.Filled.ExpandMore"
  },
  ["Children"] = {
    {
      ["Type"] = "<TEXT_AREA|BUTTON|BUTTON_GROUP|SWITCH|PROVIDER_SELECTION|...>",
      ["Props"] = {...},
    }
  }
}
```
`MaxHeight` is an integer pixel value, unlike `LAYOUT_PAPER` sizing props which accept CSS length strings such as `24rem` or `50vh`.

## Useful Lua Functions
### Included lua libraries
- [Basic Functions Library](https://www.lua.org/manual/5.2/manual.html#6.1)
- [Coroutine Manipulation Library](https://www.lua.org/manual/5.2/manual.html#6.2)
- [String Manipulation Library](https://www.lua.org/manual/5.2/manual.html#6.4)
- [Table Manipulation Library](https://www.lua.org/manual/5.2/manual.html#6.5)
- [Mathematical Functions Library](https://www.lua.org/manual/5.2/manual.html#6.6)
- [Bitwise Operations Library](https://www.lua.org/manual/5.2/manual.html#6.7)
---

### Logging helpers
The assistant runtime exposes basic logging helpers to Lua. Use them to debug custom prompt building.

- `LogDebug(message)`
- `LogInfo(message)`
- `LogWarn(message)`
- `LogError(message)`

#### Example: Use Logging in lua functions
```lua
ASSISTANT.BuildPrompt = function(input)
  LogInfo("BuildPrompt called")
  return input.fields.Text or ""
end
```
---

### Date/time helpers (assistant plugins only)
Use these when you need timestamps inside Lua.

- `DateTime(format)` returns a table with date/time parts plus a formatted string.
  - `format` is optional; default is `yyyy-MM-dd HH:mm:ss` (ISO 8601-like).
  - `formatted` contains the date in your desired format (e.g. `dd.MM.yyyy HH:mm`) or the default.
  - Members: `year`, `month`, `day`, `hour`, `minute`, `second`, `millisecond`, `formatted`.
- `Timestamp()` returns a UTC timestamp in ISO-8601 format (`O` / round-trip), e.g. `2026-03-02T21:15:30.1234567Z`.

#### Example: Use the datetime functions in lua 
```lua
local dt = DateTime("yyyy-MM-dd HH:mm:ss")
LogInfo(dt.formatted)
LogInfo(Timestamp())
LogInfo(dt.day .. "." .. dt.month .. "." .. dt.year)
```

## General Tips

1. Give every component a _**unique**_ `Name`— it’s used to track state and treated like an Id.
2. Keep in mind that components and their properties are _**case-sensitive**_ (e.g. if you write `["Type"] = "heading"` instead of `["Type"] = "HEADING"` the component will not be registered). Always copy-paste the component from the `plugin.lua` manifest to avoid this.
3. When you expect default content (e.g., a textarea with instructions), keep `UserPrompt` but also set `PrefillText` so the user starts with a hint.
4. If you need extra explanatory text (before or after the interactive controls), use `TEXT` or `HEADING` components.
5. Keep `Preselect`/`PreselectContentCleanerAgent` flags in `WEB_CONTENT_READER` to simplify the initial UI for the user.

## Useful Resources
- [plugin.lua - Lua Manifest](https://github.com/MindWorkAI/AI-Studio/tree/main/app/MindWork%20AI%20Studio/Plugins/assistants/plugin.lua)
- [AI Studio Repository](https://github.com/MindWorkAI/AI-Studio/)
- [Lua 5.2 Reference Manual](https://www.lua.org/manual/5.2/manual.html)
- [MudBlazor Documentation](https://www.mudblazor.com/docs/overview)
