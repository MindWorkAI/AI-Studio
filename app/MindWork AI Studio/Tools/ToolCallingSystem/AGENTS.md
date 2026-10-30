# AGENTS.md

These instructions cover the tool calling system in `app/MindWork AI Studio/Tools/ToolCallingSystem/`. They
add to the `AGENTS.md` in the repository root, which applies here as well; its "Tool Calling System"
section explains how selections name tool collections.

## Tool Calling System

When adding, changing, or removing model-driven tools, keep these parts in sync:
- `app/MindWork AI Studio/Tools/ToolCallingSystem/ToolCallingImplementations/` for the `IToolImplementation` class, which states its own `ToolDefinition` through `GetDefinition()`, written with `ToolSettingsSchemaBuilder` for its settings and `ToolParameterSchemaBuilder` for the arguments the model passes. There are no tool definition files; a tool arriving from elsewhere brings an `IToolDefinitionSource` instead.
- `app/MindWork AI Studio/Program.cs` for DI registration of the implementation. Registering it as an `IToolImplementation` is enough, because `CodeToolDefinitionSource` collects the definitions of all of them.
- An `IToolCollection` next to the tools, registered in `Program.cs`, when tools only make sense together.
- `app/MindWork AI Studio/Tools/ToolCallingSystem/ToolSelectionRules.cs` when the shared tool-call limits change. A tool's own minimum provider confidence belongs in its definition, or in the definition of its collection, not here.
- `app/MindWork AI Studio/Tools/ToolCallingSystem/ToolSettingsOptionSources.cs` when a tool setting offers a fixed choice the app maintains, such as languages. Prefer this over spelling the values out in the settings schema; it keeps the list in one place and gives the user translated names.
- `app/MindWork AI Studio/Plugins/configuration/plugin.lua` to document each setting's field name, meaning, and data type. Tool settings need no code to be centrally manageable: an organization addresses them by `"<toolId>.<fieldName>"` in `DataTools.LockedToolSettings` or `DataTools.DefaultToolSettings`.

Tool implementations must treat model-provided arguments as untrusted input. Validate settings and arguments, protect secrets with `SensitiveTraceArgumentNames`, use `ToolExecutionBlockedException` for intentional policy blocks, and check provider confidence before returning sensitive data to the model.

A tool which belongs to a preview feature returns false from `IToolImplementation.IsAvailable` while the preview is switched off; the registry then leaves it out of every list, every request, and the token count, so no component has to check that preview for the tool. Every tool declares in `IToolImplementation.OutboundData` where its arguments go: a chat which holds internal content, such as mails, passages of a data source, a search of the wiki, or an internal web page, keeps the tools whose data goes further than its restriction allows from being offered and from running, see `ToolSelectionRules.IsOutboundDataAllowed`. A tool which brings internal content into the chat raises `ToolExecutionResult.RequiredOutboundDataRestriction`, next to `RequiredProviderConfidence` and `RequiredDataSecurity`, names the source in its `OutboundDataOrigin`, and tells the model beforehand that it may end web search, worded by `WebSearchFirstRule`. "Outbound Data Restrictions" in `documentation/Tools.md` explains the sources and the levels, and "Searching Mailboxes" there the mail tools.

`OutboundData` also decides whether the user confirms a call before it runs, see `ToolApprovalRules` and "Confirming Tool Calls" in `documentation/Tools.md`. Never run a tool around `ToolCallingLoop`, and never let a thread which nobody sees set `ChatThread.RuntimeAsksForToolApproval`: its call would wait for an answer which never comes. Such a run asks once beforehand instead, through `AssistantBase.ApproveToolsForPrivateRunAsync`.

A tool which offers itself from the context of a chat instead of being selected, such as `semantic_search`, sets `Activation = ToolActivation.CONTEXT` and tailors its function to each request in `ResolveFunctionAsync`.

A tool needs nothing of its own to be switched off by the user or by an organization: `ToolRegistry.IsToolActive` checks both `DataTools.UserDisabledToolIds` and `DataTools.DisabledToolIds` for every tool, and the organization comes first. Keep it that way; a check which asks only one of the two lists lets a switched-off tool through. "Switched-Off Tools" in `documentation/Tools.md` explains the rest.
