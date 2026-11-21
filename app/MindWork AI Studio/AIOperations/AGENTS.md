# AGENTS.md

These instructions cover the AI operations in `app/MindWork AI Studio/AIOperations/`. They add to the
`AGENTS.md` in the repository root, which applies here as well.

## What an AI operation is

An AI operation is a task which AI Studio hands to a model on its own: one request in, one result out, without
a loop. The text content cleaner, the data source selection and the retrieval context validation of the
classic RAG process, and the security audit of plugins are AI operations. A conversation with the user, a loop
of tool calls, or an agent which plans its own steps is not one.

The common types live in this folder; each operation lives in a folder of its own, `AIOperations/<Name>/`,
with `<Name>Operation`, `<Name>Request`, and its result types.

**Never name a folder after a type of the app.** Its namespace `AIStudio.AIOperations.<Name>` would hide that
type wherever the namespace is in reach (CS0118). The component `DataSourceSelection` exists, so the
operation which selects data sources lives in `DataSourceChoice/`.

## The parts

- **`IAIOperation<TRequest, TResult>`** - `Id` for the log, `Component` for the User-Agent and
  `ChatThread.RuntimeComponent`, `GetRequiredConfidence(request)`, and `RunAsync(request, context, token)`.
  The required confidence comes from the content of the request alone, e.g., its data sources or the
  confidence a chat requires; the minimum of the operation is a setting and not part of it.
- **`AIOperationBase<TRequest, TResult>`** - `AskAsync` sends one request with a chat thread of its own to the
  provider of the context and waits for the whole answer. Right before, it checks the provider once more
  against a prohibition, the global minimum, and the confidence the request requires, but not against the
  minimum of the operation, because a caller may exempt a fallback from it.
- **`AIOperationContext`** - what a call runs with apart from its request, the provider above all.
- **`GetProviderSettings(SettingsManager)`** - every operation declares this public static method, which
  reads its preferred provider and its minimum confidence fresh on every call. It is static, so that a service
  learns which provider would run without an instance. `AIOperationsAreStatelessTests` checks that it exists.

## Thread safety

Every operation is registered as a singleton in `Program.cs` and serves several callers at once, e.g., two
chats with RAG. It therefore keeps nothing of a call: no writable field or property, only the services of
its primary constructor, which are singletons as well. Everything a call needs lives in local variables or
travels with the request and the context. A limit per call, such as the parallel validations of retrieval
contexts, is a local `SemaphoreSlim`. `AIOperationsAreStatelessTests` checks both the state and the
registration.

## Choosing the provider

The caller chooses the provider of a call and passes it in the `AIOperationContext`; an operation never
chooses one itself.

- **`AIOperationProviderChoice.Choose`** decides, a pure function on the facts about each candidate
  (`AIOperationProviderCandidate`) and the confidence levels to reach (`AIOperationProviderRules`). The
  preferred provider of the operation wins when it passes every rule; otherwise the first fallback in the
  order of the caller which passes. A prohibition, the global minimum, the confidence the request requires,
  and the check of the caller apply to every candidate; the minimum of the operation applies to the fallbacks
  only when the caller says so.
- **`AIOperationProviderIssue`** - why a candidate failed, in the order of the checks. The choice reports the
  issue of the preferred provider and, when nothing was chosen, of the first named fallback. Explain a choice
  to the user from these issues; never guess the reason afterward.
- **`AIOperationProviderResolver`** gathers the facts from the settings. Call `Resolve` right before running
  an operation, which logs the choice, and `Preview` for a check in advance or a hint in the UI.
- **`AIOperationProviderOptions`** - what the caller adds: the fallbacks in order, whether the minimum of the
  operation applies to them, and a check of its own, e.g., whether a provider may see the data sources. The
  app-wide default is `SettingsManager.AppDefaultProviderId`.

Build the options of a caller in one place, so that a check in advance counts the providers which run:
`ClassicRagProviderOptions` serves the classic RAG process and `DataSourceService`,
`AssistantPluginAuditService.GetProviderOptions` the audit, and `ReadWebContent.GetCleanerProviderOptions` the
text content cleaner.

## Settings

Each operation has a preferred provider and a minimum confidence in its settings data class, managed through
`ManagedConfiguration` and documented in `Plugins/configuration/plugin.lua`. With a switch such as
`PreselectAgentOptions`, both apply only while it is on, like the settings of the assistants. Add the minimum
to `ComponentsExtensions.MinimumConfidence` as well, so that the provider selections of the settings filter by
it.

## Answers with JSON

Find the JSON in an answer with `ModelAnswerJson` and read it with `AIOperationsJson.OPTIONS`. A new JSON type
goes into `AIOperationsJsonContext` and into `JsonTestRoots.ModelAnswers` in `app/Tests/Serialization/`. Give
a result an explicit way to say that the model gave no usable answer, such as `IsAnswered`, instead of a value
which stands for it.

## Tests

The tests live in `app/Tests/AIOperations/`: the choice as a table (`AIOperationProviderChoiceTests`), the
resolver on real settings (`AIOperationProviderResolverTests`), the statelessness of all operations, and per
operation the options its callers pass (`<Name>ProviderTests`).
