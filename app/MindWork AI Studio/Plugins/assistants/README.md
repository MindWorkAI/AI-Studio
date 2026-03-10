# Assistant Plugin Reference

This folder keeps the Lua manifest (`plugin.lua`) that defines a custom assistant. Treat it as the single source of truth for how AI Studio renders your assistant UI and builds the submitted prompt.

## Structure
- `ASSISTANT` is the root table. It must contain `Title`, `Description`, `SystemPrompt`, `SubmitText`, `AllowProfiles`, and the nested `UI` definition.
- `UI.Type` is always `"FORM"` and `UI.Children` is a list of component tables.
- Each component table declares `Type`, an optional `Children` array, and a `Props` table that feeds the component’s parameters.

Supported types (matching the Blazor UI components):

- `TEXT_AREA`: user input field based on `MudTextField`; requires `Name`, `Label`, and may include `HelperText`, `HelperTextOnFocus`, `Adornment`, `AdornmentIcon`, `AdornmentText`, `AdornmentColor`, `Counter`, `MaxLength`, `IsImmediate`, `UserPrompt`, `PrefillText`, `IsSingleLine`, `ReadOnly`, `Class`, `Style`.
- `DROPDOWN`: selects between variants; `Props` must include `Name`, `Label`, `Default`, `Items`, and optionally `ValueType` plus `UserPrompt`.
- `SWITCH`: boolean option; requires `Name`, `Label`, `Value`, and may include `Disabled`, `UserPrompt`, `LabelOn`, `LabelOff`, `LabelPlacement`, `Icon`, `IconColor`, `CheckedColor`, `UncheckedColor`, `Class`, `Style`.
- `COLOR_PICKER`: color input based on `MudColorPicker`; requires `Name`, `Label`, and may include `Placeholder`, `ShowAlpha`, `ShowToolbar`, `ShowModeSwitch`, `PickerVariant`, `UserPrompt`, `Class`, `Style`.
- `PROVIDER_SELECTION` / `PROFILE_SELECTION`: hooks into the shared provider/profile selectors.
- `WEB_CONTENT_READER`: renders `ReadWebContent`; include `Name`, `UserPrompt`, `Preselect`, `PreselectContentCleanerAgent`.
- `FILE_CONTENT_READER`: renders `ReadFileContent`; include `Name`, `UserPrompt`.
- `IMAGE`: embeds a static illustration; `Props` must include `Src` plus optionally `Alt` and `Caption`. `Src` can be an HTTP/HTTPS URL, a `data:` URI, or a plugin-relative path (`plugin://assets/your-image.png`). The runtime will convert plugin-relative paths into `data:` URLs (base64).
- `HEADING`, `TEXT`, `LIST`: descriptive helpers.

Images referenced via the `plugin://` scheme must exist in the plugin directory (e.g., `assets/example.png`). Drop the file there and point `Src` at it. The component will read the file at runtime, encode it as Base64, and render it inside the assistant UI.

## Prompt Assembly
Each component exposes a `UserPrompt` string. When the assistant runs, `AssistantDynamic` iterates over `RootComponent.Children` and, for each component that has a prompt, emits:

```
context:
<UserPrompt>
---
user prompt:
<value extracted from the component>
```

For switches the “value” is the boolean `true/false`; for readers it is the fetched/selected content; for color pickers it is the selected color text (for example `#FFAA00` or `rgba(...)`, depending on the picker mode). Always provide a meaningful `UserPrompt` so the final concatenated prompt remains coherent from the LLM’s perspective.

### Advanced: BuildPrompt (optional)
If you want full control over prompt composition, define `ASSISTANT.BuildPrompt` as a Lua function. When present, AI Studio calls it and uses its return value as the final user prompt. The default prompt assembly is skipped.

#### Contract
- `ASSISTANT.BuildPrompt(input)` must return a **string**.
- If the function is missing, returns `nil`, or returns a non-string, AI Studio falls back to the default prompt assembly.
- Errors in the function are caught and logged, then fall back to the default prompt assembly.

#### Input table shape
The function receives a single `input` table with:
- `input.fields`: values keyed by component `Name`
  - Text area, dropdown, and readers are strings
  - Switch is a boolean
  - Color picker is the selected color as a string
- `input.meta`: per-component metadata keyed by component `Name`
  - `Type` (string, e.g. `TEXT_AREA`, `DROPDOWN`, `SWITCH`, `COLOR_PICKER`)
  - `Label` (string, when provided)
  - `UserPrompt` (string, when provided)
