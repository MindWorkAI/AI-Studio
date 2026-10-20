# Tool Development

This document explains how local model-driven tools are added to AI Studio. Tool calling lets a model request a small, well-defined action during a chat or assistant run, such as searching the web or reading a web page.

Tools are currently part of the .NET app. They are currently not Lua plugins and they are currently not loaded dynamically from user folders. Adding a tool currently requires code changes.

A tool is a single `IToolImplementation` class in `app/MindWork AI Studio/Tools/ToolCallingSystem/ToolCallingImplementations/`, registered in `Program.cs`. It states what it is through `GetDefinition()` and does what it promises in `ExecuteAsync`. There are no tool definition files and no schema to keep in sync by hand, so this document carries only what the code cannot tell you: how the provider APIs differ, the rules a tool has to follow, and the obligations that come with returning content from outside AI Studio. For the shape of a tool, read `WebSearchTool` and `ReadWebPageTool`; for a tool which offers itself and describes the chat it is offered in, read `SemanticSearchTool`.

The provider only sees local tools that are

- present in this installation, which a tool of a preview feature only is while the preview is switched on, and
- available for the current component and
- selected by the user or defaults, or offering themselves from the context of the chat, and
- supported by the model and
- configured correctly and
- allowed by the provider confidence rules and
- allowed by the outbound data restriction of the chat and
- able to offer something in this request.

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

AI Studio currently executes local tool calls sequentially. Therefore, Chat Completions requests with tools set `parallel_tool_calls` to `false`, limiting each model response to at most one tool call. Requests without tools omit the parameter, and additional API parameters cannot override this behavior. Models can still request additional tools across subsequent responses.

The exception is a provider which rejects the parameter. Hugging Face answers it with a bad request, so its provider passes `mayAskForSequentialToolCalls: false` to `StreamOpenAICompatibleChatCompletion`, and its requests omit the parameter even when they offer tools. Its models may then ask for several calls in one response, which the loop works through one by one, checking the limits per call. Not every provider honors the parameter either — OpenRouter let Qwen ask for two calls at once — and such a response is handled the same way.

The OpenAI Responses API may continue to return multiple function calls in one response. AI Studio processes those calls sequentially as well; concurrent execution of separate local tool calls is not currently implemented. This does not restrict concurrency used internally by an individual tool.

AI Studio offers no provider-native tools, i.e., tools which the provider runs on its own servers, such as OpenAI's hosted web search. That search used to go along with every Responses API request, whatever the user had selected. Merely offering it cost about 4,300 input tokens per request — in a tool calling loop, per round — and it bypassed the rules our own tools follow: the selection below the message field, and the outbound data restriction of a chat which read a mailbox, see `ToolOutboundData`. Nor did it show up in the tool trace. A provider-native tool belongs into the tool selection instead, as a choice the user makes, so that the selection, the minimum provider confidence, and the outbound data restriction apply to it. Its results never reach AI Studio, though, so the prompt injection filter cannot check them; whoever adds such a tool has to decide how to deal with that.

If a tool throws `ToolExecutionBlockedException`, `ToolExecutor` returns the exception message as plain text to the model and records the trace as `BLOCKED`. Other exceptions are logged with details and returned to the model as plain text in the form `Tool execution failed: ...`, with the trace recorded as `ERROR`.

## Writing A Tool

User-visible names, descriptions, and icons come from the implementation's own members, never from the definition — only those can be translated.

Use stable lower-case IDs with underscores, and keep `Id`, `ImplementationKey`, and `Function.Name` identical unless there is a clear compatibility reason not to. Give every argument and setting name a constant that the schema and the reading code share: the two then cannot drift apart.

A tool which belongs to a preview feature returns false from `IToolImplementation.IsAvailable` while the preview is switched off. The registry then leaves it out of every list, the settings and the selections included, out of every request, where `CheckToolAsync` reports `NOT_AVAILABLE_HERE`, and out of the token count below the message field. A selection which names it keeps it, so the tool comes back with the preview. The property is asked whenever tools are listed, so keep it cheap; the mail tools ask `MailboxRetrievalService.AreMailboxesEnabled`.

`VisibleIn.AllowedComponents` and `VisibleIn.DeniedComponents` are optional lists of `Components` values; a value outside the enum makes the definition invalid. When both lists are empty, the `Chat` and `Assistants` flags apply. As soon as either list has an entry, the lists replace those flags: an empty allow list starts by allowing every component, a non-empty allow list allows only its entries, and the deny list is applied last and always wins.

Keep `Function.DescriptionForLLM` focused on what the tool does. This value is mapped to the provider's function `description` field and is only shown to the LLM. Put sequencing rules, answer-format guidance, or other behavior instructions in `SystemPromptInstructions`. When runnable tools are selected, their non-empty policy text is combined centrally and appended to the effective system prompt.

