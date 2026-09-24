# Tool Development

This document explains how local model-driven tools are added to AI Studio. Tool calling lets a model request a small, well-defined action during a chat or assistant run, such as searching the web or reading a web page.

Tools are currently part of the .NET app. They are currently not Lua plugins and they are currently not loaded dynamically from user folders. Adding a tool currently requires code changes.

A tool is a single `IToolImplementation` class in `app/MindWork AI Studio/Tools/ToolCallingSystem/ToolCallingImplementations/`, registered in `Program.cs`. It states what it is through `GetDefinition()` and does what it promises in `ExecuteAsync`. There are no tool definition files and no schema to keep in sync by hand, so this document carries only what the code cannot tell you: how the provider APIs differ, the rules a tool has to follow, and the obligations that come with returning content from outside AI Studio. For the shape of a tool, read `WebSearchTool` and `ReadWebPageTool`.

The provider only sees local tools that are

- available for the current component and
- selected by the user or defaults and
- supported by the model and
- configured correctly and
- allowed by the provider confidence rules.

## Provider API Shapes

A tool states its function once, in its `ToolDefinition`, and the adapters generate each API's request shape from it. What differs is naming and nesting: Chat Completions compatible APIs put the function under a `function` object, the OpenAI Responses API takes the same fields flat, and the Anthropic messages API calls the schema `input_schema` and nests nothing. Keep that difference inside `ProviderToolAdapters`; a tool implementation never learns which shape was used.

Adding a provider API means writing an `IToolCallingProviderAdapter`, not another loop. The existing ones are `ChatCompletionToolCallingAdapter` and `ResponsesToolCallingAdapter` in `Provider/OpenAI/`, and `AnthropicToolCallingAdapter` in `Provider/Anthropic/`. They translate between one provider API's wire format and the loop that drives every provider, `ToolCallingLoop`. Replacing that loop — for an agent mode, say — means another `IToolCallingLoop`; the adapters stay as they are.

### Optional Parameters Are Written The Ordinary Way

`Function.Parameters` is plain JSON Schema: an optional argument is simply absent from `required`. That is what `ToolParameterSchemaBuilder` writes, and Anthropic reads it as written.

OpenAI's strict mode wants it differently. It insists that **every** property appear in `required`, so an argument that may be left out has to say so by allowing null instead — `"type": ["string", "null"]`, plus `null` among its enum values where it has any. `OpenAIStrictToolSchema.FromToolParameters` therefore converts on the way out, but only where strict mode is kept. Nothing is lost there, because a tool treats an absent argument and a null one the same way.

Strict mode is a promise of the host, not of the definition: only a host which binds the model's output to the schema keeps it. OpenAI does so in both of its APIs, and Anthropic does so without needing any conversion. Anywhere else, the converted schema would only tell the model that every argument is required. Groq, which validates a tool call against the schema without binding the model to it, then rejects each call that leaves an optional argument out, and other models fill the gap with placeholders such as `0` that the tool has to refuse. Every other Chat Completions host therefore gets the schema as written, with `strict: false`. A host which does bind tool calls says so by passing `enforcesStrictToolSchemas: true` to `StreamOpenAICompatibleChatCompletion`, as `ProviderOpenAI` does, next to the page that documents it. `ToolFunctionDefinition.Strict` works the other way round: set to false, it keeps a tool out of strict mode everywhere, for a schema strict mode cannot express.

So the canonical schema is provider-neutral, and the provider that wants something else translates away from it in its own adapter. That is where the next such conversion belongs too — not in the definition.

Tool result handling also differs by API, and this is what the adapters exist for.

- **Chat Completions** returns tool calls in `message.tool_calls` and receives results as `role: "tool"` messages, one per result. A missing tool call ID can be supplied by AI Studio, because the ID only has to match between our request and our answer.
- **Responses** returns `function_call` output items and receives results as `function_call_output` input items correlated by `call_id`. There the ID comes from the provider, so a call without one cannot be answered at all and ends the conversation. The whole output of a round has to be sent back for the next one, reasoning items included.
- **Anthropic** works in content blocks: the model's turn is one assistant message whose blocks may mix `text`, `thinking`, and `tool_use`, and it has to be returned unchanged — thinking blocks in particular. All results of a round belong in a **single** user message as `tool_result` blocks; splitting them across several messages teaches the model to stop asking for more than one tool at a time. It is also the only one of the three with an error flag on a result (`is_error`), which the harness sets for failed and blocked calls.

AI Studio currently executes local tool calls sequentially. Therefore, Chat Completions requests with tools always set `parallel_tool_calls` to `false`, limiting each model response to at most one tool call. Requests without tools omit the parameter, and additional API parameters cannot override this behavior. Models can still request additional tools across subsequent responses.

