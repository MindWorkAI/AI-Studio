# Model Capabilities

This document explains how AI Studio knows what a model can do. Every question of the form "may this model take an image", "does it reason", "how much does it read", "is it a chat model at all" is answered in one place: the `Models` namespace in `app/MindWork AI Studio/Models/`.

Ask it through the provider, never through the registry directly:

```csharp
var profile = provider.GetModelProfile();          // a configured provider instance
var profile = llmProvider.GetModelProfile(model);  // a provider and a model, without an instance
```

The first form is the one almost every caller wants because it includes what the person using AI Studio, and what their organization, said about their own installation. Both are cached and cost a dictionary lookup; `ModelProfile` is a struct, so asking during a render loop is fine.

## What A Profile Says

`ModelProfile` carries six things: the capabilities as a `[Flags]` enum, how the model reasons, what kind of model it is, its context window, its tokenizer, and its image limits.

**No number ever means "unknown" by being zero.** `ContextWindow`, `TokenizerRef`, and `ImageLimits` each say so themselves — `IsKnown`, or a `null` in a nullable field. A window of zero tokens is not a thing, but zero images per message is: that is what a vLLM says before anybody raises `--limit-mm-per-prompt`. Read `ModelFactsTests` for what each of them promises.

Reasoning is a field, not a flag. The three capabilities `OPTIONAL_REASONING`, `REASONING_BY_DEFAULT`, and `ALWAYS_REASONING` still exist because they are the vocabulary of the expert settings and of the configuration plugins, but **no rule ever sets them in a profile** — `profile.Reasoning` answers instead, with a value that cannot contradict itself. A test fails when a family reaches for one of the three.

## Where An Answer Comes From

Four sources, in this order, and then nothing. The first one that says something wins for that one detail; everything it stays silent about falls through.

1. **The expert settings of the configured provider.** One person's explicit statement about their own installation. The expert dialog and the `CapabilityOverrides` of a provider in a configuration plugin write into the very same place, so an organization that only wants a different context window needs no model plugin — a number on their provider is enough.
2. **The model list of the provider.** Fetched before every chat round anyway, to check that the selected model still exists, so reading what it already carries costs no request. Only some providers state a window there; see below.
3. **What a model plugin declares.** An organization describing its own models.
4. **The built-in family rules.** What the model card says.
5. Nothing — then the profile says so, and a caller decides what to do without a number.

A model plugin replaces the built-in rules for the names it matches rather than adding to them: it is the whole statement about those models. Anything else would let a modifier nobody was thinking about overrule what an organization wrote down.

## How Priority Is Decided

Rules are **not** tried in order. Each one gets a specificity computed from the rule itself, and the highest wins:

1. an explicit rank, if a rule wrote one down by hand
2. how tightly the pattern binds — exact, then prefix, then whole name parts, then substring
3. how much of the name the pattern spells out
4. how many further name parts the rule requires or forbids
5. whether the rule is tied to a provider, a vendor, or both

So `deepseek-r1` beats `llama` because it says more, and nobody had to decide that it should. This is the whole point of the rebuild: in the previous rules, the Llama block swallowed the DeepSeek distills purely because it stood earlier in the file.

**A tie is a defect, not a coin toss.** Two rules of equal specificity which can match the same name are reported by `ModelFamilyIndex.Ambiguities` and fail the test suite. Resolution still picks the same rule every time, so a build never depends on registration order.

`Rank(rank, reason)` is the emergency exit and is meant to stay unused. It demands the reason in the signature, and refuses a blank one: a number nobody can account for reads as noise, which is what the computation replaced.

## Writing A Family

One class per family in `Models/<Vendor>/<Family>.cs`, under 150 lines. **Creating the class is all it takes** — a source generator collects every non-abstract `ModelFamily` at compile time, so there is no list to remember. `Models/OpenAI/Gpt5Family.cs` is the one to read first.

```csharp
public sealed class AcmeFamily : ModelFamily
{
    public override ModelVendor Vendor => ModelVendor.ACME;

    public override ModelSource Source => new("https://acme.example/docs/models", new DateOnly(2026, 9, 13), "What that page actually says, in a sentence.");

    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("acme-1").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL)
            .ContextWindow(131_072)
            .Tokenizer(TokenizerKind.HUGGING_FACE, "acme/acme-1");

        builder.Rule("acme-1-mini").AsPrefix().Inherits().Removes(Capability.FUNCTION_CALLING);
    }
}
```

The source is abstract, so the compiler asks for it. That is deliberate: a rule without a page behind it is a guess, and a guess nobody can check ages into a defect. Where one page is not enough — capabilities here, the context window there, the tokenizer somewhere else — state the rest in `FurtherSources`; they are held to the same standard.

What a rule can state: `Capabilities`, `Apis`, `Removes`, `Reasoning`, `Kind`, `ContextWindow`, `WithoutContextWindow`, `Tokenizer`, `Images`. What it matches: `AsExact`, `AsPrefix`, `AsSegment`, `AsSubstring`, `AlsoContains`, `NotContains`, `OnlyOn`, `OnlyFrom`.