When those instructions follow one of the tool's settings, register the ones of its default and word the current ones in `IToolImplementation.ResolveSystemPromptInstructionsAsync`. The registry asks for them with every request, after all checks and only when the tool has a function to offer; a tool which throws there is left out of the request, the same as with `ResolveFunctionAsync`. Everything outside a request reads the registered instructions, the token count below the message field among it. `read_web_page` words its instructions this way for its free address choice, see below.

A setting offering a fixed choice takes it from an option source — `RequiredChoice` and `OptionalChoice` name a list the app maintains, see `ToolSettingsOptionSources` — or spells its values out in the field's `enum` list, which is how a definition arriving as data offers a choice of its own. The two are mutually exclusive, and `ToolRegistry` rejects a definition that uses both or names an unknown source. Check a stored value in `ValidateConfigurationAsync` either way: it can predate the current list or arrive from an organization's configuration.

When a tool returns data that future messages must only send to providers at or above a specific confidence level, set `ToolExecutionResult.RequiredProviderConfidence`. AI Studio persists the highest requirement reached by the chat and applies it to later provider checks. Being listed in `DataSourceSecuritySettings.TrustedProviderIds` does not meet that requirement: the list belongs to data-source security checks, not to confidence. An organization which wants a contractually covered provider to continue such chats raises its level through `DataConfidence.CustomConfidenceScheme`.

`ToolExecutionResult.RequiredDataSecurity` is the other axis: a result from a data source which may only be used with self-hosted providers sets it to `SELF_HOSTED`, and the chat refuses every other provider from then on. Both only ever tighten, see `ChatThread.RequireProviderConfidence` and `ChatThread.RequireDataSecurity`, so raise them for what actually reached the model, not for everything the tool looked at. A search which found nothing brought nothing into the chat.

The third requirement is where the chat may still send data. Mails come from strangers and may carry instructions meant for the AI, so each mailbox decides it, see `OutboundDataRestriction`. A tool which brings content of a mailbox into the chat sets `ToolExecutionResult.RequiredOutboundDataRestriction`, and the chat keeps the strictest level reached, together with the mailbox which demanded it, see `ChatThread.RequireOutboundDataRestriction`. Every tool therefore declares where its arguments go in `IToolImplementation.OutboundData`:

| Value | Where the data goes |
|---|---|
| `NONE` | Nowhere beyond AI Studio and the provider of the model. |
| `CONFIGURED_SERVICE` | To a service configured in AI Studio, such as the wiki of the organization, an ERI server, or the embedding provider of a data source. |
| `THIRD_PARTY_QUERIES` | Queries the model writes go to a service somebody else runs, such as a web search engine. |
| `MODEL_CHOSEN_ADDRESSES` | The tool contacts addresses the model chooses; the address alone can carry data out. A tool which says nothing counts as this, the most open kind. |

Below `UNRESTRICTED`, only the first two may run, see `ToolSelectionRules.IsOutboundDataAllowed`. The registry does not offer the others (`ToolOfferBlockReason.OUTBOUND_DATA_RESTRICTED`), and `ToolExecutor` checks again before each call, because a mail tool can tighten the chat in the middle of a request. A tool which can tell allowed destinations from others on its own sets `EnforcesOutboundDataRestriction`. It is then offered on every level and has to refuse what goes too far on every call, as `read_web_page` does, see below.

A result in `JsonContent` reaches the model the way `ToolExecutionResult.ToModelContent` writes it, which escapes only what JSON requires. Umlauts and other characters outside ASCII stay as they are rather than costing six characters each.

## Tools Which Offer Themselves

Most tools are selected: by the user, by the defaults of a component, by a chat template, or by the rules of an assistant. A tool whose use follows from the chat instead sets `Activation = ToolActivation.CONTEXT`. Nobody can select such a tool, so it appears in no selection. `ToolRegistry.GetCatalogAsync` leaves it out of every list built for a component, and `ToolRegistry.NormalizeSelection` drops it from a selection which names it anyway, such as the one of a chat template. The tool list of the app settings still shows it, so that an organization can switch it off or raise the confidence it requires. `semantic_search` is the only such tool so far.

The registry takes every context tool of the component as a candidate and runs it through the same checks as a selected one. A tool which passes them is then asked what it offers in this request, through `IToolImplementation.ResolveFunctionAsync`. Most tools leave that method alone and offer the function they registered. A tool which has to know the chat first returns a function tailored to it, or null when it has nothing to offer, and is then left out of the request. Only the description and the parameters of the answer count; the name and the strict mode stay as registered, because the calls of the model find their tool by its name. A resolution which throws costs that one tool and no other.

Two rules come with it:

- **Keep the answer stable while the chat stays the same**, down to the order of what it lists. The providers cache a request from its beginning, and the tools are part of that beginning.
- **Keep it cheap.** It runs before every request. Whatever a resolution has to fetch from elsewhere belongs in a short-lived cache, the way `DataSourceDescriptionService` keeps the descriptions of ERI data sources for five minutes.

