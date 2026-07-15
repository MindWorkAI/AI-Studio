# Tool Development

This document explains how model-driven tools are added to AI Studio. Tool calling lets a model request a small, well-defined action during a chat or assistant run, such as searching the web or reading a web page.

Tools are part of the .NET app. They are not Lua plugins and they are not loaded dynamically from user folders. Adding a tool requires code changes.

## Architecture

A tool has two parts:

- A JSON definition in `app/MindWork AI Studio/wwwroot/tool_definitions/`
- A C# implementation of `IToolImplementation` in `app/MindWork AI Studio/Tools/ToolCallingSystem/ToolCallingImplementations/`

At startup, `ToolRegistry` reads all JSON definitions and matches each definition to a registered implementation by `implementationKey`. `ToolExecutor` runs the implementation when a provider returns a matching function call.

The provider only sees tools that are available for the current component, selected by the user or defaults, supported by the model, configured correctly, and allowed by the provider confidence rules. The shared tool-call loop limit is `ToolSelectionRules.MAX_TOOL_CALLS`, and all provider tool-call paths use that same limit.

## Provider API Shapes

The JSON definition in `wwwroot/tool_definitions` is the single source of truth for a tool. Do not create separate tool definition files for different provider APIs. Provider-specific request shapes are generated in code from the same `ToolDefinition`.

Chat Completions compatible APIs use a nested function shape:

```json
{
  "type": "function",
  "function": {
    "name": "get_current_weather",
    "description": "Get the current weather in a given location.",
    "parameters": {},
    "strict": true
  }
}
```

The OpenAI Responses API uses a flat function shape:

```json
{
  "type": "function",
  "name": "get_current_weather",
  "description": "Get the current weather in a given location.",
  "parameters": {},
  "strict": true
}
```

Keep this difference contained in provider adapter code. `ProviderToolAdapters` maps a canonical `ToolDefinition` to the Chat Completions or Responses wire shape. Tool implementations should not know which provider API shape was used.

Tool result handling also differs by API. Chat Completions returns tool calls in `message.tool_calls` and receives results as `role: "tool"` messages. Responses returns `function_call` output items and receives results as `function_call_output` input items correlated by `call_id`. Both paths still execute local tools through `ToolExecutor`, so validation, provider confidence checks, trace formatting, and blocked-call behavior stay shared.

If a tool throws `ToolExecutionBlockedException`, `ToolExecutor` returns the exception message as plain text to the model and records the trace as `BLOCKED`. Other exceptions are logged with details and returned to the model as plain text in the form `Tool execution failed: ...`, with the trace recorded as `ERROR`.

## Definition File

Create one JSON file per tool under `wwwroot/tool_definitions`. The file describes the user-visible tool metadata, optional settings, the function schema sent to the model, and optional per-tool policy guidance injected centrally into the system prompt.

Example:

```json
{
  "schemaVersion": 1,
  "id": "get_current_weather",
  "implementationKey": "get_current_weather",
  "visibleIn": {
    "chat": true,
    "assistants": true
  },
  "settingsSchema": {
    "type": "object",
    "properties": {
      "demoLabel": {
        "type": "string",
        "secret": false
      }
    },
    "required": [
      "demoLabel"
    ]
  },
  "policyInstructions": "Use this tool only when the user asks for current weather conditions.",
  "function": {
    "name": "get_current_weather",
    "description": "Get the current weather in a given location.",
    "strict": true,
    "parameters": {
      "type": "object",
      "properties": {
        "city": {
          "type": "string",
          "description": "The city to find the weather for, e.g. 'San Francisco'."
        },
        "state": {
          "type": "string",
          "description": "The two-letter abbreviation for the state, e.g. 'CA'."
        },
        "unit": {
          "type": "string",
          "description": "The unit to fetch the temperature in.",
          "enum": [
            "celsius",
            "fahrenheit"
          ]
        }
      },
      "required": [
        "city",
        "state",
        "unit"
      ],
      "additionalProperties": false
    }
  }
}
```

Use stable lower-case IDs with underscores. Keep `id`, `implementationKey`, and `function.name` identical unless there is a clear compatibility reason not to.

Keep `function.description` focused on what the tool does. Put sequencing rules, answer-format guidance, or other behavior instructions in `policyInstructions`. When runnable tools are selected, their non-empty policy text is combined centrally and appended to the effective system prompt.

## Implementation

Implement `IToolImplementation` and register the class in `Program.cs` as an `IToolImplementation`.

Example:

```csharp
using System.Text.Json;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class GetCurrentWeatherTool : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool));

    public string ImplementationKey => "get_current_weather";

    public string Icon => Icons.Material.Filled.Cloud;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => TB("Current Weather");

    public string GetDescription() => TB("Use this demo tool to retrieve the current weather for a given city and state."); // this Description is shown to the user

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "demoLabel" => TB("Demo Label"),
        _ => TB(fieldDefinition.Title),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "demoLabel" => TB("Required demo setting for validating tool settings."),
        _ => TB(fieldDefinition.Description),
    };

    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var city = arguments.TryGetProperty("city", out var cityValue) ? cityValue.GetString() ?? string.Empty : string.Empty;
        var state = arguments.TryGetProperty("state", out var stateValue) ? stateValue.GetString() ?? string.Empty : string.Empty;
        var unit = arguments.TryGetProperty("unit", out var unitValue) ? unitValue.GetString() ?? string.Empty : string.Empty;

        if (unit is not ("celsius" or "fahrenheit"))
            throw new ArgumentException($"Invalid unit '{unit}'.");

        return Task.FromResult(new ToolExecutionResult
        {
            TextContent = $"The weather in {city}, {state} is 85 degrees {unit}.",
        });
    }
}
```

Register it:

```csharp
builder.Services.AddSingleton<IToolImplementation, GetCurrentWeatherTool>();
```

The example above is documentation-only. Do not keep demo tools in the production tool catalog.

## Settings And Secrets

Tool settings are stored through `ToolSettingsService`. Plain settings are stored in the regular configuration data. Settings marked with `"secret": true` are stored in the OS keyring through the Rust service.

Use `ValidateConfigurationAsync` when a setting needs more than "required field is present" validation, such as URL syntax, numeric limits, mutually exclusive options, or allowlist parsing.

Use `SensitiveTraceArgumentNames` for model-provided arguments that must not be shown in tool traces. Do not return secrets in `TextContent`, `JsonContent`, exception messages, logs, or trace formatting.

## Security

Treat model-provided tool arguments as untrusted input.

For tools that perform network requests:

- Accept only the schemes and hosts that are required for the feature.
- Validate redirects before following them.
- Do not allow model-supplied URLs to access localhost, loopback, link-local, multicast, or private network targets unless the feature has an explicit policy for that.
- Check `ToolExecutionContext.ProviderConfidence` before returning sensitive data to the model.
- Throw `ToolExecutionBlockedException` for intentional policy blocks so the UI can show the call as blocked instead of failed.

For settings that administrators should be able to manage centrally, add the setting to the appropriate `Settings/DataModel` class, register it with `ManagedConfiguration.Register(...)`, process it in `PluginConfiguration`, clean leftovers in `PluginFactory.Loading`, and document it in `Plugins/configuration/plugin.lua`.

## Checklist

- Add the JSON definition in `wwwroot/tool_definitions`.
- Add the `IToolImplementation` class.
- Register the implementation in `Program.cs`.
- Validate settings and model arguments.
- Protect secrets and sensitive trace arguments.
- Add provider-confidence checks when tool output may contain sensitive data.
- Update configuration plugin documentation when admins can manage the setting.
- Add a changelog entry when users or administrators are affected.