**Everything left unsaid stays unsaid.** A rule that says nothing about the context window does not claim that nobody knows it; it makes no statement, and whatever else does keeps its answer. `Inherits()` continues from the rule above, `InheritsFrom("<pattern>")` from a named one — worth reaching for as soon as a family has more than one generation, because "the rule above" changes when somebody inserts one.

Patterns are written the way model names arrive: lower case, hyphens between the parts, dots kept. A pattern written any other way can never match anything, so it is a compile-time error (MWAIS0013) rather than a rule that happens to stay quiet. Note that a dot does not end a name part: `gpt-5` as a prefix does not answer for `gpt-5.1`.

`builder.Modifier(...)` states a rule that adjusts an answer instead of choosing the model — `-base` and the like. Selectors compete and exactly one wins; every matching modifier is then applied, least specific first.

For the handful of families whose capabilities are **computed** from the name — Mistral encodes a release date as four digits, Z AI marks its vision models with a "v" behind the version number — override `Refine`. It runs on the family whose rule won. Everything that can be said with a pattern belongs in a pattern, where the specificity can see it.

## Hosts: The Routing Graph

One `IModelHost` per `LLMProviders` value, in `Models/Hosting/Hosts/`. A host does exactly two things:

- **It unwraps a name** until the model underneath is visible, and it may say who built it. Unwrapping is iterative, because wrappings stack: Hugging Face first drops the routing suffix, then the organization prefix.
- **It says what the transport takes away.** A gateway reselling somebody else's model speaks its own dialect: the model may well answer through a vendor-specific API, but not there.

A host that serves other people's models under their plain names unwraps nothing and only trims the transport — the same mechanism, not a special case. This replaced the mutual recursion between vendor rules that nobody could read a route out of.

## Model Kinds

Whether something is a chat model, an embedding model, an image generator, or no model at all is answered by the same engine: the kind markers are ordinary rules, living in `Models/Kinds/`. There is no second normalizer and no second set of string comparisons.

## What Organizations Can Declare

Two surfaces, and they answer different questions:

- **A model plugin** (`PluginType.MODEL`) describes a model wherever it is reached — a fine-tune of your own, a model behind an internal name. It only describes: no endpoint, no key, no code. `app/MindWork AI Studio/Plugins/models/plugin.lua` documents every key with examples.
- **`CapabilityOverrides` on an LLM provider** in a configuration plugin describes **this one installation** of a model. This is the right place for the window an operator actually configured, as opposed to the one the model card advertises.

Both are documented for administrators in `documentation/Enterprise IT.md`. Model plugins are deployed, not imported: a user cannot install one themselves.

## Live Metadata From The Model Lists

Some providers state the context window in the model list they answer with anyway. AI Studio reads it where it is there: OpenRouter (`context_length`), Groq (`context_window`), Mistral (`max_context_length`), the Hugging Face router (per inference provider), and any OpenAI-compatible self-hosted engine that fills `max_model_len`, which vLLM does.

Three things to know when adding another one:

- **A listing describes one installation, never the model as such.** It is kept per configured provider instance and never written to disk. Two machines may serve the same weights behind different windows.
- **Reporting replaces, it never adds.** A model an installation no longer serves has to stop answering. Therefore only report from a call that holds the *whole* list — OpenRouter's embedding route deliberately reports nothing, because it would wipe the windows of the chat models.
- Pass a `listingFactory` to `BaseProvider.LoadModelsResponse` and let `ModelListing.For` drop what cannot be used. Hosts that would need an extra request — Ollama's `/api/show`, LM Studio's `/api/v0/models`, LiteLLM's `/model/info` — are deliberately left out.

## Verification

- **The test project** (`app/Tests/Models/`) owns everything that can be asked of the rules: a corpus of real model IDs per provider, the difference test against the rules this replaced, and the properties every rule has to have — no two rules of equal specificity on one name, every family and host names a page and a day, every pattern in normalized form, no family stating one of the three reasoning words.
- **`dotnet run verify-models`** in `app/Build` reports how long ago somebody last read those pages and names everything older than six months. It warns and never fails, because that answer changes with the calendar rather than with the code.
- **`dotnet run verify`** runs the whole gate, and `dotnet run build` runs it before building. See `documentation/Build.md`.

## Checklist

- Put the family in `Models/<Vendor>/<Family>.cs` and let the source generator find it. Do not add it to a list.
- Name the page and the day it was read, in `Source` and in `FurtherSources`.
- Write every pattern in normalized form, and mind that a dot does not end a name part.
- State reasoning with `Reasoning(...)`, never with one of the three reasoning capabilities.
- State a number only where a page states it. Leaving it out means "nobody knows", which is a usable answer; a made-up number is not.
- Add the models to the corpus in `app/Tests/Models/Corpus/` and say whether the answer is expected to change.
- Use `Refine` only for what a pattern cannot express, and `Rank` only with a reason that says what the computation gets wrong.
- Run `dotnet test`, and `dotnet run verify-models` when you touched sources.
- Add a changelog entry when users or administrators are affected — a new plugin key always affects administrators.