- `input.profile`: selected profile data
  - `Id`, `Name`, `NeedToKnow`, `Actions`, `Num`
  - When no profile is selected, values match the built-in "Use no profile" entry

#### Table shapes (quick reference)
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
```

#### Using `meta` inside BuildPrompt
`input.meta` is useful when you want to dynamically build the prompt based on component type or reuse existing UI text (labels/user prompts).

Example: iterate all fields with labels and include their values
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

Example: handle types differently
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

Example:
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
  - 
Example:
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

Example:
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

#### Using `profile` inside BuildPrompt
Profiles are optional user context (e.g., "NeedToKnow" and "Actions"). You can inject this directly into the user prompt if you want the LLM to always see it.

Example:
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

### Included lua libraries
- [Basic Functions Library](https://www.lua.org/manual/5.2/manual.html#6.1)
- [Coroutine Manipulation Library](https://www.lua.org/manual/5.2/manual.html#6.2)
- [String Manipulation Library](https://www.lua.org/manual/5.2/manual.html#6.4)
- [Table Manipulation Library](https://www.lua.org/manual/5.2/manual.html#6.5)
- [Mathematical Functions Library](https://www.lua.org/manual/5.2/manual.html#6.6)
- [Bitwise Operations Library](https://www.lua.org/manual/5.2/manual.html#6.7)

### Logging helpers (assistant plugins only)
The assistant runtime exposes basic logging helpers to Lua. Use them to debug custom prompt building.

- `LogDebug(message)`
- `LogInfo(message)`
- `LogWarn(message)`
- `LogError(message)`

Example:
```lua
ASSISTANT.BuildPrompt = function(input)
  LogInfo("BuildPrompt called")
  return input.fields.Text or ""
end
```

### Date/time helpers (assistant plugins only)
Use these when you need timestamps inside Lua.

- `DateTime(format)` returns a table with date/time parts plus a formatted string.
  - `format` is optional; default is `yyyy-MM-dd HH:mm:ss` (ISO 8601-like).
  - `formatted` contains the date in your desired format (e.g. `dd.MM.yyyy HH:mm`) or the default.
  - Members: `year`, `month`, `day`, `hour`, `minute`, `second`, `millisecond`, `formatted`.
- `Timestamp()` returns a UTC timestamp in ISO-8601 format (`O` / round-trip), e.g. `2026-03-02T21:15:30.1234567Z`.

Example:
```lua
local dt = DateTime("yyyy-MM-dd HH:mm:ss")
LogInfo(dt.formatted)
LogInfo(Timestamp())
LogInfo(dt.day .. "." .. dt.month .. "." .. dt.year)
```

#### Example: simple custom prompt
```lua
ASSISTANT.BuildPrompt = function(input)
  local f = input.fields
  return "Topic: " .. (f.Topic or "") .. "\nDetails:\n" .. (f.Details or "")
end
```

#### Example: structured prompt (similar to Coding assistant)
```lua
ASSISTANT.BuildPrompt = function(input)
  local f = input.fields
  local parts = {}

  if (f.Code or "") ~= "" then
    table.insert(parts, "I have the following code:")
    table.insert(parts, "```")
    table.insert(parts, f.Code)
    table.insert(parts, "```")
    table.insert(parts, "")
  end

  if (f.CompilerMessages or "") ~= "" then
    table.insert(parts, "I have the following compiler messages:")
    table.insert(parts, "```")
    table.insert(parts, f.CompilerMessages)
    table.insert(parts, "```")
    table.insert(parts, "")
  end

  table.insert(parts, "My questions are:")
  table.insert(parts, f.Questions or "")
  return table.concat(parts, "\n")
end
```

# Tips

1. Give every component a unique `Name`— it’s used to track state.
2. Keep in mind that components and their properties are case-sensitive (e.g. if you write `["Type"] = "heading"` instead of `["Type"] = "HEADING"` the component will not be registered). Always copy-paste the component from the `plugin.lua` manifest to avoid this.
3. When you expect default content (e.g., a textarea with instructions), keep `UserPrompt` but also set `PrefillText` so the user starts with a hint.
4. If you need extra explanatory text (before or after the interactive controls), use `TEXT` or `HEADING` components.
5. Keep `Preselect`/`PreselectContentCleanerAgent` flags in `WEB_CONTENT_READER` to simplify the initial UI for the user.