Whoever decides something on behalf of a request asks the registry rather than checking for itself. `ToolRegistry.GetOfferBlockReasonAsync` answers whether a tool can be offered to a provider in a component, with the same checks in the same order as the request, and names what is in the way otherwise. Ask it with the provider settings the request uses, `IProvider.CreateSettingsProvider`, or the two can come to different answers. On purpose, it cannot tell whether a tool has something to offer right now: only the request can answer that.

### Paging Without State

A tool whose results come in pages takes a `page` argument starting at 1 and reports `has_more` with each result, never a total. A total is often unknown — a vector search has none, since every chunk matches, only less closely — and a number known for some sources and not for others invites the model to page through all of them. `semantic_search` works this way; `web_search` takes a `page` as well.

Paging stays stateless: every call brings its query and its page again. It has to, because tool results do not travel into later turns. `ToolInvocationTrace.Result` is not saved, and the tool conversation of a request is gone once the answer stands. Cap how deep a model may page, since every page fetches its whole window again, and refuse a page beyond the cap with the last page there is in the message.

## Tool Collections

Some tools only make sense together. Searching mails without being able to read the ones found gets in the way, and reading them with less trust than the search asks for would protect nothing. Such tools form a collection: people select it as one entry, it needs one minimum provider confidence, and an organization switches it off as one. The model still sees each of its tools on its own and calls each by its name.

A collection is an `IToolCollection` class next to its tools, registered in `Program.cs`. It states its `ToolCollectionDefinition`: its ID, the IDs of its tools in the order they are listed, its minimum provider confidence, and a description for a model which picks the tools of an assistant. Its name, description, and icon come from its own members, so they can be translated. Whether it exists right now follows from its tools: it disappears with the last of them, e.g., while their preview is switched off. `ToolRegistry` registers collections after the tools and leaves out what it cannot accept: a collection taking the ID of a tool, a tool which is not registered or which offers itself from the context of a chat, and a tool another collection claimed first.

Every tool belongs to exactly one collection. A tool which belongs to no declared collection forms one of its own under its own ID. That is why the settings which used to name tools need no migration: `DisabledToolIds`, `MinimumProviderConfidenceByToolId`, the defaults of the components, chat templates, document analysis policies, and assistant plugins all name collections now, and the ID of such a tool is the ID of its collection. The ID of a tool in a declared collection stands for its whole collection wherever it appears. A selection stored before the tool joined the collection selects the collection, and an organization which names one tool of a collection switches the whole collection off or raises its confidence. Of several levels set for a collection and its tools, the highest applies, and the minimum a tool declares itself does not count once it belongs to a collection.

Three methods of the registry translate between both views:

- `ToolRegistry.NormalizeSelection` turns a selection into the collections which run. Every place which shows or stores a selection calls it.
- `ToolRegistry.ExpandSelection` turns a selection into the tools which run. Preparing a request, counting its tokens, the security card of an assistant plugin, and its audit use it, because they are about what the model reads.
- `ToolRegistry.GetCollectionId` names the collection of a tool.

The settings of a tool stay with the tool, also inside a collection. An organization addresses them by `"<toolId>.<fieldName>"`, and the settings dialog of a collection shows one section per tool. `mailboxes` is the only declared collection so far, with `search_mails`, `read_mail`, and `count_mails`.

## Security

Treat model-provided tool arguments as untrusted input. Refuse a wrong one rather than guessing what it meant: a placeholder such as `0` is not a page, and reading it as "no page" does something the model did not ask for. The model reads the refusal and tries again, so the message has to name the argument and the value that arrived, say what would be valid, and, for an optional argument, that leaving it out is always possible. `ToolArgumentReader` reads strings, positive integers, and values out of a fixed choice, alone or as a list, and words the refusals so; `WebSearchTool` shows how a tool uses it.

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

`PromptInjectionGuardService` performs the filtering. Use the overload taking a list of `PromptInjectionText` for a tool call that produces several texts: it filters them in one runtime request and reports them to the user as one event, grouped by source, instead of once per field. Texts from the same page must share one `PromptInjectionSource.WebContent(url)` so the report names the page rather than its fields. A tool which filters in several steps begins one action with `PromptInjectionGuardService.BeginAction` and hands its scope to each step, as `semantic_search` does; whatever is filtered without a scope is reported right away.

For web pages, `WebPageContentSanitizer` already does this for the fields of an `ExtractedWebPage`; `web_search` and `read_web_page` both go through it. Filter after truncating the content, not before: only the text that actually reaches the model needs checking, and a page can be far larger than what a tool returns.

Filtering never rejects content. When the runtime cannot be reached, the text is passed through unchanged and the user is warned, because failing the user's request over a best-effort check would cost them their work. Do not build a tool that depends on the filter having run.

