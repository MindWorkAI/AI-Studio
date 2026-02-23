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
- `HEADING`, `TEXT`, `LIST`: descriptive helpers.

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

# Tips

1. Give every component a unique `Name`— it’s used to track state.
2. Keep in mind that components and their properties are case-sensitive (e.g. if you write `["Type"] = "heading"` instead of `["Type"] = "HEADING"` the component will not be registered). Always copy-paste the component from the `plugin.lua` manifest to avoid this.
3. When you expect default content (e.g., a textarea with instructions), keep `UserPrompt` but also set `PrefillText` so the user starts with a hint.
4. If you need extra explanatory text (before or after the interactive controls), use `TEXT` or `HEADING` components.
5. Keep `Preselect`/`PreselectContentCleanerAgent` flags in `WEB_CONTENT_READER` to simplify the initial UI for the user.

The sample `plugin.lua` in this directory is the live reference. Adjust it, reload the assistant plugin via the desktop app, and verify that the prompt log contains the blocked `context`/`user prompt` pairs that you expect.
