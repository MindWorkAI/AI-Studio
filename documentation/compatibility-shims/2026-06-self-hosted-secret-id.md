# Self-Hosted Provider Secret ID

- Status: Active
- Introduced: 2026-06-19
- Remove after: 2026-12-19
- Code references:
  - `app/MindWork AI Studio/Tools/Services/RustService.APIKeys.cs`
  - `app/MindWork AI Studio/Provider/LLMProvidersExtensions.cs`

## User Impact

Some self-hosted provider API keys were stored under a localized OS keyring namespace. In German installations this could produce entries using `Selbst gehostet`, while the fixed canonical namespace is `Self-hosted`.

Without this shim, affected users may see an invalid or missing API key warning until they manually enter the key again.

## Compatibility Behavior

AI Studio uses `Self-hosted` as the canonical secret namespace. For a limited time, API key reads, writes, and deletes also consider the known German legacy namespace `Selbst gehostet`.

When a legacy entry is found, AI Studio stores the same encrypted API key under the canonical namespace and deletes the legacy entry. If the canonical entry already exists, AI Studio also attempts to delete the known legacy alias.

This applies to LLM provider, embedding provider, and transcription provider API keys, including enterprise configuration plugin namespaces.

## Removal Checklist

- Remove `LEGACY_SELF_HOSTED_SECRET_ID_DE`.
- Remove `LegacySelfHostedAPIKeys`.
- Remove legacy lookup, migration, and cleanup calls from API key read, write, and delete paths.
- Keep `LLMProvidersExtensions.ToSecretId()` and the canonical `Self-hosted` namespace.
- Update this document's status to `Removed`.
- Add a changelog entry only if removal is user-visible.