The prompt-level warning in `systemPromptInstructions` — that everything a tool brings back from outside is untrusted working material — complements this but does not replace it: a model can be talked out of following an instruction, so it is not a security boundary.

## Reading Web Pages

`web_search` and `read_web_page` both load pages, and so does the `ReadWebContent` component the assistants offer. All three go through `WebPageRetrievalService` — every page AI Studio reads goes through that one service. It validates DNS results and every redirect target before connecting, binds the connection to the validated addresses, and caps the response size.

The service reads HTML pages and text documents. An HTML page has its main content extracted and converted to Markdown. A text document — plain text, JSON, XML, YAML, CSV, and similar formats — comes back as the server sent it, with only its line endings unified; `RetrievedWebPage.ContentKind` tells the two apart, which matters because a short text document is complete while a short extracted page usually is not. Binary content such as PDFs or images is refused as soon as the response headers arrive, before its body is downloaded, unless the caller allows documents, see "Reading Documents" below. Both kinds go through the same prompt-injection filter, after truncation, in every caller; the runtime's filter also decodes the escapes of JSON and XML, such as `\u0049` or `&#73;`, because a model reads them as the characters they stand for.

The page extraction removes frames, objects, and embeds, because they usually hold videos, ads, or maps. One which recognizably holds a document, by its declared media type, by the file name in its address, or by an address in a parameter of a viewer such as PDF.js, becomes a link first, see `EmbeddedDocumentLinks`. An intranet page often shows its forms and guidelines that way, and without the link the model could neither name the document nor open it while the free address choice is off.

What differs between callers is which targets are acceptable, and that follows from who chose the URL. `web_search` uses the public-only policy and never reads private, loopback, or link-local targets. `read_web_page` may reach an explicitly allowed private host, and only for a High-confidence provider. The `ReadWebContent` component sets `TargetChosenByUser`, which lifts the target restrictions entirely: the user typed the address, so their own network and a local server are legitimate. Never set that flag for a URL that reached AI Studio through a model.

`read_web_page` remains the independent single-URL tool and may use its configured private-host allowlist and operating-system sign-in behavior for allowed HTTPS targets. An allowed private host can only be read by a High-confidence provider. A page read from a private address, and every page of the configured wiki, is internal and restricts where the chat may send data afterwards, see "Outbound Data Restrictions" below.

`search_confluence` builds a CQL query for the configured HTTPS Confluence Data Center site's `dosearchsite.action` page and loads it through `WebPageRetrievalService`, the same reader used by `read_web_page`. The model supplies a search phrase and optionally a space key, never a URL or CQL expression. The query uses the `siteSearch` field, which Confluence's own search box sends: it finds pages holding any of the words and ranks them by relevance, while the documented `text` field requires all of them and misses pages the search box finds. The tool returns the extracted search page as Markdown with links, after truncation and prompt-injection filtering, and lists the search page as its source. Every request, redirects included, must stay within the configured base URL; `WebPageRetrievalOptions.IsTargetAllowed` refuses a redirect before it is followed, so the query never reaches another host. The operating-system sign-in goes to the configured host only when all its addresses are private, the same rule `read_web_page` follows, and a redirect to Confluence's login page is reported as a missing sign-in instead of an empty search. The tool is offered to High-confidence providers only and checks that again before each search, because a lowered tool setting must not let internal wiki content reach a less trusted provider; the result raises the chat's continuing confidence requirement to High. Selecting `search_confluence` also selects `read_web_page` so the model can load a result's full content; the latter tool's private-host allowlist and other availability rules still apply, so a wiki with a private address has to be in that allowlist before any result opens. A successful search restricts where the chat may send data afterwards, at the level `read_web_page` applies to internal pages, see "Outbound Data Restrictions" below.

`search_intranet` asks the search REST API of the configured SharePoint Server intranet, `_api/search/query`, through `WebPageRetrievalService` as well, with the same rules for the operating-system sign-in and for redirects, which must stay within the configured base URL. The model supplies plain words; the tool removes the characters of SharePoint's query language, such as colons, quotes, and parentheses, so the words cannot become property restrictions, and writes a single quote twice inside the string of the API. The page reader asks for HTML first and XML second, so SharePoint usually answers in Atom XML; the tool reads that and both JSON forms, verbose and plain, and only the relevant results, not promoted results or refiners. Each hit returns its title, a link, the excerpt without SharePoint's highlighting marks, its file type, the date of its last change, and its site, all filtered for prompt injections in one request; a hit without an HTTP address, such as one of a file share, is left out. The links of documents in libraries lead to the file itself, which `read_web_page` opens. An answer which is no search result, such as a sign-in page, is reported as such instead of as a search without hits. Like `search_confluence`, the tool is offered to High-confidence providers only, brings `read_web_page` along, and raises the chat to High; it restricts where the chat may send data only when the search returned hits, since a search without hits brought nothing internal into the chat. Its intranet is a service configured in AI Studio for `read_web_page` just as the wiki is: pages and documents below its base URL are internal whatever their address, and an address the model chose there has to stay within the intranet. SharePoint Online is not supported yet, because it does not accept the operating-system sign-in.

