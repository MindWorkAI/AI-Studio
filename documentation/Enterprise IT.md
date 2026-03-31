# Enterprise IT

## Overview
Do you want to manage MindWork AI Studio in a corporate environment or within an organization? This documentation explains what you need to do and how it works. First, here's an overview of the entire process:

- You can distribute MindWork AI Studio to employees' devices using tools like Microsoft System Center Configuration Manager (SCCM).
- Employees can get updates through the built-in update feature. If you want, you can disable automatic updates and control which version gets distributed.
- AI Studio checks about every 16 minutes to see where and which configuration it should load. This information is loaded from the local system. On Windows, you might use the registry, for example.
- If it finds the necessary metadata, AI Studio downloads the configuration as a ZIP file from the specified server.
- The configuration is an AI Studio plugin written in Lua.
- Any changes to the configuration apply live while the software is running, so employees don’t need to restart it.

AI Studio checks about every 16 minutes to see if the configuration ID, the server for the configuration, or the configuration itself has changed. If it finds any changes, it loads the updated configuration from the server and applies it right away.

## Configure the devices
So that MindWork AI Studio knows where to load which configuration, this information must be provided as metadata on employees' devices. Currently, the following options are available:

- **Windows Registry / GPO**: On Windows, AI Studio first tries to read the enterprise configuration metadata from the registry. This is the preferred option for centrally managed Windows devices.

- **Policy files**: AI Studio can read simple YAML policy files from a system-wide directory. On Linux and macOS, this is the preferred option. On Windows, it is used as a fallback after the registry.

- **Environment variables**: Environment variables are still supported on all operating systems, but they are now only used as the last fallback.

### Source order and fallback behavior

AI Studio does **not** merge the registry, policy files, and environment variables. Instead, it checks them in order:

- **Windows:** Registry -> Policy files -> Environment variables
- **Linux:** Policy files -> Environment variables
- **macOS:** Policy files -> Environment variables

For enterprise configurations, AI Studio uses the **first source that contains at least one valid enterprise configuration**.

For the encryption secret, AI Studio uses the **first source that contains a non-empty encryption secret**, even if that source does not contain any enterprise configuration IDs or server URLs. This allows secret-only setups during migration or on machines that only need encrypted API key support.

### Multiple configurations (recommended)

AI Studio supports loading multiple enterprise configurations simultaneously. This enables hierarchical configuration schemes, such as organization-wide settings combined with institute- or department-specific settings.

The preferred format is a fixed set of indexed pairs:

- Registry values `config_id0` to `config_id9` together with `config_server_url0` to `config_server_url9`
- Environment variables `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID0` to `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID9` together with `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL0` to `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL9`
- Policy files `config0.yaml` to `config9.yaml`

