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

- Registry values `config_id_00000` to `config_id_99999` together with `config_server_url_00000` to `config_server_url_99999`
- Environment variables `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_00000` to `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_99999` together with `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL_00000` to `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL_99999`
- Policy files `config_00000.yaml` to `config_99999.yaml`

Each configuration ID must be a valid [GUID](https://en.wikipedia.org/wiki/Universally_unique_identifier#Globally_unique_identifier). Up to 100,000 indexed configuration slots are supported per device.

If multiple configurations define the same setting, the first definition wins. For indexed pairs and policy files, the order is slot `00000`, then `00001`, and so on up to `99999`.

For backwards compatibility, the older slot names `0` to `9` without an underscore are still supported. AI Studio also accepts other numeric slot suffixes with up to five digits. Slot suffixes are matched exactly, so `config_id_1`, `config_id_01`, and `config_id_00001` are treated as separate slots. Use the five-digit format with an underscore for new deployments.

### Windows registry example

The Windows registry path is:

`HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`

Example values:

- `config_id_00000` = `9072b77d-ca81-40da-be6a-861da525ef7b`
- `config_server_url_00000` = `https://intranet.example.org/ai-studio/configuration`
- `config_id_10503` = `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
- `config_server_url_10503` = `https://intranet.example.org/ai-studio/department-config`
- `config_encryption_secret` = `BASE64...`

This approach works well with GPOs because each slot can be managed independently without rewriting a shared combined string.

### Policy files

#### Windows policy directory

`%ProgramData%\MindWorkAI\AI-Studio\`

#### Linux policy directories

AI Studio checks each directory listed in `$XDG_CONFIG_DIRS` and looks for a `mindwork-ai-studio` subdirectory in each one. If `$XDG_CONFIG_DIRS` is empty or not set, AI Studio falls back to:

`/etc/xdg/mindwork-ai-studio/`

The directories from `$XDG_CONFIG_DIRS` are processed in order.

#### Flatpak policy directory

When AI Studio runs as a Flatpak, it first checks this sandbox path before the regular Linux policy directories:

`/app/etc/MindWorkAI/`

This path is intended for a Flatpak provisioning extension like:

```yaml
add-extensions:
  org.MindWorkAI.AIStudio.provisioning:
    directory: etc/MindWorkAI
    no-autodownload: true
```

Policy files can then be provided on the host through the extension directories. For example:

- System-wide, read-only: `/var/lib/flatpak/extension/org.MindWorkAI.AIStudio.provisioning/x86_64/stable/`
- User-specific: `$XDG_DATA_HOME/flatpak/extension/org.MindWorkAI.AIStudio.provisioning/x86_64/stable/`

Files placed there are mounted into the sandbox at `/app/etc/MindWorkAI/`. Use the same policy file names and YAML format described below.

#### macOS policy directory

`/Library/Application Support/MindWork/AI Studio/`

#### Policy file names and content

Configuration files:

- `config_00000.yaml`
- `config_00001.yaml`
- ...
- `config_99999.yaml`

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

Optional custom root certificate policy file:

- `external_http_custom_root_certificates.yaml`

```yaml
enabled: true
bundle_path: "/app/etc/MindWorkAI/company-root-cas.pem"
allowed_hosts: "*.intra.example.org;eri.example.org"
```

When this file exists and contains a valid `enabled` value, it takes precedence over the custom root certificate environment variables described below. This is useful for Flatpak deployments because a Flatpak provisioning extension can provide the policy file and the PEM bundle together. Set `enabled: false` to explicitly disable additional root certificates and ignore lower-priority environment variables.

### Environment variable example

If you need the fallback environment-variable format, configure the values like this:

```bash
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_00000=9072b77d-ca81-40da-be6a-861da525ef7b
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL_00000=https://intranet.example.org/ai-studio/configuration
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_10503=a1b2c3d4-e5f6-7890-abcd-ef1234567890
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL_10503=https://intranet.example.org/ai-studio/department-config
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

### Custom root certificates for Flatpak deployments

On Linux, AI Studio normally relies on the operating system's trusted root certificates for external HTTPS requests. In a Flatpak package, however, the application may not be able to read organization-specific root certificates from the host system. This can affect connections to self-hosted AI providers, embedding providers, transcription providers, ERI servers, and enterprise configuration servers.

If your organization uses private root CAs, place a PEM bundle with the required root CA certificates in a location that is readable inside the Flatpak sandbox. The bundle should contain one or more certificates using the regular PEM marker:

```text
-----BEGIN CERTIFICATE-----
...
-----END CERTIFICATE-----
```

For Flatpak deployments, the recommended approach is to provide an enterprise policy file through the Flatpak provisioning extension:

```yaml
# /app/etc/MindWorkAI/external_http_custom_root_certificates.yaml
enabled: true
bundle_path: "/app/etc/MindWorkAI/company-root-cas.pem"
allowed_hosts: "*.intra.example.org;eri.example.org"
```

Place the PEM bundle at the configured path inside the sandbox, for example, through the same provisioning extension. This allows AI Studio to use the additional root certificates during the first enterprise configuration download.

As a fallback, you can configure these environment variables before AI Studio starts:

```bash
MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATES_ENABLED=true
MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH=/path/in/sandbox/company-root-cas.pem
MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS=*.intra.example.org;eri.example.org
```

You can also manage the same behavior from a configuration plugin after the plugin has been downloaded:

```lua
CONFIG["SETTINGS"]["DataApp.ExternalHttpCustomRootCertificatesEnabled"] = true
CONFIG["SETTINGS"]["DataApp.ExternalHttpCustomRootCertificateBundlePath"] = "/path/in/sandbox/company-root-cas.pem"
CONFIG["SETTINGS"]["DataApp.ExternalHttpCustomRootCertificateAllowedHosts"] = { "*.intra.example.org", "eri.example.org" }
```

This feature does not disable TLS verification. AI Studio first uses the system certificate validation. If that fails only because the certificate chain is not trusted, AI Studio tries again with the configured root CA bundle, but only for configured host patterns. Exact hosts such as `eri.intra.example.org` and one-label wildcards such as `*.intra.example.org` are supported. Hostname mismatches, missing certificates, expired certificates, and otherwise invalid chains are still rejected. Built-in cloud provider endpoints, such as OpenAI, Google, etc., never use configured custom root certificates.

As an alternative, your Flatpak launch environment can set `SSL_CERT_FILE` or `SSL_CERT_DIR` to a certificate bundle or directory that .NET/OpenSSL can read. This is useful when your deployment already manages a consistent PEM bundle for the sandbox.

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
- Enterprise hash approvals for assistant plugins
- The update behavior of AI Studio
- Various UI and feature settings (see the example configuration for details)

All other settings can be made by the user themselves. If you need additional settings, feel free to create an issue in our planning repository: https://github.com/MindWorkAI/Planning/issues

## Enterprise approval for assistant plugins

Enterprise configurations can approve assistant plugins by hash so that users do not need to run a local assistant audit before activation. The approval is based only on the current plugin content, not on the plugin GUID.

AI Studio computes the approval hash as a SHA-256 digest over all `.lua` files in the assistant plugin directory:

- recursively
- sorted by relative path in ordinal order
- using canonical `/` path separators
- hashing relative-path length, relative path, content length, and file content for each Lua file

If any Lua file changes, the hash changes automatically and the enterprise approval no longer applies.

### Configuration example

Add the approval list to `CONFIG["SETTINGS"]` in your configuration plugin:

```lua
CONFIG["SETTINGS"]["DataAssistantPluginAudit.EnterpriseApprovedPlugins"] = {
    {
        ["PluginHash"] = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
        ["DisplayName"] = "Corporate Translation Assistant",
        ["Comment"] = "Approved for internal rollout",
        ["ApprovedBy"] = "AI Governance Board",
        ["ApprovedAtUtc"] = "2026-07-02T09:30:00Z",
    }
}
```

`PluginHash` is required. All other fields are optional and are shown in the UI as approval metadata.

### Generating the hash

Use the build-script command from the repository root:

```bash
dotnet run --project app/Build -- assistant-plugin-hash "<plugin-dir>" --lua-snippet
```

This prints the canonical hash and, with `--lua-snippet`, also prints a ready-to-paste Lua snippet for `CONFIG["SETTINGS"]`.

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