Both searches read their best hits right away, `readTopHits` of them (default 3, at most 10, 0 switches it off), through `SearchHitReader`. A chat may call `ToolSelectionRules.MAX_TOOL_CALLS` tools per answer, and a search followed by one `read_web_page` per hit used them up before a question which needs several searches was answered; `web_search` reads its hits itself for the same reason. Only hits within the searched site are read, with the request options of the search itself, so every request and redirect stays within the site and the operating-system sign-in goes nowhere else. `search_confluence` finds its hits in the HTML of the search page by the class `search-result-link` of their title links, or, where a version marks none, by the first link of each element of the class `search-result`. The hits share `maxHitContentCharacters` (default 30,000) equally; a page is cut to its share and a document while it is read, and the hit is then marked `partial`. Documents follow `read_web_page.documentAccess` and `read_web_page.maxDocumentMegabytes`. All hits pass the prompt-injection filter in one request, so the user hears of one search. A hit which cannot be read is reported with its reason and costs that hit only. Every hit which was read becomes a source of its own. The instructions tell the model to answer from that content and to open further hits with `read_web_page` only when needed.

Confluence Cloud is not supported yet. It offers neither `dosearchsite.action` as a server-rendered page nor the operating-system sign-in; its search needs Confluence's REST API with an API token instead, which is also the way to stop depending on the HTML of the Data Center search page.

Every successfully retrieved page with readable content is also returned as a structured tool source, using the final URL after redirects and the extracted page title. The provider collects these sources across local tool calls and attaches them to the final response under the separate “Sources used by tools” heading. Failed, blocked, empty, and duplicate retrievals do not add sources — a pattern worth copying for any tool that returns material the user may want to check.

### Reading Documents

`read_web_page` also reads documents: PDF, Word (DOCX), PowerPoint (PPTX), Excel and other spreadsheets, and OpenDocument files. A caller allows them through `WebPageRetrievalOptions.Documents`; `web_search` and the `ReadWebContent` component do not, so they keep refusing binary content.

- **Where from.** `read_web_page.documentAccess` decides: `OFF`, `INTERNAL_ONLY`, the default, or `ALL_ADDRESSES`. Internal means what restricts a chat as an internal page: read from a non-public address, or within the configured wiki, whatever its address, see `ReadWebPageTool.IsInternalAddress`. `WebDocumentOptions.IsAllowed` decides once the headers of the final response have arrived, so a refused document costs nothing but its headers, and the refusal names no address. The outbound data restriction and the free address choice apply to a document exactly as to a page.
- **Confidence.** An internal document requires a High-confidence provider, checked before the download, and raises the chat to High. That holds for a wiki on a public address too, whose pages read by `read_web_page` do not raise it.
- **Memory and disk.** The body streams into a file of `WebDocumentFiles`, in `webDocuments/` of the data folder, capped at `read_web_page.maxDocumentMegabytes` (default 50, at most 200). The file has a random name without extension and is deleted in `WebPageRetrievalService.RetrieveAsync` whether reading succeeded, failed, or was cancelled. Files older than an hour, which only a crash leaves behind, go before the next download.
- **Format.** Whether a response is a document follows from its media type: anything which is not HTML or a text format. What kind of document it is, the runtime decides by the content alone, since the file has no extension; it refuses programs and formats it cannot read, such as legacy `.doc` files.
- **Prompt injections.** The runtime filters the text while it streams it, and the result names the address of the document. The tool filters its title and the text it returns once more, like the fields of a page.
- **Length.** `RustService.ReadArbitraryFileData` keeps no more than `maxContentCharacters` characters and marks a result cut there. It still reads the rest and throws it away, because the runtime reports what its filter removed only at the end. The tool returns such a document as `partial` with a warning; paging through a long document is not supported yet.

### Free Address Choice

`read_web_page.freeAddressChoice` decides whether the model may read addresses it chose itself. `OFF`, the default, tells the model to read only URLs which appear word for word in the conversation: in the system prompt, in a user message with the documents and data source content it carries, or in a tool result. When none fits and no other tool can find one, the model asks the user. `ON` lets it choose addresses as well. The values are the members of `FreeAddressChoice`, offered through `ToolSettingsOptionSources.FREE_ADDRESS_CHOICE`, and the setting follows the usual precedence of tool settings: a locked organization value, then the user's saved value, then an organization default.