The OpenAI Responses API may continue to return multiple function calls in one response. AI Studio processes those calls sequentially as well; concurrent execution of separate local tool calls is not currently implemented. This does not restrict concurrency used internally by an individual tool.

Provider-native tools are separate from local function tools and do not have a `ToolDefinition` or an `IToolImplementation`. The local tool calling implementation does not influence the provider-native tool selection at all.

If a tool throws `ToolExecutionBlockedException`, `ToolExecutor` returns the exception message as plain text to the model and records the trace as `BLOCKED`. Other exceptions are logged with details and returned to the model as plain text in the form `Tool execution failed: ...`, with the trace recorded as `ERROR`.

## Writing A Tool

User-visible names, descriptions, and icons come from the implementation's own members, never from the definition — only those can be translated.

Use stable lower-case IDs with underscores, and keep `Id`, `ImplementationKey`, and `Function.Name` identical unless there is a clear compatibility reason not to. Give every argument and setting name a constant that the schema and the reading code share: the two then cannot drift apart.

`VisibleIn.AllowedComponents` and `VisibleIn.DeniedComponents` are optional lists of `Components` values; a value outside the enum makes the definition invalid. When both lists are empty, the `Chat` and `Assistants` flags apply. As soon as either list has an entry, the lists replace those flags: an empty allow list starts by allowing every component, a non-empty allow list allows only its entries, and the deny list is applied last and always wins.

Keep `Function.DescriptionForLLM` focused on what the tool does. This value is mapped to the provider's function `description` field and is only shown to the LLM. Put sequencing rules, answer-format guidance, or other behavior instructions in `SystemPromptInstructions`. When runnable tools are selected, their non-empty policy text is combined centrally and appended to the effective system prompt.

A setting offering a fixed choice takes it from an option source — `RequiredChoice` and `OptionalChoice` name a list the app maintains, see `ToolSettingsOptionSources` — or spells its values out in the field's `enum` list, which is how a definition arriving as data offers a choice of its own. The two are mutually exclusive, and `ToolRegistry` rejects a definition that uses both or names an unknown source. Check a stored value in `ValidateConfigurationAsync` either way: it can predate the current list or arrive from an organization's configuration.

When a tool returns data that future messages must only send to providers at or above a specific confidence level, set `ToolExecutionResult.RequiredProviderConfidence`. AI Studio persists the highest requirement reached by the chat and applies it to later provider checks. Being listed in `DataSourceSecuritySettings.TrustedProviderIds` does not meet that requirement: the list belongs to data-source security checks, not to confidence. An organization which wants a contractually covered provider to continue such chats raises its level through `DataConfidence.CustomConfidenceScheme`.

## Security

Treat model-provided tool arguments as untrusted input. Refuse a wrong one rather than guessing what it meant: a placeholder such as `0` is not a page, and reading it as "no page" does something the model did not ask for. The model reads the refusal and tries again, so the message has to name the argument and the value that arrived, say what would be valid, and, for an optional argument, that leaving it out is always possible. `WebSearchTool` shows the pattern.

For tools that perform network requests:

- Accept only the schemes and hosts that are required for the feature.
- Validate redirects before following them.
- Do not allow model-supplied URLs to access localhost, loopback, link-local, multicast, or private network targets unless the feature has an explicit policy for that.
- Check `ToolExecutionContext.ProviderConfidence` before returning sensitive data to the model.
- Throw `ToolExecutionBlockedException` for intentional policy blocks so the UI can show the call as blocked instead of failed.

Use `SensitiveTraceArgumentNames` for model-provided arguments that must not be shown in tool traces. Do not return secrets in `TextContent`, `JsonContent`, exception messages, logs, or trace formatting.

### Content Fetched From Outside AI Studio

A tool that returns content it fetched from outside AI Studio must filter it for prompt injections before the model sees it, and must declare `IToolImplementation.ReturnsUntrustedExternalContent`.

Filter every field that reaches the model, not only the main content. A page title, a description, an author name from a meta tag, and a publication date are all written by whoever controls the page, and a search engine's result title is written by whoever ranks for the query. Anything the tool puts into `TextContent`, `JsonContent`, or `Sources` counts.

`PromptInjectionGuardService` performs the filtering. Use the overload taking a list of `PromptInjectionText` for a tool call that produces several texts: it filters them in one runtime request and reports them to the user as one event, grouped by source, instead of once per field. Texts from the same page must share one `PromptInjectionSource.WebContent(url)` so the report names the page rather than its fields.

