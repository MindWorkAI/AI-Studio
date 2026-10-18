# AGENTS.md

These instructions cover the model knowledge in `app/MindWork AI Studio/Models/`. They add to the
`AGENTS.md` in the repository root, which applies here as well.

## Model Capabilities

When adding, changing, or removing model knowledge, keep these parts in sync:
- `app/MindWork AI Studio/Models/<Vendor>/<Family>.cs` for the family itself. Creating the class is enough — the source generator in `app/SourceGeneratedMappings/` collects every non-abstract `ModelFamily` and `IModelHost` at compile time, so there is no registration list. Do not add reflection here; `PublishTrimmed` is on.
- `app/Tests/Models/Corpus/` for the model IDs the family covers, marked as either unchanged or expected to change. A porting difference which nobody declared is what the corpus exists to catch.
- `app/MindWork AI Studio/Models/Kinds/` when the change is about what kind of model something is, rather than what it can do. These are ordinary rules of the same engine.
- `app/MindWork AI Studio/Models/Hosting/Hosts/` when a provider wraps model names or cannot pass an API through. A host unwraps and trims the transport; it states nothing about the model itself.
- `app/MindWork AI Studio/Plugins/models/plugin.lua` when a new field can be declared by an organization, and `app/MindWork AI Studio/Plugins/configuration/plugin.lua` when it can be overridden per provider instance.

Rules are never tried in order: specificity is computed from the rule, and two rules of equal specificity on one name fail the test suite. State how a model reasons with `Reasoning(...)` — the three reasoning capabilities are override vocabulary and must never appear in a profile. Every family and every host has to name the page it was read from and the day somebody read it; `dotnet run verify-models` reports the ones which have gone stale.