`OFF` is enforced as well: `read_web_page` refuses an address which was not given to the model, see `ChatThread.IsWebAddressGivenToTheModel`. Given means that the address stands in the system prompt the last request was sent with, in a user message or a document attached to it, or in the result of a tool. What the model wrote itself never counts, its earlier answers included. Each source is collected where its text exists in full: the system prompt in `PrepareSystemPrompt`, the attached documents in `ContentText.PrepareTextContentForAI`, since they are read from disk only when a message is sent, and the tool results in `ToolExecutor`. Relative links need no care of their own, because the page extraction makes every link absolute before the model sees it. Two addresses count as the same when they ask the server for the same, see `WebAddresses.CreateRequestKey`: scheme and host regardless of case, path and query exactly, the fragment not at all. A redirect may go anywhere, since the server rather than the model chose it.

An address in a tool result counts only when it is no echo of the call: one which stands in the arguments, even as a part of one, is left out, because Semantic Search returns its query, and the model could otherwise turn any address it makes up into one a tool returned. The tool results are kept for the session only, like the results themselves, so after a restart a chat knows fewer addresses, never more. A refusal never repeats the address, for the same reason. On top of this, the network target restrictions of `WebPageRetrievalService` and the prompt-injection filter apply with both values.

Links in a tool result count as given with both values, a link on a page read before included. Searching and then reading what was found is what these tools are for, and `search_confluence` opens its hits that way. Before this setting existed, the instructions forbade following a link which only retrieved content mentioned; that rule was dropped on purpose. Following a link word for word cannot carry anything out of the conversation. Putting parts of the conversation into an address could, so the instructions forbid that with both values.

The instructions depend on the setting, so `read_web_page` words them per request through `ResolveSystemPromptInstructionsAsync`. Its registered instructions are those of `OFF`, and the token count below the message field counts with them. With `ON`, a request carries a shorter instruction, and the count comes out a few tokens high.

## Outbound Data Restrictions

Once internal content is in a chat, every tool which runs afterwards could carry it out: a web search in its query, `read_web_page` in the address it reads. The chat therefore records where it may still send data, `ChatThread.RequiredOutboundDataRestriction`, an `OutboundDataRequirement` with a level and the source which demands it. It only ever gets stricter, it is saved with the chat, and a later change of a setting does not loosen it, because the content was read under the old one. Four sources set it, each through `ToolExecutionResult.RequiredOutboundDataRestriction`, and only on a result which brought content in:

- A mailbox, at its own level, see "Searching Mailboxes" below.
- A search of the Confluence wiki with `search_confluence`.
- A search of the intranet with `search_intranet` which returned hits.
- An internal page read with `read_web_page`: one read from a non-public address, or one of the configured wiki or intranet, whatever its address. Only the page as it was read in the end counts, after every redirect. A public address which redirects into the intranet restricts the chat; an intranet address which redirects to a public page, such as a sign-in page, does not.

The wiki, the intranet, and the internal pages share one setting, `read_web_page.internalPageOutboundDataRestriction`, which defaults to `ONLY_LINKS_FROM_CHAT`. The search loads a wiki page through the same reader, and the model opens its hits with `read_web_page` right after, so one setting gives the chat one level, and an organization one setting to lock. The tool blocked in the tool selection, and a call which was turned down, name the source: the mailbox, the wiki, the intranet, or the host of the page, see `OutboundDataRequirementExtensions.GetToolBlockedMessage`.

What each level allows, see `ToolSelectionRules.IsOutboundDataAllowed`:

- `UNRESTRICTED` allows every tool.
- `ONLY_LINKS_FROM_CHAT` keeps every tool from running which sends queries to a third party, such as `web_search`. `read_web_page` still reads the addresses given to the model, by the same rule as the free address choice, and the services configured in AI Studio.
- `ONLY_CONFIGURED_SERVICES` allows the services configured in AI Studio only, not even an address the user wrote.

The services configured in AI Studio are the mailboxes, the Confluence wiki, the intranet of `search_intranet`, and the hosts matching `read_web_page.allowedPrivateHosts`. Without a wiki, an intranet, and allowed hosts, `read_web_page` offers nothing on the strictest level.

**Why an address counts as sending data.** A model which writes an address can put anything it read into it, in the path or the query, and the server at that address receives it. This is why `read_web_page` declares `ToolOutboundData.MODEL_CHOSEN_ADDRESSES` and keeps to the restriction itself, see `ReadWebPageTool.IsAllowedByOutboundDataRestriction`. An address given in the chat, by the user or in the result of a tool, is read word for word, so it carries nothing the model chose. Where a redirect leads was chosen by the server, so an address given in the chat may be redirected anywhere.

**What an address the model chose may reach.** A wiki page has to stay in the wiki, redirects included, through `WebPageRetrievalOptions.IsTargetAllowed`. A page on an allowed private host has to stay on non-public addresses, on every request, through `WebPageRetrievalOptions.NonPublicTargetsOnly`: a pattern such as `*.example.org` matches public hosts as well, so a matching name is not enough. The wiki is checked first, so a wiki on an allowed host stays confined to the wiki. This holds for chats restricted by a mailbox as well, which may therefore send mail content to an allowed host in an address the model chose, as they already could to the wiki.

