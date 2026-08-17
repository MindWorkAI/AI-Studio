# Enterprise IT

## Overview
Do you want to manage MindWork AI Studio in a corporate environment or within an organization? This documentation explains what you need to do and how it works. First, here's an overview of the entire process:

- You can distribute MindWork AI Studio to employees' devices using tools like Microsoft System Center Configuration Manager (SCCM).
- Employees can get updates through the built-in update feature. Enterprise configuration can disable automatic checks or the entire built-in update feature so that the IT department controls which version gets distributed. Installations you rolled out yourself never update themselves anyway, so the two kinds can coexist on one device.
- AI Studio checks about every 16 minutes to see where and which configuration it should load. This information is loaded from the local system. On Windows, you might use the registry, for example.
- If it finds the necessary metadata, AI Studio downloads the configuration as a ZIP file from the specified server.
- The configuration is an AI Studio plugin written in Lua.
- Any changes to the configuration apply live while the software is running, so employees don’t need to restart it.

AI Studio checks about every 16 minutes to see if the configuration ID, the server for the configuration, or the configuration itself has changed. If it finds any changes, it loads the updated configuration from the server and applies it right away.

### Manage app updates

Set `CONFIG["SETTINGS"]["DataApp.UpdateInterval"]` in the configuration plugin to control update checks:

- `NO_CHECK` disables automatic update checks. Users can still check for and install updates manually.
- `DISABLE_UPDATES` disables automatic and manual update checks and installations. AI Studio tells users that updates are managed by their organization and directs questions to their IT department. This policy takes effect immediately when the enterprise configuration changes.

Use `DISABLE_UPDATES` when your organization distributes approved versions through its own software-management process.

### Installations that never update themselves

AI Studio recognizes installations its updater cannot replace and never updates those, no matter what `DataApp.UpdateInterval` and `DataApp.UpdateInstallation` say. You can therefore leave automatic updates enabled for your whole organization: the installations you rolled out ignore them and receive their versions from you, while installations your colleagues fetched from GitHub keep updating themselves.

This matters most on Windows. The installer we publish installs per user below `%LOCALAPPDATA%`, and the updater runs exactly that installer. Updating an installation that sits anywhere else therefore does not replace it: a second installation appears below `%LOCALAPPDATA%` while yours stays untouched, and from then on it is a matter of chance which one a colleague starts. Loosening the permissions of your deployment does not change this — the updater never writes into the current location to begin with.

AI Studio recognizes these cases:

| Case | How AI Studio recognizes it | What users are told |
|---|---|---|
| Marker file | A file named `managed-installation` next to the program file | Updates come from their IT department |
| Machine-wide program directory | The program file sits below `%ProgramFiles%`, `%ProgramFiles(x86)%`, or `%ProgramW6432%` | Updates come from their IT department |
| Location the user cannot write to | The directory that would have to be replaced is not writable for the current user, e.g. `/Applications` on a device managed through MDM, or `/opt` on Linux | Updates come from their IT department |
| Self-chosen directory (Windows only) | Everything else outside `%LOCALAPPDATA%`, e.g. `D:\Tools\MindWork AI Studio` | They have to install a new version themselves, with a link to the latest release |
| Flatpak | Running inside a Flatpak sandbox | Updates come from their Flatpak distribution |

The information page reports which case applies, so a support request can start from that instead of guesswork.

#### The marker file

Place an empty file named `managed-installation` next to the program file, in the same directory as `MindWork AI Studio.exe` on Windows or as the executable on Linux. Its content is ignored; only its existence matters. The marker applies to that one installation, so a colleague who installed AI Studio from GitHub on the same device is not affected by it.

Use the marker when the other cases do not cover your deployment, for example, when you roll out our regular per-user installer through Intune, or when you install into a directory of your own such as `D:\Program Files\MindWork AI Studio`.

On macOS there is no marker file: any additional file inside the app bundle would break its code signature. A bundle in a location your users cannot write to is recognized anyway. If you want AI Studio to name your organization explicitly on macOS, set `DataApp.UpdateInterval` to `DISABLE_UPDATES`, which takes precedence over all of this.

#### Existing double installations

This recognition prevents new double installations; it does not clean up ones that already exist. On affected devices, remove the second installation below `%LOCALAPPDATA%\MindWork AI Studio\` together with its uninstall entry under `HKEY_CURRENT_USER`, and make sure that shortcuts point at your deployment again.

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

The slot order determines which configurations are downloaded, not which one wins a conflict. When two of your configuration plugins define the same setting or the same object, the declared priority decides. See [Priority of configuration plugins](#priority-of-configuration-plugins).

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
  org.mindworkai.AIStudio.provisioning:
    directory: etc/MindWorkAI
    no-autodownload: true
```

