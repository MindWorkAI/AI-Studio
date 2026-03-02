# Assistant Plugin Reference

This folder keeps the Lua manifest (`plugin.lua`) that defines a custom assistant. Treat it as the single source of truth for how AI Studio renders your assistant UI and builds the submitted prompt.

## Structure
- `ASSISTANT` is the root table. It must contain `Title`, `Description`, `SystemPrompt`, `SubmitText`, `AllowProfiles`, and the nested `UI` definition.
- `UI.Type` is always `"FORM"` and `UI.Children` is a list of component tables.
- Each component table declares `Type`, an optional `Children` array, and a `Props` table that feeds the component’s parameters.

Supported types (matching the Blazor UI components):

- `TEXT_AREA`: any user input field with `Name`, `Label`, `UserPrompt`, `PrefillText`, `IsSingleLine`, `ReadOnly`.
- `DROPDOWN`: selects between variants; `Props` must include `Name`, `Label`, `Default`, `Items`, and optionally `ValueType` plus `UserPrompt`.
- `SWITCH`: boolean option; requires `Name`, `Label`, `Value`, `LabelOn`, `LabelOff`, and may include `UserPrompt`.
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

For switches the “value” is the boolean `true/false`; for readers it is the fetched/selected content. Always provide a meaningful `UserPrompt` so the final concatenated prompt remains coherent from the LLM’s perspective.

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
- `input.meta`: per-component metadata keyed by component `Name`
  - `Type` (string, e.g. `TEXT_AREA`, `DROPDOWN`, `SWITCH`)
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
      Type = "<TEXT_AREA|DROPDOWN|SWITCH|WEB_CONTENT_READER|FILE_CONTENT_READER>",
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
    elseif value and value ~= "" then
      table.insert(parts, name .. ": " .. value)
    end
  end
  return table.concat(parts, "\n")
end
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
