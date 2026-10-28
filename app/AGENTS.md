# AGENTS.md

These instructions cover the .NET app: all C#, Razor, and Lua code under `app/`, and everything else
there. They add to the `AGENTS.md` in the repository root, which applies here as well. Several areas below
`app/` have guides of their own; the table "Area guides" in the root `AGENTS.md` lists them.

## .NET App (`app/MindWork AI Studio/`)
**Entry point:** `app/MindWork AI Studio/Program.cs`

Key structure:
- **Program.cs** - Bootstraps Blazor Server, configures Kestrel, initializes encryption and Rust service
- **Provider/** - LLM provider implementations (OpenAI, Anthropic, Google, Mistral, etc.)
  - `BaseProvider.cs` - Abstract base for all providers with streaming support
  - `IProvider.cs` - Provider interface defining capabilities and streaming methods
- **Chat/** - Chat functionality and message handling
- **Assistants/** - Pre-configured assistants (translation, summarization, coding, etc.)
  - `AssistantBase.razor` - Base component for all assistants
- **Agents/** - contains all agents, e.g., for data source selection, context validation, etc.
  - `AgentDataSourceSelection.cs` - Selects appropriate data sources for queries
  - `AgentRetrievalContextValidation.cs` - Validates retrieved context relevance
- **Tools/PluginSystem/** - Lua-based plugin system
- **Tools/Services/** - Core background services (settings, message bus, data sources, updates)
- **Tools/Rust/** - .NET wrapper for Rust API calls
- **Settings/** - Application settings and data models
- **Components/** - Reusable Blazor components
- **Pages/** - Top-level page components

## Important Development Notes

- **Naming conventions** - Constants, enum members, and `static readonly` fields use `UPPER_SNAKE_CASE` such as `MY_CONSTANT`.
- **Message Bus** - Singleton event bus for cross-component communication inside the .NET app
- **Prompt injection reports** - The user hears once per action about what the filter removed, as soon as that action ends. Content which belongs to an action carries its scope from `PromptInjectionGuardService.BeginAction` down to where it is read or filtered; whatever is filtered without a scope is reported right away. Pass the scope as a parameter, never through a field or an `AsyncLocal`: the service serves the whole app, and the message bus delivers to other receivers in the execution context of whoever sends.
- **Paths in stored data** - A stored path to a file which AI Studio keeps below the data directory, such as a transcript or a tokenizer copy, is relative to that directory, so that a backup or a copy of the data directory works on another computer, under another user name, or on another OS. Mark such a property with `[JsonConverter(typeof(DataDirectoryRelativePathConverter))]`, for a positional record parameter with `[property: ...]`; in memory, the path stays absolute. Better still, derive the path from IDs and store none, as `VisualBriefingStore` does. Paths to files of the user stay absolute. Chats stored before v26.10.1 are migrated by a compatibility shim, see `documentation/compatibility-shims/2026-10-relative-data-paths.md`.
- **New files and folders in the data or configuration directory** - Whoever adds a file or folder directly to the data or configuration directory, or below `plugins/` or `databases/` one which needs a decision of its own, lists it in `DataBackupContents.ENTRIES`: as part of a backup, or with the reason a backup leaves it out. A backup takes along only what is listed as included, and a restore replaces only that. An entry included in a backup also goes into `REPLACEABLE_ELEMENTS` in `runtime/src/data_restore.rs`; otherwise the runtime refuses to restore it. The folder panel on the information page shows the list to users who back up these folders with a tool of their own.
- **Sending content on** - Content which leaves one place for another, such as the result of an assistant sent to another assistant or a text sent into the chat, travels as a `TextLaunchPayload` through `AssistantLaunchService`, together with the restrictions of where it was written (`ChatRestrictions`, see `ChatThread.CaptureRestrictions`). Never hand over a bare string: the destination would let any provider or tool see content which its source kept from them. Build the payload with `AssistantLaunchService.CreateTextPayloadAsync`, which filters what an AI wrote for prompt injections before it reaches the next model as a message of the user. An assistant which allows sending to it overrides `AssistantBase.ApplyTextLaunchPayload`, and `SendToReceiverTests` checks that every one does; the restrictions themselves are applied centrally in `AssistantBase`.
- **JSON in model answers** - Read the JSON part of a model answer with `ModelAnswerJson`, never with a scanner of your own. It skips the prose and code fences around the JSON but checks nothing, so broken JSON reaches the deserializer unchanged and its message can go back to the model.
- **Encryption** - Initialized before Rust service is marked ready
- **Debug environment** - Reads `startup.env` file with IPC credentials
- **Static files and the browser cache** - When the developer starts the app from Rider, the embedded browser of Tauri keeps the static files under `wwwroot/`, such as `app.css` or a script, in its cache. The app then still shows the old file, and a change seems not to work. Whenever you changed such a file, tell the developer to clear that cache through the developer tools of the embedded browser before testing. Users of a release are not affected.
- **Production environment** - Runtime launches .NET sidecar with environment variables

## Tests

An assembly-wide `[SetUpFixture]` in `app/Tests/TestHost.cs` fills the static application state that
the app itself only fills while starting up, `Program.LOGGER_FACTORY` above all. Types that
initialize a static logger from it — `Settings.Provider` among them — otherwise die in their type
initializer before the first assertion. Prefer writing new code so that it does not reach for such
statics at all.

## Localization

The app's texts are localized in two steps, and the developer always does the first one.

1. The developer starts the app, which runs the I18N collector, and runs the localization assistant
   in the app for German and US English. Agents never write these initial translations themselves:
   they neither add nor regenerate entries in `app/MindWork AI Studio/Assistants/I18N/allTexts.lua`,
   `app/MindWork AI Studio/Plugins/languages/en-us-97dfb1ba-50c4-4440-8dfa-6575daf543c8/plugin.lua`,
   or `app/MindWork AI Studio/Plugins/languages/de-de-43065dbc-78d0-45b7-92be-f14c2926e2dc/plugin.lua`.
   When new or changed texts are waiting for translation, remind the developer to start the app and
   run the localization.
2. Afterward, agents always review the German translation. Compare the new and changed values of the
   de-de `plugin.lua` with `main`, check them against the wording already established there, and
   correct or improve them directly in that file. `allTexts.lua` and the en-us `plugin.lua` stay as
   the assistant wrote them.

**Moving a `TB()` text into another class gives it a new I18N key**, so its translation is made anew during
the next localization run.

## Plugin System

**Location:** `app/MindWork AI Studio/Plugins/`

Plugins are written in Lua and provide:
- **Language plugins** - I18N translations (e.g., German language pack)
- **Configuration plugins** - Enterprise IT configurations for centrally managed providers, settings
- **Assistant plugins** - custom assistants and direct-chat launchers, subject to approval or a local security audit
- **Model plugins** - what an organization's own models can do, see `documentation/Models.md`

**Example configuration plugin:** `app/MindWork AI Studio/Plugins/configuration/plugin.lua`

**Area guide:** `app/MindWork AI Studio/Tools/PluginSystem/AGENTS.md`, with the three kinds of values a
configuration plugin provides and how to add each of them.

## Tool Calling System

**Documentation:** `documentation/Tools.md`

Selections, `DataTools.DisabledToolIds`, `DataTools.UserDisabledToolIds`, and the minimum provider confidence name tool collections; a tool outside a declared collection forms one under its own ID, and the ID of a tool inside one stands for its whole collection. Normalize a selection with `ToolRegistry.NormalizeSelection`, and expand it into tools with `ToolRegistry.ExpandSelection` only where the view of the model counts.

Build a list of tools for a component with `ToolRegistry.GetCatalogAsync(Components)`, never from the definitions: it leaves out the tools the user switched off. Whether a tool may be used at all is answered by `ToolRegistry.IsToolActive`, which checks what the organization and what the user switched off. A field which lets somebody change a stored selection shows it through `ToolRegistry.LeaveOutSwitchedOffByUser` and stores the change through `ToolRegistry.KeepSwitchedOffByUser`, so a tool switched off only hides and comes back when switched on again. See "Switched-Off Tools" in `documentation/Tools.md`.

Code which decides something on behalf of a request — whether the classic RAG process steps back, say — asks `ToolRegistry.GetOfferBlockReasonAsync` or `ToolRegistry.GetEffectiveRetrievalModeAsync` with the provider settings of the request (`IProvider.CreateSettingsProvider`), never a check of its own: two answers which drift apart leave a chat searching nothing or twice.

**Area guide:** `app/MindWork AI Studio/Tools/ToolCallingSystem/AGENTS.md`, with how to add, change, or
remove a tool and the rules every tool implementation follows.

## Model Capabilities

**Documentation:** `documentation/Models.md`

What a model can do is answered in `app/MindWork AI Studio/Models/`, through `provider.GetModelProfile()`. Never ask `ModelRegistry` directly from a component: the extension method is what adds the expert settings and what a provider's model list reported, and the registry alone answers neither.

**Area guide:** `app/MindWork AI Studio/Models/AGENTS.md`, with how to add, change, or remove model
knowledge.

## RAG (Retrieval-Augmented Generation)

RAG is available as a beta preview feature. Architecture:
- **External Retrieval Interface (ERI)** - Contract for integrating external data sources
- **Data Sources** - Local files and external data via ERI servers
- **Two ways to search** - By default, the chat model searches the data sources itself through the tool `semantic_search`, whenever a question calls for it. The classic process (`AISrcSelWithRetCtxVal`) searches them with every message instead, when the user chose so per chat (`DataSourceOptions.RetrievalMode`) or whenever the tool cannot be offered. `ToolRegistry.GetEffectiveRetrievalModeAsync` decides between the two; pass its answer to `DataSourceService`, because the agents only count as providers that see the data when they actually run. Both ways decide where a chat may still send data once their passages reached it through `DataSourceRequirements.Of`, never with a check of their own: a way which demands less leaves the other one open. See "Searching Data Sources" in `documentation/Tools.md`.
- **Agents** - AI agents select data sources and validate retrieval quality, in the classic process only
- **Embedding providers** - Support for various embedding models
- **Vector database** - Qdrant Edge, embedded in the Rust runtime; see "Databases" below
- **Index database** - SQLite, holding the file fingerprints and the chunk texts for full-text search; see "Databases" below
- **File processing** - Extracts text from PDF, DOCX, XLSX via Rust runtime

### Indexed data sources

Everything AI Studio embeds itself runs through one pipeline in `app/MindWork AI Studio/Tools/Services/Indexing/`,
driven by `DataSourceEmbeddingService`, which queues the runs, prepares each one and owns the statuses. A new
kind of data source plugs into this pipeline instead of building its own.

**Every content path goes through the prompt injection filter.** Files pass the sanitizer of the runtime while
their text is extracted; a new kind of data source needs its own pass through `PromptInjectionGuardService`.
Both report with the scope of the run, `IndexedRunContext.PromptInjectionScope`, so the user hears about a run
once, when it is over, and a message sent meanwhile does not wait for it.

**Area guide:** `app/MindWork AI Studio/Tools/Services/Indexing/AGENTS.md`, with the parts of the
pipeline, how to add a kind of data source, and the rules which are easy to break.

## Databases

`DatabaseClientProvider` is the only way to a client. It caches one per role and guards each role
with its own semaphore, so never construct a client yourself.

**Counts in the UI go through `long.CompactCount()` / `int.CompactCount()`** (`Tools/LongExtensions.cs`),
which shortens anything above 999 to `1.46k` or `4.51M` and formats it with the culture of the
active language plugin. Storage sizes are the exception: they keep using the byte formatters.

**Area guide:** `app/MindWork AI Studio/Tools/Databases/AGENTS.md`, with both stores and what to keep in
mind when working on them, EF Core migrations included.

## Slide Assistant and PowerPoint

The Slide Assistant plans slides for the layouts of a PowerPoint template and writes the
presentation with the Open XML SDK (`DocumentFormat.OpenXml`), in `app/MindWork AI Studio/Tools/PowerPoint/`.

**Area guide:** `app/MindWork AI Studio/Tools/PowerPoint/AGENTS.md`, with how a presentation comes about
and the rules which are easy to break.

## Enterprise IT Support

AI Studio supports centralized configuration for enterprise environments:
- **Registry (Windows)** or **environment variables** (all platforms) specify configuration server URL and ID
- Configuration downloaded as ZIP containing Lua plugin
- Checks for updates every ~16 minutes via ETag
- Allows IT departments to pre-configure providers, settings, and chat templates

**Documentation:** `documentation/Enterprise IT.md`

## Provider Confidence System

Multi-level confidence scheme allows users to control which providers see which data:
- Confidence levels: e.g. `NONE`, `LOW`, `MEDIUM`, `HIGH`, and some more granular levels
- Each assistant/feature can require a minimum confidence level
- Users assign confidence levels to providers based on trust

**Implementation:** `app/MindWork AI Studio/Provider/Confidence.cs`
