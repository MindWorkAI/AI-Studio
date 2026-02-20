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

- **Registry** (only available for Microsoft Windows): On Windows devices, AI Studio first tries to read the information from the registry. The registry information can be managed and distributed centrally as a so-called Group Policy Object (GPO).

- **Environment variables**: On all operating systems (on Windows as a fallback after the registry), AI Studio tries to read the configuration metadata from environment variables.

### Multiple configurations (recommended)

AI Studio supports loading multiple enterprise configurations simultaneously. This enables hierarchical configuration schemes, e.g., organization-wide settings combined with department-specific settings. The following keys and variables are used:

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `configs` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS`: A combined format containing one or more configuration entries. Each entry consists of a configuration ID and a server URL separated by `@`. Multiple entries are separated by `;`. The format is: `id1@url1;id2@url2;id3@url3`. The configuration ID must be a valid [GUID](https://en.wikipedia.org/wiki/Universally_unique_identifier#Globally_unique_identifier).

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `config_encryption_secret` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET`: A base64-encoded 32-byte encryption key for decrypting API keys in configuration plugins. This is optional and only needed if you want to include encrypted API keys in your configuration. All configurations share the same encryption secret.

**Example:** To configure two enterprise configurations (one for the organization and one for a department):

```
MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS=9072b77d-ca81-40da-be6a-861da525ef7b@https://intranet.my-company.com:30100/ai-studio/configuration;a1b2c3d4-e5f6-7890-abcd-ef1234567890@https://intranet.my-company.com:30100/ai-studio/department-config
```

**Priority:** When multiple configurations define the same setting (e.g., a provider with the same ID), the first definition wins. The order of entries in the variable determines priority. Place the organization-wide configuration first, followed by department-specific configurations if the organization should have higher priority.

### Windows GPO / PowerShell example for `configs`

If you distribute multiple GPOs, each GPO should read and write the same registry value (`configs`) and only update its own `id@url` entry. Other entries must stay untouched.

The following PowerShell example provides helper functions for appending and removing entries safely:

```powershell
$RegistryPath = "HKCU:\Software\github\MindWork AI Studio\Enterprise IT"
$ConfigsValueName = "configs"

function Get-ConfigEntries {
    param([string]$RawValue)

    if ([string]::IsNullOrWhiteSpace($RawValue)) { return @() }

    $entries = @()
    foreach ($part in $RawValue.Split(';')) {
        $trimmed = $part.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }

        $pair = $trimmed.Split('@', 2)
        if ($pair.Count -ne 2) { continue }

        $id = $pair[0].Trim().ToLowerInvariant()
        $url = $pair[1].Trim()
        if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($url)) { continue }

        $entries += [PSCustomObject]@{
            Id  = $id
            Url = $url
        }
    }

    return $entries
}

function ConvertTo-ConfigValue {
    param([array]$Entries)

    return ($Entries | ForEach-Object { "$($_.Id)@$($_.Url)" }) -join ';'
}

function Add-EnterpriseConfigEntry {
    param(
        [Parameter(Mandatory=$true)][Guid]$ConfigId,
        [Parameter(Mandatory=$true)][string]$ServerUrl
    )

    if (-not (Test-Path $RegistryPath)) {
        New-Item -Path $RegistryPath -Force | Out-Null
    }

    $raw = (Get-ItemProperty -Path $RegistryPath -Name $ConfigsValueName -ErrorAction SilentlyContinue).$ConfigsValueName
    $entries = Get-ConfigEntries -RawValue $raw
    $normalizedId = $ConfigId.ToString().ToLowerInvariant()
    $normalizedUrl = $ServerUrl.Trim()

    # Replace only this one ID, keep all other entries unchanged.
    $entries = @($entries | Where-Object { $_.Id -ne $normalizedId })
    $entries += [PSCustomObject]@{
        Id  = $normalizedId
        Url = $normalizedUrl
    }

    Set-ItemProperty -Path $RegistryPath -Name $ConfigsValueName -Type String -Value (ConvertTo-ConfigValue -Entries $entries)
}

function Remove-EnterpriseConfigEntry {
    param(
        [Parameter(Mandatory=$true)][Guid]$ConfigId
    )

    if (-not (Test-Path $RegistryPath)) { return }

    $raw = (Get-ItemProperty -Path $RegistryPath -Name $ConfigsValueName -ErrorAction SilentlyContinue).$ConfigsValueName
    $entries = Get-ConfigEntries -RawValue $raw
    $normalizedId = $ConfigId.ToString().ToLowerInvariant()

    # Remove only this one ID, keep all other entries unchanged.
    $updated = @($entries | Where-Object { $_.Id -ne $normalizedId })
    Set-ItemProperty -Path $RegistryPath -Name $ConfigsValueName -Type String -Value (ConvertTo-ConfigValue -Entries $updated)
}

# Example usage:
# Add-EnterpriseConfigEntry -ConfigId "9072b77d-ca81-40da-be6a-861da525ef7b" -ServerUrl "https://intranet.example.org:30100/ai-studio/configuration"
# Remove-EnterpriseConfigEntry -ConfigId "9072b77d-ca81-40da-be6a-861da525ef7b"
```

### Single configuration (legacy)

The following single-configuration keys and variables are still supported for backwards compatibility. AI Studio always reads both the multi-config and legacy variables and merges all found configurations into one list. If a configuration ID appears in both, the entry from the multi-config format takes priority (first occurrence wins). This means you can migrate to the new format incrementally without losing existing configurations:

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `config_id` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID`: This must be a valid [GUID](https://en.wikipedia.org/wiki/Universally_unique_identifier#Globally_unique_identifier). It uniquely identifies the configuration. You can use an ID per department, institute, or even per person.

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `config_server_url` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL`: An HTTP or HTTPS address using an IP address or DNS name. This is the web server from which AI Studio attempts to load the specified configuration as a ZIP file.

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `config_encryption_secret` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET`: A base64-encoded 32-byte encryption key for decrypting API keys in configuration plugins. This is optional and only needed if you want to include encrypted API keys in your configuration.

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

The `ID` field inside your configuration plugin (the Lua file) **must** be identical to the enterprise configuration ID used in the registry or environment variable. AI Studio uses this ID to match downloaded configurations to their plugins. If the IDs do not match, AI Studio will log a warning and the configuration may not be displayed correctly on the Information page.

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
   Distribute the secret to all client machines via Group Policy (Windows Registry) or environment variables:
   - Registry: `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT\config_encryption_secret`
   - Environment: `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET`

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