Policy files can then be provided on the host through the extension directories. For example:

- System-wide, read-only: `/var/lib/flatpak/extension/org.mindworkai.AIStudio.provisioning/x86_64/stable/`
- User-specific: `$XDG_DATA_HOME/flatpak/extension/org.mindworkai.AIStudio.provisioning/x86_64/stable/`

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

The field describes a plugin, it does not grant it anything. Which configurations belong to your organization is always decided by the plugin path: which approvals for assistant plugins are honored, which configuration wins a conflict, and which configuration AI Studio withdraws once you stop referencing it. A configuration stored under `.config` is therefore removed when your organization no longer references its ID, whatever this field says.

## Priority of configuration plugins

When you deploy more than one configuration, two of your configuration plugins may manage the same setting or define the same object, e.g. the same LLM provider. The optional `PRIORITY` field decides which one wins:

```lua
PRIORITY = 100
```

A configuration plugin with a higher priority is applied later and therefore wins. The field is optional and defaults to `0`.

A typical layered setup:

| Configuration | `PRIORITY` | Role |
|---|---|---|
| Organization-wide base | `0` | Providers, update behavior, and security settings for everybody |
| Department | `100` | Refines the base, e.g. a different default model |
| Project or lab | `200` | Refines the department configuration |

A configuration only overrides what it actually defines. Everything it does not mention keeps the value of the configuration below it. The same applies when you remove a configuration later: its settings fall back to the configuration below, not to the AI Studio defaults. Once no configuration manages a setting anymore, see [Withdrawing a configuration](#withdrawing-a-configuration).

Give two configurations that must override each other different priorities. With an equal priority, the order is stable across restarts but arbitrary, so the outcome is not the one you designed.

Two guarantees are independent of the priority:

- A local configuration plugin never wins against one your IT department deployed, whatever priority it declares. Local plugins are always applied afterwards, and they may not take over a setting or an object that belongs to one of your configurations.
- Two plugins must not share the same plugin ID. If that happens, AI Studio keeps the one your IT department deployed and logs a warning for the other.

The single exception is a configuration you stage for a test under `.config-tests`. It is applied after your deployed configurations and wins a shared plugin ID, so that you can try out the next version of a configuration under its final ID. See [Local staging and testing](#local-staging-and-testing).

### Settings that hold a list or a table

For a setting that holds a list or a table, the winning configuration replaces the whole collection. It does not merge the entries. A department configuration that lists a single entry drops every entry the base configuration had set for that setting.

This is intentional: replacing is the only way a department can take something back. A department that wants an assistant to be visible again can only achieve that by not listing it.

Plan for it in these settings:

| Setting | What a partial list costs you |
|---|---|
| `DataApp.HiddenAssistants` | Assistants hidden by the base configuration become **visible** again |
| `DataSourceSecuritySettings.TrustedProviderIds` | Providers trusted by the base configuration lose that status |
| `DataApp.ExternalHttpCustomRootCertificateAllowedHosts` | Hosts of the base configuration stop trusting your root certificates |
| `DataConfidence.CustomConfidenceScheme` | Providers left out fall back to the AI Studio default confidence |
| `DataChat.PreselectedDataSourceIds` | Data sources preselected by the base configuration are no longer preselected |

The rule of thumb: whenever a configuration with a higher priority touches one of these settings, it has to repeat every entry it wants to keep. Watch `DataApp.HiddenAssistants` in particular, because it is the only one in this list that opens something up instead of restricting it.

Two settings are the exception and add up instead of replacing:

- `DataApp.EnabledPreviewFeatures` — enable one preview feature for the whole organization and another one for a single department, and users of that department get both.
- `DataAssistantPluginAudit.EnterpriseApprovedPlugins` — a department configuration can approve additional assistant plugins without repeating the approvals of the base configuration. Approving is a pure allowlist over hashes, so there is nothing a replacing list could express that adding does not.

In both cases each configuration keeps its own contribution, so removing one of them only withdraws what this configuration had granted. While a configuration plugin is deployed but cannot be loaded, its approvals are kept: AI Studio does not withdraw approvals it cannot currently read.

One clarification for `DataChat.PreselectedDataSourceIds`: the IDs are not limited to the data sources of the same configuration. They are resolved against every known data source, including those of your other configurations and the ones a user configured. IDs that resolve to nothing are ignored.

## Withdrawing a configuration

A configuration does not have to stay forever: you stop deploying it, a user deletes a configuration they installed themselves, or a test configuration ends with the next restart. AI Studio then removes what that configuration brought along, such as its providers, data sources, profiles, chat templates, and its approvals for assistant plugins.

Settings go one step further. AI Studio remembers the value each setting had before a configuration took it over and hands it back once no configuration manages that setting anymore. Somebody who had chosen a start page before your configuration set one therefore gets their own start page back, not the AI Studio default.

Two cases differ:

- **There is nothing to hand back.** When a setting still had its AI Studio default at the moment your configuration took it over, that default returns. The same applies to settings which a configuration already managed before AI Studio v26.8.1, because nothing was remembered back then.
- **Somebody used `AllowUserOverride`.** A setting you offered as an organization default, and which the user changed afterwards, keeps the user's value. Their decision outlives your configuration.

A configuration that is deployed but cannot be loaded, e.g. because of an error in its Lua code, is not withdrawn. It still manages the device, so everything it brought along stays untouched until you actually stop deploying it.

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

### Only your configurations may approve

Approvals are honored only in configuration plugins that speak for your organization: plugins a configuration server deployed, meaning plugins stored under the `.config` directory, and plugins you staged for a test under `.config-tests`. AI Studio ignores the approvals of any other locally placed configuration plugin and writes a warning to the log.

The reason is what an approval does: it marks an assistant plugin as safe without any security audit, and AI Studio then tells the user that their organization approved it. Anyone who can drop a file into the plugin directory could otherwise disable the security audit for an assistant plugin of their choosing while the app vouches for it in your name.

This is decided by where the plugin is stored, not by its `DEPLOYED_USING_CONFIG_SERVER` field. That field is part of the plugin itself, so any plugin could claim it.

If you want to test approvals before rolling a configuration out, see [Local staging and testing](#local-staging-and-testing).

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

## Local staging and testing

Before you roll a configuration out through a configuration web server, you can stage it on a device and test it end to end, including the enterprise approvals for assistant plugins described above. This needs no configuration web server, no registry, policy, or environment entry, and no encryption secret.

AI Studio has a dedicated directory for this: `.config-tests`. A configuration stored there speaks for your organization exactly like a deployed one. In exchange, AI Studio empties the directory on every start, so a test configuration is valid for one session.

Do not use the `.config` directory for this. It belongs to your configuration web server, and AI Studio removes everything there that your organization does not reference anymore.

### The data directory

Plugins live in the data directory of AI Studio:

| Platform | Data directory |
| --- | --- |
| Windows | `%LOCALAPPDATA%\com.github.mindwork-ai.ai-studio\data` |
| macOS | `~/Library/Application Support/com.github.mindwork-ai.ai-studio/data` |
| Linux | `$XDG_DATA_HOME/com.github.mindwork-ai.ai-studio/data`, usually `~/.local/share/com.github.mindwork-ai.ai-studio/data` |
| Linux (Flatpak) | `~/.var/app/org.mindworkai.AIStudio/data/com.github.mindwork-ai.ai-studio/data` |

### Staging a configuration

Place the files **while AI Studio is running**: the test directory is emptied whenever the app starts.

1. Start AI Studio. It creates `<data directory>/plugins/.config-tests/` if it does not exist yet.
2. Create a directory below it and place your `plugin.lua` there, e.g. `.config-tests/my-department-draft/`. The directory name is up to you here: a test configuration is identified by the `ID` field inside the plugin, not by the directory it lives in.
3. Place the assistant plugin you want to test in `<data directory>/plugins/assistants/<any name>/`.
4. AI Studio watches the plugin directory and picks both up without a restart. The security card of the assistant then states that your organization approved it, exactly as it will after the rollout.

While a test configuration is loaded, the Information page reports it, including the directory it was staged in. After a restart, that same page tells you that a test configuration was removed, so nobody has to wonder where the directory went.

What behaves like the later rollout:

- The approvals for assistant plugins are honored.
- Settings and configuration objects the test configuration manages are protected against local configuration plugins.
- When the test configuration declares the same plugin `ID` as one your organization deployed, the test configuration wins. This is how you try out the next version of an existing configuration under its final ID.

What deliberately does not:

- A test configuration has no protection against the user. You can remove it on the plugin page and replace it by importing a new version.
- It does not survive a restart.

### Testing with a small group

To let colleagues take part in the test, place the same two directories on each of their devices while AI Studio runs, for example through a script, your MDM solution, or a login script. A configuration web server is not involved, and nothing has to be enabled inside AI Studio. Ordinary user accounts can take part: the data directory belongs to the user, so no administrator rights are needed to place the files.

Keep in mind that everybody in the group loses the test configuration the next time they start AI Studio. Either repeat the step, or let your script place the files at every login.

### Cleaning up

Restart AI Studio: the test directory is emptied, the approvals are gone, and the assistant requires a security audit again. Every setting your test configuration had taken over returns to the value it had before the test, as described in [Withdrawing a configuration](#withdrawing-a-configuration). To end a test without restarting, delete the configuration on the plugin page.

### Security note

A test configuration carries the rights of an organization configuration without anybody having deployed it. Two properties keep that in check, and you should not work around either of them:

- The directory is emptied on every start, so nothing staged for a test can settle in unnoticed.
- No feature inside AI Studio writes into that directory. Importing, sharing, and deleting plugins never touch it, so a user cannot be talked into staging a configuration by opening a file.

The data directory belongs to the user account, so whoever can write there can approve assistant plugins in the name of your organization until the next restart. Treat write access to the data directory as equivalent to deploying a configuration, and protect it accordingly on managed devices.

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

## Letting users provide their own API key

Sometimes you want to hand out a preconfigured provider -- a fixed host, model, and instance name
-- without embedding a shared API key for it. Each user then brings their own key, for example
their personal OpenAI or Anthropic account, while everything else about the provider stays exactly
as your organization configured it.

Set `AllowUserProvidedAPIKey` on the provider:

```lua
CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
    ["Id"] = "9072b77d-ca81-40da-be6a-861da525ef7b",
    ["InstanceName"] = "Corporate OpenAI GPT-4",
    ["UsedLLMProvider"] = "OPEN_AI",
    ["Host"] = "NONE",
    ["Hostname"] = "",
    ["AllowUserProvidedAPIKey"] = true,
    ["AdditionalJsonApiParameters"] = "",
    ["Model"] = {
        ["Id"] = "gpt-4",
        ["DisplayName"] = "GPT-4",
    }
}
```

With `AllowUserProvidedAPIKey` set, the provider still shows up as managed by your organization,
and users still cannot change the host, model, instance name, or any other field. The settings
page shows a key icon instead of the usual lock icon for this provider; opening it only offers the
API key field, with everything else disabled.

The flag works the same way for embedding and transcription providers:

```lua
CONFIG["EMBEDDING_PROVIDERS"][#CONFIG["EMBEDDING_PROVIDERS"]+1] = {
    ["Id"] = "3f0a4e8c-1d6b-4a91-8f2e-7c5d9b0a4e13",
    ["Name"] = "Corporate Embeddings",
    ["UsedLLMProvider"] = "OPEN_AI",
    ["Host"] = "NONE",
    ["Hostname"] = "",
    ["AllowUserProvidedAPIKey"] = true,
    ["Model"] = {
        ["Id"] = "text-embedding-3-large",
        ["DisplayName"] = "Text Embedding 3 Large",
    }
}

CONFIG["TRANSCRIPTION_PROVIDERS"][#CONFIG["TRANSCRIPTION_PROVIDERS"]+1] = {
    ["Id"] = "b1c7d24f-5e83-4a06-9d1b-2f8e6a3c7d50",
    ["Name"] = "Corporate Transcription",
    ["UsedLLMProvider"] = "OPEN_AI",
    ["Host"] = "NONE",
    ["Hostname"] = "",
    ["AllowUserProvidedAPIKey"] = true,
    ["Model"] = {
        ["Id"] = "whisper-1",
        ["DisplayName"] = "Whisper",
    }
}
```

For embedding providers, the settings page keeps the test button available next to the key icon, so
users can verify their own key right after entering it.

This is mutually exclusive with an embedded `APIKey` on the same provider: if both are present,
AI Studio ignores the embedded key and logs a warning, because the whole point of the flag is that
each user manages their own key. Combine the two across different providers if you need it -- one
provider with a shared, embedded key and another with `AllowUserProvidedAPIKey` -- but not on the
same provider.

The user's key follows the same "withdrawing a configuration" philosophy as everything else in this
document: if your configuration stops offering this provider, AI Studio removes the provider from
the settings but leaves the user's key in the OS keyring rather than deleting it, in case the same
provider comes back later. See [Withdrawing a configuration](#withdrawing-a-configuration).