A call has to pass both the restriction and the free address choice, so with the choice switched off, a page of the wiki or of an allowed host counts only when its address was given to the model, e.g., as a hit of a wiki search. The instructions of `read_web_page` name what is left on the level of the chat, and a refusal never repeats the address.

**The order of the tools.** A chat which still may search the web loses that as soon as internal content enters it. Only the tool which brings such content in knows when it does, so `search_mails`, `search_confluence`, and `read_web_page` each tell the model so in their own instructions, and tell it to search the web first when a task needs both. `WebSearchFirstRule` words that rule once for all of them; `search_mails` speaks for the whole mailbox collection, which is always offered together. The tools word their instructions per request and leave the rule out when the request does not offer `web_search`, because a rule about a tool the model does not get would only confuse it. They learn this from `ToolResolutionContext.CandidateToolIds`, the tools which passed every check of the request, which `ToolRegistry.GetRunnableToolsAsync` fills before it asks the first tool what it offers. `search_confluence` also leaves the rule out when the level of internal pages is `UNRESTRICTED`, and `read_web_page` also without a wiki and without allowed private hosts, because it cannot reach an internal page then. A mailbox may have any level, so `search_mails` says that reading it may end the web search. Once the search is gone, no instruction mentions it any more: a call of `web_search` in the same request is refused with the text of `OutboundDataRequirementExtensions.GetToolBlockedMessage`, which names the source and says that a new chat can search again, and the tool selection shows the user the same text.

**Residual risk.** On `ONLY_LINKS_FROM_CHAT`, the model cannot write addresses, but it still chooses which of the given links to follow. A crafted page with many links could leak a few bits that way, limited by `ToolSelectionRules.MAX_TOOL_CALLS`. Organizations which do not accept this lock the level to `ONLY_CONFIGURED_SERVICES`.

**Out of scope.** Files the user attaches or pastes are content the user brings in on purpose and restrict nothing. Neither do `semantic_search` and the classic RAG process yet.

## Searching Data Sources

`semantic_search` lets the model search the data sources of a chat itself, with a query it works out from the conversation, whenever a question calls for it. The classic RAG process, `AISrcSelWithRetCtxVal`, searches them with every message instead, using the message as the query. One place decides which of the two runs, `ToolRegistry.GetEffectiveRetrievalModeAsync`. Semantic search is the default, and the user can choose the other way per chat through `DataSourceOptions.RetrievalMode`. Whenever the tool cannot be offered — a model without tool calling, tools or this tool switched off, a provider below a confidence the organization set for it — the classic process searches instead. That process steps back only when the answer is semantic search, so a chat never ends up searching nothing.

The tool offers the data sources of the chat which the provider may use. With the automatic selection switched on, those are all data sources the provider may use: the AI which selects is the chat model itself. No agent takes part, so `DataSourceService` counts only the chat provider when it decides what may be used, see `DataSourceRetrievalMode`. The description of the tool lists the offered data sources by ID, name, kind, page size, and last page, with their own description shortened to 500 characters. The description of an ERI data source comes from its server, so it goes through the prompt-injection filter before it gets there. The listed IDs are the only values `data_source_ids` accepts.

The data sources are checked again before each search, since rounds may have passed since they were offered, and then searched in parallel. A data source which fails is reported as not searched rather than left out, so that the model does not take its silence for finding nothing. Every passage goes through the same filter and into the same shape as with the classic RAG process, `IRetrievalContext.AsMarkdown`, which gets the scope of one `PromptInjectionGuardService.BeginAction` per search, so that the user hears about what was filtered once per search, as soon as it is done. The result holds whole passages up to 100,000 characters, and the data sources take turns: first the best passage of each, then the second best of each. Otherwise, the data source listed first would take the whole budget. What does not fit is counted in the result, with a narrower query as the way out. The passages become sources through `IRetrievalContext.ToSources()`, as with the classic process, and only the data sources whose passages reached the model raise the requirements of the chat.

## Searching Mailboxes

`search_mails`, `read_mail`, and `count_mails` form the collection `mailboxes`, behind the preview `PRE_MAILBOXES_2026` on top of `PRE_RAG_2024`. None of them asks a mail server anything. They read the local index which `MailboxIndexer` keeps, through `MailboxRetrievalService`. Mailboxes are kept in a list of their own, `Data.Mailboxes`, so `semantic_search` and the classic RAG process never see one.