For web pages, `WebPageContentSanitizer` already does this for the fields of an `ExtractedWebPage`; `web_search` and `read_web_page` both go through it. Filter after truncating the content, not before: only the text that actually reaches the model needs checking, and a page can be far larger than what a tool returns.

Filtering never rejects content. When the runtime cannot be reached, the text is passed through unchanged and the user is warned, because failing the user's request over a best-effort check would cost them their work. Do not build a tool that depends on the filter having run.

The prompt-level warning in `systemPromptInstructions` — that everything a tool brings back from outside is untrusted working material — complements this but does not replace it: a model can be talked out of following an instruction, so it is not a security boundary.

## Reading Web Pages

`web_search` and `read_web_page` both load pages, and so does the `ReadWebContent` component the assistants offer. All three go through `WebPageRetrievalService` — every page AI Studio reads goes through that one service. It validates DNS results and every redirect target before connecting, binds the connection to the validated addresses, and caps the response size.

The service reads HTML pages and text documents. An HTML page has its main content extracted and converted to Markdown. A text document — plain text, JSON, XML, YAML, CSV, and similar formats — comes back as the server sent it, with only its line endings unified; `RetrievedWebPage.ContentKind` tells the two apart, which matters because a short text document is complete while a short extracted page usually is not. Binary content such as PDFs or images is refused as soon as the response headers arrive, before its body is downloaded. Both kinds go through the same prompt-injection filter, after truncation, in every caller; the runtime's filter also decodes the escapes of JSON and XML, such as `\u0049` or `&#73;`, because a model reads them as the characters they stand for.

What differs between callers is which targets are acceptable, and that follows from who chose the URL. `web_search` uses the public-only policy and never reads private, loopback, or link-local targets. `read_web_page` may reach an explicitly allowed private host, and only for a High-confidence provider. The `ReadWebContent` component sets `TargetChosenByUser`, which lifts the target restrictions entirely: the user typed the address, so their own network and a local server are legitimate. Never set that flag for a URL that reached AI Studio through a model.

`read_web_page` remains the independent single-URL tool and may use its configured private-host allowlist and operating-system sign-in behavior for allowed HTTPS targets. An allowed private host can only be read by a High-confidence provider.

`search_confluence` builds a CQL query for the configured HTTPS Confluence Data Center site's `dosearchsite.action` page and loads it through `WebPageRetrievalService`, the same reader used by `read_web_page`. The model supplies a search phrase and optionally a space key, never a URL or CQL expression. The tool returns the extracted search page as Markdown with links, after truncation and prompt-injection filtering, and lists the search page as its source. Every request, redirects included, must stay within the configured base URL; `WebPageRetrievalOptions.IsTargetAllowed` refuses a redirect before it is followed, so the query never reaches another host. The operating-system sign-in goes to the configured host only when all its addresses are private, the same rule `read_web_page` follows, and a redirect to Confluence's login page is reported as a missing sign-in instead of an empty search. The tool is offered to High-confidence providers only and checks that again before each search, because a lowered tool setting must not let internal wiki content reach a less trusted provider; the result raises the chat's continuing confidence requirement to High. Selecting `search_confluence` also selects `read_web_page` so the model can load a result's full content; the latter tool's private-host allowlist and other availability rules still apply, so a wiki with a private address has to be in that allowlist before any result opens.

Confluence Cloud is not supported yet. It offers neither `dosearchsite.action` as a server-rendered page nor the operating-system sign-in; its search needs Confluence's REST API with an API token instead, which is also the way to stop depending on the HTML of the Data Center search page.

Every successfully retrieved page with readable content is also returned as a structured tool source, using the final URL after redirects and the extracted page title. The provider collects these sources across local tool calls and attaches them to the final response under the separate “Sources used by tools” heading. Failed, blocked, empty, and duplicate retrievals do not add sources — a pattern worth copying for any tool that returns material the user may want to check.

## Checklist

- Add the `IToolImplementation` class, including its `GetDefinition()`.
- Register the implementation in `Program.cs`.
- Put every argument and setting name in a constant that the schema and the reading code share.
- Set `MinimumProviderConfidence` to what the tool actually exposes.
- Mark a setting the tool cannot work without as `Required`, rather than saying so in its description.
- Validate settings and model arguments, and refuse a wrong argument with a message the model can correct itself from.
- Filter content fetched from outside AI Studio for prompt injections, and declare `ReturnsUntrustedExternalContent`.
- Protect secrets and sensitive trace arguments.
- Add provider-confidence checks when tool output may contain sensitive data.
- Document each setting's field name, meaning, and data type in `Plugins/configuration/plugin.lua`, so administrators can manage it.
- Add a changelog entry when users or administrators are affected.