Each configuration ID must be a valid [GUID](https://en.wikipedia.org/wiki/Universally_unique_identifier#Globally_unique_identifier). Up to ten configurations are supported per device.

If multiple configurations define the same setting, the first definition wins. For indexed pairs and policy files, the order is slot `0`, then `1`, and so on up to `9`.

### Windows registry example

The Windows registry path is:

`HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`

Example values:

- `config_id0` = `9072b77d-ca81-40da-be6a-861da525ef7b`
- `config_server_url0` = `https://intranet.example.org/ai-studio/configuration`
- `config_id1` = `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
- `config_server_url1` = `https://intranet.example.org/ai-studio/department-config`
- `config_encryption_secret` = `BASE64...`

This approach works well with GPOs because each slot can be managed independently without rewriting a shared combined string.

### Policy files

#### Windows policy directory

`%ProgramData%\MindWorkAI\AI-Studio\`

#### Linux policy directories

AI Studio checks each directory listed in `$XDG_CONFIG_DIRS` and looks for a `mindwork-ai-studio` subdirectory in each one. If `$XDG_CONFIG_DIRS` is empty or not set, AI Studio falls back to:

`/etc/xdg/mindwork-ai-studio/`

The directories from `$XDG_CONFIG_DIRS` are processed in order.

#### macOS policy directory

`/Library/Application Support/MindWork/AI Studio/`

#### Policy file names and content

Configuration files:

- `config0.yaml`
- `config1.yaml`
- ...
- `config9.yaml`

Each configuration file contains one configuration ID and one server URL:

```yaml
id: "9072b77d-ca81-40da-be6a-861da525ef7b"
server_url: "https://intranet.example.org/ai-studio/configuration"
```

Optional encryption secret file:

- `config_encryption_secret.yaml`

```yaml
config_encryption_secret: "BASE64..."
```

### Environment variable example

If you need the fallback environment-variable format, configure the values like this:

```bash
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID0=9072b77d-ca81-40da-be6a-861da525ef7b
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL0=https://intranet.example.org/ai-studio/configuration
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID1=a1b2c3d4-e5f6-7890-abcd-ef1234567890
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL1=https://intranet.example.org/ai-studio/department-config
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET=BASE64...
```

### Legacy formats (still supported)

The following older formats are still supported for backwards compatibility:

- Registry value `configs` or environment variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS`: Combined format `id1@url1;id2@url2;...`
- Registry value `config_id` or environment variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID`
- Registry value `config_server_url` or environment variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL`
- Registry value `config_encryption_secret` or environment variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET`

Within a single source, AI Studio reads the new indexed pairs first, then the combined legacy format, and finally the legacy single-configuration format. This makes it possible to migrate gradually without breaking older setups.

### How configurations are downloaded

Let's assume as example that `https://intranet.my-company.com:30100/ai-studio/configuration` is the server address and `9072b77d-ca81-40da-be6a-861da525ef7b` is the configuration ID. AI Studio will derive the following address from this information: `https://intranet.my-company.com:30100/ai-studio/configuration/9072b77d-ca81-40da-be6a-861da525ef7b.zip`. Important: The configuration ID will always be written in lowercase, even if it is configured in uppercase. If `9072B77D-CA81-40DA-BE6A-861DA525EF7B` is configured, the same address will be derived. Your web server must be configured accordingly.

Finally, AI Studio will send a GET request and download the ZIP file. The ZIP file only contains the files necessary for the configuration. It's normal to include a file for an icon along with the actual configuration plugin.

Approximately every 16 minutes, AI Studio checks the metadata of the ZIP file by reading the [ETag](https://en.wikipedia.org/wiki/HTTP_ETag). When the ETag was not changed, no download will be performed. Make sure that your web server supports this. When using multiple configurations, each configuration is checked independently.

## Configure the configuration web server

In principle, you can use any web server that can serve ZIP files from a folder. However, keep in mind that AI Studio queries the file's metadata using [ETag](https://en.wikipedia.org/wiki/HTTP_ETag). Your web server must support this feature. For security reasons, you should also make sure that users cannot list the contents of the directory. This is important because the different configurations may contain confidential information such as API keys. Each user should only know their own configuration ID. Otherwise, a user might try to use someone else’s ID to gain access to exclusive resources.

The ZIP file names for the configurations must be in lowercase on the server, or your web server needs to ignore the spelling in requests. Also, make sure the web server is only accessible within your organization’s intranet. You don’t want the server open to everyone worldwide.

You can use the open source web server [Caddy](https://caddyserver.com/). The project is openly developed on [GitHub](https://github.com/caddyserver/caddy). Below you’ll find an example configuration, a so-called `Caddyfile`, for serving configurations from the folder `/localdata1/ai-studio/config` to AI Studio. The TLS certificates are loaded from the folder `/localdata1/tls-certificate`.

```
{
    # Disable logging:
    log {
        output discard
    }

    # Disable automatic HTTPS redirection:
    auto_https off
}

intranet.my-company.com:30100 {
    # Load TLS certificates:
    tls /localdata1/tls-certificate/cert_webserver.pem /localdata1/tls-certificate/key_webserver.pem
    
    # Serve the configuration files:
    handle_path /ai-studio/configuration/* {
        file_server {
            root /localdata1/ai-studio/config
            
            # Disable directory browsing:
            browse false
        }
    }
    
    # All other requests will receive a 404 Not Found response:
    handle {
        respond "Not Found" 404
    }
}
```

## Important: Plugin ID must match the enterprise configuration ID

The `ID` field inside your configuration plugin (the Lua file) **must** be identical to the enterprise configuration ID configured on the client device, whether it comes from the registry, a policy file, or an environment variable. AI Studio uses this ID to match downloaded configurations to their plugins. If the IDs do not match, AI Studio will log a warning and the configuration may not be displayed correctly on the Information page.

For example, if your enterprise configuration ID is `9072b77d-ca81-40da-be6a-861da525ef7b`, then your plugin must declare:

```lua
ID = "9072b77d-ca81-40da-be6a-861da525ef7b"
```

## Important: Mark enterprise-managed plugins explicitly

Configuration plugins deployed by your configuration server should define:

```lua
DEPLOYED_USING_CONFIG_SERVER = true
```

Local, manually managed configuration plugins should set this to `false`. If the field is missing, AI Studio falls back to the plugin path (`.config`) to determine whether the plugin is managed and logs a warning.

## Example AI Studio configuration
The latest example of an AI Studio configuration via configuration plugin can always be found in the repository in the `app/MindWork AI Studio/Plugins/configuration` folder. Here are the links to the files:

- [The icon](../app/MindWork%20AI%20Studio/Plugins/configuration/icon.lua)
- [The configuration with explanations](../app/MindWork%20AI%20Studio/Plugins/configuration/plugin.lua)

Please note that the icon must be an SVG vector graphic. Raster graphics like PNGs, GIFs, and others aren’t supported. You can use the sample icon, which looks like a gear.

Currently, you can configure the following things:
- Any number of LLM providers (self-hosted or cloud providers with encrypted API keys)
- Any number of transcription providers for voice-to-text functionality
- Any number of embedding providers for RAG
- The update behavior of AI Studio
- Various UI and feature settings (see the example configuration for details)

All other settings can be made by the user themselves. If you need additional settings, feel free to create an issue in our planning repository: https://github.com/MindWorkAI/Planning/issues

## Encrypted API Keys

You can include encrypted API keys in your configuration plugins for cloud providers (like OpenAI, Anthropic) or secured on-premise models. This feature provides obfuscation to prevent casual exposure of API keys in configuration files.

**Important Security Note:** This is obfuscation, not absolute security. Users with administrative access to their machines can potentially extract the decrypted API keys with sufficient effort. This feature is designed to:
- Prevent API keys from being visible in plaintext in configuration files
- Protect against accidental exposure when sharing or reviewing configurations
- Add a barrier against casual snooping

### Setting Up Encrypted API Keys

1. **Generate an encryption secret:**
   In AI Studio, enable the "Show administration settings" toggle in the app settings. Then click the "Generate encryption secret and copy to clipboard" button in the "Enterprise Administration" section. This generates a cryptographically secure 256-bit key and copies it to your clipboard as a base64 string.

2. **Deploy the encryption secret:**
   Distribute the secret to all client machines using any supported enterprise source. The secret can be deployed on its own, even when no enterprise configuration IDs or server URLs are defined on that machine:
   - Windows Registry / GPO: `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT\config_encryption_secret`
   - Policy file: `config_encryption_secret.yaml`
   - Environment fallback: `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET`

   You must also deploy the same secret on the machine where you will export the encrypted API keys (step 3).

3. **Export encrypted API keys from AI Studio:**
   Once the encryption secret is deployed on your machine:
   - Configure a provider with an API key in AI Studio's settings
   - Click the export button for that provider
   - If an API key is configured, you will be asked if you want to include the encrypted API key in the export
   - The exported Lua code will contain the encrypted API key in the format `ENC:v1:<base64-encoded data>`

4. **Add encrypted keys to your configuration:**
   Copy the exported configuration (including the encrypted API key) into your configuration plugin.

### Example Configuration with Encrypted API Key

```lua
CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
    ["Id"] = "9072b77d-ca81-40da-be6a-861da525ef7b",
    ["InstanceName"] = "Corporate OpenAI GPT-4",
    ["UsedLLMProvider"] = "OPEN_AI",
    ["Host"] = "NONE",
    ["Hostname"] = "",
    ["APIKey"] = "ENC:v1:MTIzNDU2Nzg5MDEyMzQ1NkFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFla...",
    ["AdditionalJsonApiParameters"] = "",
    ["Model"] = {
        ["Id"] = "gpt-4",
        ["DisplayName"] = "GPT-4",
    }
}
```

The API key will be automatically decrypted when the configuration is loaded and stored securely in the operating system's credential store (Windows Credential Manager / macOS Keychain).