- `search_mails` searches with a `query` by meaning and by words, and returns each mail once with the passage which matched best. Without a query, it lists the mails meeting the conditions, the most recently received first. Results come in pages per mailbox, so a page after the first needs exactly one mailbox. All mailboxes share a budget of 100,000 characters and take turns in it.
- `read_mail` reads one mail in pages of 30,000 characters, an attachment by its number, and the header block on request. It names the mail this one replies to, when that one is in the index.
- `count_mails` counts with the same conditions, by folder or by sender on request, and adds how many mails the folders hold on the server.

The index holds the sent mails and the drafts as well. Without a root folder, they belong to the whole mailbox anyway; with one, `DataSourceMailbox.IncludeSentAndDrafts` adds them from outside it, see `MailFolderSelection`. Drafts are indexed whatever their age, like flagged mails. `search_mails` and `count_mails` take `special_folder` with `sent` or `drafts`, which `MailConditions` resolves by what the server marks a folder as (`MailFolderRecord.SpecialUse`), never by its name, since that differs by server and language. Results mark such mails with `special_folder` as well, so the model can tell what the user wrote from what the user received.

Asking for drafts starts a sync of the mailbox when its last complete sync is older than a minute, see `MailDraftSync`: a user who saved a draft a moment ago and asks the AI to improve it expects the AI to find it, while the next sync at the interval may be a quarter of an hour away. `DataSourceEmbeddingService.RequestMailboxSyncAsync` queues it as `DataSourceEmbeddingRefreshMode.TOOL_REQUEST`, which keeps to the automatic refresh setting and never signs in despite a refused sign-in. The result says that drafts may be missing or outdated until the sync is done. The model cannot save a draft; it proposes the improved text in its answer.

The tools offer the mailboxes which `MailboxRetrievalService.GetReadableMailboxes` returns. The chat provider and the embedding provider both have to meet the level of a mailbox, because the embedding provider receives the query, which the model may have written from a mail. A mailbox requires a level from `VERY_LOW` to `HIGH`; `NONE`, `UNTRUSTED`, and `UNKNOWN` would let almost every provider through, so they close the mailbox instead. A mailbox on a server the organization does not allow is left out as well, see `MailServerPolicy`. Each call checks again, since rounds may have passed since the tools were offered, and `read_mail` finds a mail only in the mailboxes the provider may read.

Mails are written by others. Everything of a mail which reaches the model, from the subject and the addresses to the passages and the names of attachments, goes through one `PromptInjectionGuardService` batch per call, as `PromptInjectionSource.MailContent`. Only the mailboxes whose content reached the model raise the requirements of the chat; of several, the strictest restriction wins, and the first mailbox demanding it is named. An organization's `DataMailboxes.MinimumOutboundDataRestriction` tightens a mailbox whose own level is less strict. Found and read mails become sources under `mailbox://<mailbox ID>/<mail ID>`, which the sources list shows as text without a link, see `SourceExtensions.IsMailSource`. AI Studio cannot read an encrypted mail, only its header, and the instructions tell the model to say so rather than guess.

Logs name a mailbox and its ID, never a subject, a sender or recipient, a folder, or an attachment. `ToolExecutor` logs the message of every exception, so a mail tool must not throw with such a value either. A `folder` or a `special_folder` the mailboxes do not know is therefore answered in the result, together with the folders to choose from, rather than refused by an exception.

## Checklist

- Add the `IToolImplementation` class, including its `GetDefinition()`.
- Register the implementation in `Program.cs`.
- When the tool belongs to a preview feature, return false from `IsAvailable` while the preview is switched off.
- Put every argument and setting name in a constant that the schema and the reading code share.
- Set `MinimumProviderConfidence` to what the tool actually exposes.
- When the tool only makes sense together with others, put them into a tool collection, and set the minimum provider confidence there.
- Mark a setting the tool cannot work without as `Required`, rather than saying so in its description.
- Validate settings and model arguments, and refuse a wrong argument with a message the model can correct itself from.
- Filter content fetched from outside AI Studio for prompt injections, and declare `ReturnsUntrustedExternalContent`.
- Protect secrets and sensitive trace arguments.
- Add provider-confidence checks when tool output may contain sensitive data, and raise `RequiredProviderConfidence`, `RequiredDataSecurity`, and `RequiredOutboundDataRestriction` for what actually reached the model.
- Declare where the tool sends data in `OutboundData`. Set `EnforcesOutboundDataRestriction` only when the tool refuses what goes too far itself, on every call.
- For a tool which offers itself from the context of the chat, set `Activation = ToolActivation.CONTEXT` and return null from `ResolveFunctionAsync` when there is nothing to offer. Keep a tailored function stable for the same chat, and cache what it fetches.
- When the system prompt instructions follow a setting, register those of the default and word the current ones in `ResolveSystemPromptInstructionsAsync`.
- Page with `page` and `has_more`, not with a total, and cap how deep the model may go.
- Document each setting's field name, meaning, and data type in `Plugins/configuration/plugin.lua`, so administrators can manage it.
- Add a changelog entry when users or administrators are affected.
