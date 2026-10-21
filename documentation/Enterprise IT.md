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

On Linux, AI Studio normally relies on the operating system's trusted root certificates for external HTTPS requests. In a Flatpak package, however, the application may not be able to read organization-specific root certificates from the host system. This can affect connections to self-hosted AI providers, embedding providers, transcription providers, ERI servers, the IMAP servers of mailboxes, and enterprise configuration servers.

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

Plugins deployed by your configuration server should define:

```lua
DEPLOYED_USING_CONFIG_SERVER = true
```

This holds for every plugin type, not just for configurations. Local, manually managed plugins should set this to `false`. If the field is missing on a plugin below `.config` or `.config-tests`, AI Studio falls back to the plugin path, treats the plugin as managed, and logs a warning.

Inside those two directories the field is a courtesy, not a requirement: the path already proves who the plugin belongs to. You need it for plugins you roll out **past** those directories, for example when your MDM solution places an assistant plugin under `plugins/assistants/`. Such a plugin has no path to prove its origin, and this field is the only marker it has.

What the field decides, and what it does not:

- **The plugin path alone** decides which configurations speak for your organization: which approvals for assistant plugins are honored, which configuration wins a conflict, and which configuration AI Studio withdraws once you stop referencing it. A configuration stored under `.config` is therefore removed when your organization no longer references its ID, whatever this field says. A plugin that declares `false` while sitting below `.config` does not escape any of this — AI Studio logs the contradiction and treats it as managed.
- **The path and the field together** decide whether a plugin is protected against the user. Whoever carries either marker cannot be deleted, edited, shared, or replaced through the user interface. Your IT department stays the only party that changes it.

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

## Deploying other plugin types

A deployment is not limited to a configuration, even though the directory it lands in is called `.config`. Your configuration server serves one archive per configuration ID, and you may use it for every kind of plugin: assistant plugins and model plugins today, further types such as tool plugins as they arrive. Read the directory name as "centrally configured and rolled out", not as "configurations only".

Put each plugin into its own subdirectory of the archive:

```
9072b77d-ca81-40da-be6a-861da525ef7b.zip
├── plugin.lua                      ← your configuration plugin, ID = 9072b77d-…
└── translation-assistant/
    └── plugin.lua                  ← an assistant plugin with an ID of its own
```

AI Studio extracts the whole tree and picks up every `plugin.lua` in it. A few rules apply:

- **Only the configuration plugin carries the configuration ID.** Every other plugin has its own `ID`, as any plugin does. The archive does not have to contain a configuration plugin at all: an archive that only ships an assistant plugin is fine.
- **Everything in the archive belongs to your organization.** Users cannot delete, edit, share, or replace any of it, whatever the individual plugins declare about themselves.
- **The withdrawal takes the whole deployment.** Once you stop referencing the configuration ID, AI Studio removes that directory including every plugin you shipped in it. See [Withdrawing a configuration](#withdrawing-a-configuration).
- **Assistant plugins still need an approval or an audit.** Deploying an assistant does not approve it. List its hash in `CONFIG["SETTINGS"]["DataAssistantPluginAudit.EnterpriseApprovedPlugins"]` of a configuration you deploy, otherwise users have to run a local security audit before they can activate it. An approval does not enable the assistant either; add `Activate` to the approval for that. See [Enterprise approval for assistant plugins](#enterprise-approval-for-assistant-plugins) and [Enabling an assistant plugin for your colleagues](#enabling-an-assistant-plugin-for-your-colleagues).

If you would rather not use the configuration server for this, roll the plugin out with your MDM solution into the ordinary plugin directory and mark it with `DEPLOYED_USING_CONFIG_SERVER = true`. It is then protected against changes just the same, but it is not tied to a configuration ID, so you have to remove it the same way you placed it.

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

Please note that the icon must be an SVG vector graphic. Raster graphics like PNGs, GIFs, and others aren’t supported. You can use the sample icon, which looks like a gear. AI Studio shows plugin icons in an isolated image element, so set the colors inside the SVG itself: `currentColor` and CSS rules from the app do not reach the icon. Your organization is responsible for holding the rights to any icon it ships. You can also give each configured provider its own icon, see [Giving providers your own icon](#giving-providers-your-own-icon).

Currently, you can configure the following things:
- Any number of LLM providers (self-hosted or cloud providers with encrypted API keys)
- Any number of transcription providers for voice-to-text functionality
- Any number of embedding providers for RAG
- Any number of ERI data sources for RAG
- Any number of profiles and chat templates, including the tools and data sources a template brings along
- Any number of policies for the Document Analysis assistant
- Enterprise hash approvals for assistant plugins
- Tool settings, encrypted tool API keys, and minimum provider confidence requirements
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

The field does count elsewhere, namely for protecting a plugin against the user, and that is not a contradiction: there the field only ever takes a possibility away from whoever declared it. Granting an approval works the other way round, so it needs a source no plugin can write.

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

`PluginHash` is required. All other fields are optional. `DisplayName`, `Comment`, `ApprovedBy`, and `ApprovedAtUtc` are shown in the UI as approval metadata; `Activate` and `AllowUserOverride` are described in the next section.

### Enabling an assistant plugin for your colleagues

An approval only says that an assistant plugin is safe. Whether it is enabled is a second decision, and it stays with your colleagues unless you make it: after a rollout the assistant is approved, but everybody still has to find it on the plugin page and switch it on. Two optional fields of an approval let you make that decision instead:

```lua
CONFIG["SETTINGS"]["DataAssistantPluginAudit.EnterpriseApprovedPlugins"] = {
    {
        ["PluginHash"] = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
        ["DisplayName"] = "Corporate Translation Assistant",
        ["Activate"] = true,
        ["AllowUserOverride"] = true,
    }
}
```

`AllowUserOverride` works exactly as it does for every other managed setting: without it, what you set is locked; with it, you only provide a default.

| `Activate` | `AllowUserOverride` | Result |
| --- | --- | --- |
| absent | any | Approved. Everybody enables the assistant themselves. This is the behavior of every approval written before these fields existed. |
| `true` | `true` | AI Studio enables the assistant once. Your colleagues may switch it off again, and their decision survives every restart. |
| `true` | absent | AI Studio enables the assistant and keeps it enabled. The switch on the plugin page is greyed out, and the security card says why. |

A default is applied exactly once per plugin, not on every start: otherwise it would keep overruling a colleague who deliberately switched the assistant off. AI Studio forgets that it applied the default as soon as no approval asks for it anymore, so rolling the same plugin out again later takes effect again.

#### Activating needs the rollout, not only the approval

AI Studio only enables an assistant plugin your organization actually rolled out: one below `.config` or `.config-tests`, or one you marked with `DEPLOYED_USING_CONFIG_SERVER`, as described in [Deploying other plugin types](#deploying-other-plugin-types).

The reason is the hash. An approval is matched by the plugin content alone, so it also covers a byte-identical copy a user placed in their own plugin directory. For an approval that is correct, because the hash is the code. For enabling a plugin on somebody's behalf it is not enough: you would be enforcing a copy you never shipped, cannot update, and cannot withdraw. Such a copy therefore stays approved, and nobody's settings are changed for it. AI Studio notes this in the log.

#### When several configurations approve the same plugin

`Activate` and `AllowUserOverride` are combined in opposite directions, so a department cannot quietly take back what your base configuration locked:

- One configuration asking for the activation is enough. Not asking for it says nothing against it.
- The freedom to switch the assistant off survives only when every configuration that asks for the activation grants it.

An approval that does not ask for the activation at all says nothing about that freedom and is not counted.

### Generating the hash

Use the build-script command from the repository root:

```bash
dotnet run --project app/Build -- assistant-plugin-hash "<plugin-dir>" --lua-snippet
```

This prints the canonical hash and, with `--lua-snippet`, also prints a ready-to-paste Lua snippet for `CONFIG["SETTINGS"]`.

## Local staging and testing

Before you roll a configuration out through a configuration web server, you can stage it on a device and test it end to end, including the enterprise approvals for assistant plugins described above. This needs no configuration web server, no registry, policy, or environment entry, and no encryption secret.

AI Studio has a dedicated directory for this: `.config-tests`. Anything stored there speaks for your organization exactly like a deployed plugin, and it takes every plugin type just like a real deployment does, so you can reproduce the directory structure of your later archive one to one. In exchange, AI Studio empties the directory on every start, so a test is valid for one session.

Do not use the `.config` directory for this. It belongs to your configuration web server, and AI Studio removes everything there that your organization does not reference anymore.

### The data and configuration directories

Plugins live in the data directory of AI Studio. The settings live in its configuration directory:

| Platform | Data directory | Configuration directory |
| --- | --- | --- |
| Windows | `%LOCALAPPDATA%\com.github.mindwork-ai.ai-studio\data` | `%APPDATA%\com.github.mindwork-ai.ai-studio` |
| macOS | `~/Library/Application Support/com.github.mindwork-ai.ai-studio/data` | `~/Library/Application Support/com.github.mindwork-ai.ai-studio` |
| Linux | `$XDG_DATA_HOME/org.mindworkai.AIStudio/data`, usually `~/.local/share/org.mindworkai.AIStudio/data` | `$XDG_CONFIG_HOME/org.mindworkai.AIStudio`, usually `~/.config/org.mindworkai.AIStudio` |
| Linux (Flatpak) | `~/.var/app/org.mindworkai.AIStudio/data/org.mindworkai.AIStudio/data` | `~/.var/app/org.mindworkai.AIStudio/config/org.mindworkai.AIStudio` |

The panel **Your Data Folders** on the Information page shows both directories as AI Studio resolved them on the device at hand.

### Staging a deployment

Place the files **while AI Studio is running**: the test directory is emptied whenever the app starts.

1. Start AI Studio. It creates `<data directory>/plugins/.config-tests/` if it does not exist yet.
2. Create a directory below it and place your `plugin.lua` there, e.g. `.config-tests/my-department-draft/`. The directory name is up to you here: a plugin is identified by the `ID` field inside it, not by the directory it lives in.
3. Place every other plugin of the deployment in a subdirectory of it, e.g. `.config-tests/my-department-draft/translation-assistant/`. This mirrors the archive you will serve later, as described in [Deploying other plugin types](#deploying-other-plugin-types).
4. AI Studio watches the plugin directory and picks everything up without a restart. The security card of the assistant then states that your organization approved it, exactly as it will after the rollout. If your approval sets `Activate`, the assistant is enabled right away, so you see the state your colleagues will start from.

You can also keep an assistant plugin you are only iterating on in `<data directory>/plugins/assistants/<any name>/`. Your test configuration approves it by hash either way. The difference is that a plugin outside `.config-tests` is not protected against the user, so this variant no longer mirrors the later rollout.

While a test configuration is loaded, the Information page reports it, including the directory it was staged in. After a restart, that same page tells you that a test configuration was removed, so nobody has to wonder where the directory went.

What behaves like the later rollout:

- The approvals for assistant plugins are honored.
- Settings and configuration objects the test configuration manages are protected against local configuration plugins.
- Everything staged there is protected against the user: no plugin of the test deployment can be deleted, edited, shared, or replaced through the user interface.
- When the test configuration declares the same plugin `ID` as one your organization deployed, the test configuration wins. This is how you try out the next version of an existing configuration under its final ID.

What deliberately does not:

- It does not survive a restart.
- It is not tied to a configuration ID, so nothing is withdrawn by removing an ID from your devices. A test ends as described in [Cleaning up](#cleaning-up).

### Testing with a small group

To let colleagues take part in the test, place the same directories on each of their devices while AI Studio runs, for example through a script, your MDM solution, or a login script. A configuration web server is not involved, and nothing has to be enabled inside AI Studio. Ordinary user accounts can take part: the data directory belongs to the user, so no administrator rights are needed to place the files.

Keep in mind that everybody in the group loses the test configuration the next time they start AI Studio. Either repeat the step, or let your script place the files at every login.

### Cleaning up

Restart AI Studio: the test directory is emptied, the approvals are gone, and the assistant requires a security audit again. Every setting your test configuration had taken over returns to the value it had before the test, as described in [Withdrawing a configuration](#withdrawing-a-configuration).

To end a test without restarting, delete the directory you created under `.config-tests` yourself. AI Studio watches the plugin directory and reacts right away, with the same result as a restart. There is no button for this on the plugin page: a test deployment carries the protection of a real one, so the user interface does not remove it. Whoever stages a test writes into the data directory anyway, so both ways are open to them.

### Security note

A test configuration carries the rights of an organization configuration without anybody having deployed it. Two properties keep that in check, and you should not work around either of them:

- The directory is emptied on every start, so nothing staged for a test can settle in unnoticed.
- No feature inside AI Studio writes into that directory. Importing, sharing, and deleting plugins never touch it, so a user cannot be talked into staging a configuration by opening a file.

The data directory belongs to the user account, so whoever can write there can approve assistant plugins in the name of your organization until the next restart. Treat write access to the data directory as equivalent to deploying a configuration, and protect it accordingly on managed devices.

## Exporting configurations from the app

You do not have to write your configuration plugin by hand. Set something up in AI Studio, export it, and paste the Lua fragment into your plugin.

Enable **Show administration settings** in the app settings once. It reveals the **Enterprise Administration** section and an **Export configuration** button next to each of these:

| What you can export | Where the button sits | Offered for |
|---|---|---|
| LLM providers | the provider list in the app settings | providers you created yourself |
| Embedding providers | the provider list in the app settings | as above, and only while the RAG preview is enabled |
| Transcription providers | the provider list in the app settings | providers you created yourself |
| Profiles | the profile dialog in the app settings | profiles you created yourself |
| Chat templates | the chat template dialog in the app settings | templates you created yourself |
| ERI data sources | the data source list in the app settings | ERI sources only, and not the ones using Kerberos |
| Document analysis policies | the Document Analysis assistant itself | the policy you have selected |
| Tools | **Tool Settings** in the app settings | every tool |

Anything your organization already manages has no export button: it came from a plugin to begin with. Local files and local directories have none either — such a data source exists on one machine only, so there is nothing to hand to your colleagues.

The button copies the fragment to your clipboard. Paste it into your [configuration plugin](../app/MindWork%20AI%20Studio/Plugins/configuration/plugin.lua), after the initialization of the table it extends, such as `CONFIG["LLM_PROVIDERS"] = {}`. One export writes to disk as well: a chat template whose attachments you package copies those files into a folder of your plugin and puts the Lua into your clipboard as usual.

**An export mints a new ID** for the exported object, so exporting the same provider or template twice deploys two of them to your colleagues. Once something is in your plugin, keep its ID and edit the rest around it. Document analysis policies are the exception: they keep the ID they have.

Some exports ask a question first: a provider with an API key offers to include it encrypted (see [Encrypted API Keys](#encrypted-api-keys)), an ERI data source does the same for its token or its credentials, a chat template with file attachments asks whether to keep their paths or copy them into your plugin, and a tool opens a dialog for the areas and the kind of management you want (see [Exporting tool configurations](#exporting-tool-configurations)).

**A tokenizer travels as a path, not as a file.** When an LLM or embedding provider uses a tokenizer of its own, the fragment names the copy AI Studio keeps of it, relative to the [data directory](#the-data-and-configuration-directories), e.g., `tokenizers/chat_<ID>/tokenizer.json` for an LLM provider and `tokenizers/embedding_<ID>/tokenizer.json` for an embedding provider. A configuration plugin reads this path relative to its own folder. Copy the file from your data directory to the same place in your plugin, or change the path to wherever you put it. A comment in the fragment says the same. The ID in the folder name is the one the provider has on your computer; it does not have to match anything else. Without the file, AI Studio counts tokens with its default tokenizer.

Handing a whole plugin to a colleague is a different thing: that is the **Share** function on the plugins page, which writes a `.mwplugin` archive and is governed by its own organization setting rather than by the administration settings.

### Importing an exported item

Not everything worth sharing calls for a configuration plugin. Say somebody built a good chat template and wants to hand it to three colleagues: they export it as above, and each colleague imports it as a template of their own. Every item in the table above except tools has an **Import** button next to its **Add** button, e.g., **Import Chat Template** or **Import Provider**. It opens a dialog to paste the exported fragment into.

- **Data only.** AI Studio reads the fragment with the same rules as a configuration plugin, but it never runs it. A fragment which contains anything other than values, such as a function call, a variable, or an expression, is refused.
- **An item of the user's own.** The imported item gets an ID of its own and belongs to no configuration plugin, even when the fragment came from one. Importing the same fragment twice gives two items.
- **Reviewed before it is saved.** Providers, profiles, chat templates, and data sources open in their usual form, filled in, so the user checks everything before saving. A document analysis policy is added right away and selected, and the user edits it in the assistant like any other.
- **Hints first.** Before that, the dialog lists what the user should know: a preselected provider, profile, data source, or tool which does not exist on this computer, a tokenizer which is not on this computer and is therefore left out, an API key which cannot be decrypted, and every local file which a chat template would attach to each of its chats. Attachments which are not on this computer are relinked or removed in the form. An exported provider names the copy of its tokenizer on the computer it came from, so the tokenizer comes along only when the fragment is imported there.
- **Secrets.** An API key, token, or password which the export included in encrypted form is decrypted only where the encryption secret of your organization is configured, see [Encrypted API Keys](#encrypted-api-keys). Without it, a provider is imported without its key, and an ERI data source with a token or password cannot be imported at all. Data sources which sign in with Kerberos cannot be imported either.
- **No connection on its own.** Importing contacts no server. The user loads the models of a provider, or tests the connection of a data source, in the form.

Exporting needs **Show administration settings**; importing does not.

#### Allowing or locking imports

Each of these settings is `true` by default:

| Setting | Allows importing |
|---|---|
| `DataApp.AllowUserToImportConfigurationSnippets` | anything at all |
| `DataApp.AllowUserToImportProfile` | profiles |
| `DataApp.AllowUserToImportLLMProvider` | LLM providers |
| `DataApp.AllowUserToImportEmbeddingProvider` | embedding providers |
| `DataApp.AllowUserToImportTranscriptionProvider` | transcription providers |
| `DataApp.AllowUserToImportChatTemplate` | chat templates |
| `DataApp.AllowUserToImportERIDataSource` | ERI data sources |
| `DataApp.AllowUserToImportDocumentAnalysisPolicy` | document analysis policies |

An import needs both the first setting and the one of its type. A provider also needs what adding one by hand needs: `DataApp.AllowUserToAddProvider` and the setting of its kind, such as `DataApp.AllowUserToAddLLMProvider`. A locked **Import** button stays visible with a lock. The permission is checked once more when the user saves, so locking imports while somebody fills in a form still holds. Importing plugin archives has a setting of its own, `DataApp.AllowUserToImportPlugins`.

## Encrypted API Keys

You can include encrypted API keys in your configuration plugins for cloud providers (like OpenAI, Anthropic) or secured on-premise models. This feature provides obfuscation to prevent casual exposure of API keys in configuration files.

**Important Security Note:** This is obfuscation, not absolute security. Users with administrative access to their machines can potentially extract the decrypted API keys with sufficient effort. This feature is designed to:
- Prevent API keys from being visible in plaintext in configuration files
- Protect against accidental exposure when sharing or reviewing configurations
- Add a barrier against casual snooping

### Setting Up Encrypted API Keys

1. **Generate an encryption secret:**
   In AI Studio, click the "Generate encryption secret and copy to clipboard" button in the "Enterprise Administration" section of the app settings, which [Show administration settings](#exporting-configurations-from-the-app) reveals. This generates a cryptographically secure 256-bit key and copies it to your clipboard as a base64 string.

2. **Deploy the encryption secret:**
   Distribute the secret to all client machines using any supported enterprise source. The secret can be deployed on its own, even when no enterprise configuration IDs or server URLs are defined on that machine:
   - Windows Registry / GPO: `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT\config_encryption_secret`
   - Policy file: `config_encryption_secret.yaml`
   - Environment fallback: `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET`

   You must also deploy the same secret on the machine where you will export the encrypted API keys (step 3).

3. **Export encrypted API keys from AI Studio:**
   Once the encryption secret is deployed on your machine, configure the provider with its API key and [export it](#exporting-configurations-from-the-app). AI Studio asks whether to include the key; the exported Lua code then contains it in the format `ENC:v2:<base64-encoded data>` or `ENC:v1:<base64-encoded data>`, see [Formats of encrypted values](#formats-of-encrypted-values).

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
    ["APIKey"] = "ENC:v2:MTIzNDU2Nzg5MDEyMzQ1NkFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFla...",
    ["AdditionalJsonApiParameters"] = "",
    ["Model"] = {
        ["Id"] = "gpt-4",
        ["DisplayName"] = "GPT-4",
    }
}
```

The API key will be automatically decrypted when the configuration is loaded and stored securely in the operating system's credential store (Windows Credential Manager / macOS Keychain).

### Formats of encrypted values

AI Studio encrypts API keys, tokens, and passwords in one of two formats. Both use the same encryption secret, so you neither generate nor deploy a new one.

| Format | Starts with | Read by | A value encrypted with another secret |
|---|---|---|---|
| Protected | `ENC:v2:` | AI Studio v26.10.1 or newer | is always refused |
| Compatible | `ENC:v1:` | every version of AI Studio | is usually refused, but in rare cases decrypted into a wrong key |

AI Studio reads both formats, and it keeps reading `ENC:v1:` for good: the configuration plugins you have already deployed keep working.

**Which format your exports write** is set by **Format for encrypted secrets in exports** in the **Enterprise Administration** section of the app settings. New installations start with the protected format. Installations which existed before v26.10.1 keep the compatible format until you switch, because a version of AI Studio before v26.10.1 takes an `ENC:v2:` value for plaintext and leaves the key out.

**When you roll out updates yourself**, your administrators run a new version weeks before everybody else. Keep the compatible format until every computer in your organization runs v26.10.1 or newer, then switch to the protected one. You can also set the format centrally:

```lua
CONFIG["SETTINGS"]["DataApp.EnterpriseEncryptionExportFormat"] = "V1"
```

Change the value to `"V2"` once the update has arrived everywhere. While a configuration sets the format, the choice in the app settings is locked; with `DataApp.EnterpriseEncryptionExportFormat.AllowUserOverride` set to `true`, the configured format becomes a default instead.

Switching the format leaves the values in your configuration plugins as they are. To protect a value you have already deployed, export it again and replace it in your plugin.

## Exporting tool configurations

A tool export is the one that asks the most before it writes anything. It assumes `CONFIG` and `CONFIG["SETTINGS"]` already exist in your plugin.

1. In **Tool Settings**, configure the tool and save your changes, then [export it](#exporting-configurations-from-the-app).
2. Select the areas to export. All areas start selected. For Web Search, SearXNG, Staan, Tavily, and General are independent: selecting only Tavily does not include the search language, strategy, or preferred backend. Select General separately when you need those settings.
3. Choose **Locked settings** or **Editable defaults**. Locked settings go into `DataTools.LockedToolSettings` and cannot be changed by users. Editable defaults go into `DataTools.DefaultToolSettings`; a user's saved value takes precedence over them.
4. Optionally select **Include encrypted API keys and other secrets**, which starts off. The option is available only when the selected areas contain configured secrets and this machine has a valid enterprise encryption secret. Deploy the same secret to recipients as described in [Setting Up Encrypted API Keys](#setting-up-encrypted-api-keys). Secrets always go into `LockedToolSettings`, including when you choose editable defaults for the other fields. Managed tool secrets are used from the configuration without replacing the user's own keyring entries; removing the managed secret makes the user's own key available again.
5. Review **Include minimum provider confidence**, which starts on. The exported requirement applies to the whole tool, or to the whole tool collection, and is locked, because a managed setting without an `AllowUserOverride` flag is locked by default. The export therefore only adds a comment about that flag instead of writing it: setting it applies to the entire confidence table, including entries for other tools, so that decision stays yours. Deselect this option if your fragment should not configure provider confidence.
6. Click **Export to clipboard**, then paste the fragment into your plugin after its `CONFIG["SETTINGS"] = {}` initialization and after any assignments that replace the tables you want to extend. Review the code and test the plugin using [Local staging and testing](#local-staging-and-testing) before rollout. The export dialog stays open so you can produce another selection.

The export reads saved, effective settings, including organization-managed values. It does not save settings or change the keyring. Missing values are omitted, explicitly empty non-secret values are preserved, and implicit runtime defaults are not added. Incomplete configurations can be exported so that you can finish them in Lua. If encryption fails, no partial fragment is copied; an empty export also leaves the clipboard unchanged.

Some tools only make sense together and form a tool collection, such as **Mailboxes** with Search Mails, Read Mail, and Count Mails. **Tool Settings** shows such a collection as one row, and its export covers all of its tools: each area names its tool, and the minimum provider confidence goes under the ID of the collection, e.g., `mailboxes`. Wherever your configuration names tools — `DataTools.DisabledToolIds`, `DataTools.MinimumProviderConfidenceByToolId`, chat templates, and document analysis policies — use the ID of the collection for them. The ID of one of its tools counts for the whole collection, so naming it switches off the whole collection or sets its confidence; of several levels set for a collection and its tools, the highest applies. Tool settings stay with each tool and keep the `"<toolId>.<fieldName>"` keys.

### Complete tool export

For example, save a timeout of `30`, a content limit of `12000`, an empty private-host list, and free address choice switched off for **Read Web Page**. Select its General area, **Locked settings**, and **Include minimum provider confidence**. With its default confidence requirement of `VERY_LOW`, the export is:

```lua
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"] = CONFIG["SETTINGS"]["DataTools.LockedToolSettings"] or {}
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"]["read_web_page.timeoutSeconds"] = "30"
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"]["read_web_page.maxContentCharacters"] = "12000"
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"]["read_web_page.allowedPrivateHosts"] = ""
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"]["read_web_page.freeAddressChoice"] = "OFF"

CONFIG["SETTINGS"]["DataTools.MinimumProviderConfidenceByToolId"] = CONFIG["SETTINGS"]["DataTools.MinimumProviderConfidenceByToolId"] or {}
CONFIG["SETTINGS"]["DataTools.MinimumProviderConfidenceByToolId"]["read_web_page"] = "VERY_LOW"
-- The whole table is locked unless you set CONFIG["SETTINGS"]["DataTools.MinimumProviderConfidenceByToolId.AllowUserOverride"] = true
```

### Exporting only one search backend

For **Web Search**, select only Tavily, choose **Editable defaults**, enable encrypted secrets, and deselect **Include minimum provider confidence**. With a saved search depth of `basic` and an API key, the fragment has this form. The ciphertext below is a placeholder; use the encrypted value generated by your export.

```lua
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"] = CONFIG["SETTINGS"]["DataTools.LockedToolSettings"] or {}
CONFIG["SETTINGS"]["DataTools.LockedToolSettings"]["web_search.tavily.apiKey"] = "ENC:v2:<base64-encoded encrypted data>"

CONFIG["SETTINGS"]["DataTools.DefaultToolSettings"] = CONFIG["SETTINGS"]["DataTools.DefaultToolSettings"] or {}
CONFIG["SETTINGS"]["DataTools.DefaultToolSettings"]["web_search.tavily.searchDepth"] = "basic"
```

This does not change the SearXNG or Staan settings, the general Web Search settings, or its confidence requirement. Web Search still needs a configured `defaultLanguage`; include it through a separate General-area export, add it manually, or let the user configure it. A backend-only export does not restrict the tool to that backend.

You can combine both fragments in the same plugin: their table initializations preserve earlier entries, and only a later assignment to an identical key replaces its value. A later whole-table assignment such as `CONFIG["SETTINGS"]["DataTools.LockedToolSettings"] = { ... }` replaces those entries, so place exports after it or merge them manually. This behavior applies within one plugin; across separate configuration plugins, the winning plugin replaces the whole managed table as described in [Settings that hold a list or a table](#settings-that-hold-a-list-or-a-table).

## Chat templates with tools and data sources

On top of its system prompt and the rest, a chat template decides the tools and the data sources a chat started with it begins with. The data source IDs in an exported template are **carried over unchanged**, unlike the template's own ID: they point at the data sources of your organization. Check them against your `CONFIG["DATA_SOURCES"]` -- an ID that resolves to nothing is ignored, and a chat with that template then starts without that source.

Writing such a template by hand means knowing that saying nothing and saying none are two different statements:

| What the template says | What a chat started with it does |
|---|---|
| no `ToolIds` at all | starts with the tools the user has set as their chat default |
| `ToolIds` present but empty | starts with no tool at all, whatever that default says |
| no `DataSourceOptions` at all | starts with the data source options the user has set as their chat default |
| `DataSourceOptions` present | starts with exactly those, including how the sources are searched and whether an agent picks them |

Writing the `DataSourceOptions` table at all is already the statement that this template wants data sources, so `DisableDataSources` starts at `false` inside it, unlike everywhere else in the app.

`RetrievalMode` inside that table decides how the chat searches its data sources:

| Value | What happens |
|---|---|
| `SEMANTIC_SEARCH` (default) | The AI searches the data sources itself, through the tool `semantic_search`, whenever a question calls for it. No agent takes part: `AutomaticDataSourceSelection` lets the AI itself choose among all data sources it may use, and `AutomaticValidation` has no effect. |
| `EVERY_MESSAGE` | AI Studio searches the data sources with every message, before the AI answers, with the agents for selection and validation as configured. |

Semantic search only works where `semantic_search` can be offered: the model has to be able to call tools, neither `DataTools.EnableTools` nor `DataTools.DisabledToolIds` may switch it off, and the provider has to meet a minimum confidence you set for it in `DataTools.MinimumProviderConfidenceByToolId`. Otherwise, the chat searches with every message instead. A template that leaves `RetrievalMode` out gets `SEMANTIC_SEARCH`, not the chat default. That chat default is the setting `DataChat.PreselectedDataSourcesRetrievalMode`, with the same two values.

When an [assistant plugin](../app/MindWork%20AI%20Studio/Plugins/assistants/README.md) opens a chat directly and its chat template names tools, data sources, a provider, or a profile, that template decides them alone; what the launcher names is dropped with a warning in the log. Its README explains the rule and how such sources are checked.

## Chat templates with a provider and a profile

A chat template may also decide the provider, and with it the model, and the profile a chat started with it begins with. Use case: a template for working with mails preselects the mailbox as its data source, the provider whose confidence level the mailbox requires, and a profile that suits mail work, so that users no longer switch both by hand for every chat.

| What the template says | What a chat started with it does |
|---|---|
| no `ProviderId` at all | starts with the provider it would start with anyway |
| `ProviderId` present | starts with that provider, if it exists and meets the confidence level required for chats |
| no `ProfileId` at all | starts with the profile the user has set as their default |
| `ProfileId` = `00000000-0000-0000-0000-000000000000` | starts without a profile, whatever that default says |
| `ProfileId` present | starts with that profile, if it exists |

Both are a preselection, not a lock: users change them in the chat as usual. A provider or profile that does not exist, and a provider below the confidence level of chats, are ignored, and the chat starts as if the template said nothing. A `ProfileId` means nothing when `AllowProfileUsage` is `false`. The data sources a template preselects still have to be permitted for the provider in use.

The IDs may name a provider from `CONFIG["LLM_PROVIDERS"]` or a profile from `CONFIG["PROFILES"]` of the same configuration, whose IDs are the same on every computer, or one of another configuration of your organization. A chat template exported from the app carries the provider and profile IDs of the computer it was exported from, mostly the user's own, and the export starts with a comment saying so. Replace them with IDs from your configuration.

## Mailboxes

Mailboxes are still a preview: enable `PRE_MAILBOXES_2026` together with `PRE_RAG_2024` in `DataApp.EnabledPreviewFeatures`. Users add a mailbox as a data source, with their own username and password for its IMAP server. AI Studio synchronizes it every 16 minutes and keeps a local index of it, which the tool collection `mailboxes` searches, reads, and counts.

These settings decide how your organization uses them:

| Setting | Effect |
|---|---|
| `DataApp.AllowUserToAddMailbox` | `false` keeps users from adding mailboxes. Mailboxes they added before stay. |
| `DataMailboxes.MinimumOutboundDataRestriction` | The least strict outbound data restriction a mailbox may have, see below. |
| `DataMailboxes.AllowOnlyOrganizationMailServers` | `true` allows mailboxes only on the mail servers you offer, see below. |
| `DataTools.DisabledToolIds` with `mailboxes` | Keeps the AI from reading any mailbox. |

Every mailbox also requires a provider confidence of its own, from `VERY_LOW` to `HIGH`. The chat provider and the embedding provider have to meet it before they see a mail. An IMAP server whose certificate chains to your private root CA works with the same settings as HTTPS, see [Custom root certificates for Flatpak deployments](#custom-root-certificates-for-flatpak-deployments).

### Where a chat may send data

Mails come from strangers and may contain instructions meant for the AI. That is why each mailbox decides where a chat may still send data once it has read mails from it:

| Value | What the chat may still do |
|---|---|
| `ONLY_CONFIGURED_SERVICES` | Use only services configured in AI Studio, such as the mailbox itself or your Confluence. New mailboxes start here. |
| `ONLY_LINKS_FROM_CHAT` | Also read web pages whose addresses stand in the chat, written by the user or returned by a tool. No web search, and no addresses the AI chooses itself. |
| `UNRESTRICTED` | Use every tool the user selected. |

With `DataMailboxes.MinimumOutboundDataRestriction`, you rule out the less strict levels. A mailbox set to one of them gets your level whenever its mails reach a chat, and its dialog no longer offers them. A chat which read mails before keeps the level it got then, until it reads mails again.

### Mail servers of your organization

`CONFIG["MAILBOX_PROVIDERS"]` offers your own mail servers in the mailbox dialog, ahead of the well-known public providers. Each entry names the server with its host, port, and encryption, and may add a hint on the username and a link to your instructions; users still sign in with their own username and password. A configuration may define several mail servers, and those of all your configurations are offered together. `plugin.lua` lists the fields.

With `DataMailboxes.AllowOnlyOrganizationMailServers`, AI Studio connects to no other IMAP server. Before every connection, it compares the host of the mailbox with the hosts of your mail servers; port and encryption make no difference. A mailbox somebody added on another server before stops synchronizing at once, the embeddings page says why, and the AI no longer reads it. Its local index stays, so the mailbox comes back as it was, should you allow its server again. Switch it on together with your first mail servers, and nobody gets to add a mailbox elsewhere at all.

## Backing up and restoring data

Users can back up the data of AI Studio into a single `.mwbackup` file, for example to take it to a new computer, and restore it there. They find both under **Data Backup & Restore**, on the Information page and in the settings. Instead of replacing all their data, users may also add only the chats of a backup to the chats they have. Secrets such as API keys never go into a backup. These settings decide whether they may:

| Setting | Effect |
|---|---|
| `DataApp.AllowUserToBackUpData` | `false` keeps users from writing a backup. The button stays visible but is disabled. |
| `DataApp.AllowUserToRestoreData` | `false` keeps users from restoring a backup and from adding the chats of one. The buttons stay visible but are disabled. |

Both settings start as `true`. A backup holds the chats, the text of indexed documents, and indexed mails in a single file. The file is not encrypted, and it can leave the computer on any USB stick or cloud drive. Where that must not happen, set `DataApp.AllowUserToBackUpData` to `false`.

### What a backup holds

A backup takes along what AI Studio needs to carry on with the same data on another computer:

- the settings, without what your configurations manage, see [Settings and objects of your configurations](#settings-and-objects-of-your-configurations),
- the chats in workspaces and the temporary chats, along with their transcripts,
- the plugins of the user, such as the assistants they built,
- the tokenizers chosen for providers,
- the projects of the Visual Briefing Assistant,
- the local index of data sources and mailboxes, together with its embeddings, so that nothing has to be embedded again. While a backup copies the index, the indexing pauses, and it carries on afterward.

What stays out:

- **Secrets.** API keys, passwords, and tokens stay in the OS keyring. After a restore on another computer, AI Studio lists the providers, data sources, mailboxes, and tools of the user which need theirs again. The secrets of your configurations come back with your configurations.
- **Plugins of your organization.** Everything below `.config` and `.config-tests` stays out, and so does every plugin elsewhere which is marked with `DEPLOYED_USING_CONFIG_SERVER = true`, see [Important: Mark enterprise-managed plugins explicitly](#important-mark-enterprise-managed-plugins-explicitly). Your deployment brings them back. A plugin which your MDM solution placed without that marker counts as one of the user's own: it goes into backups, and a restore can replace or remove it.
- **Files outside the folders of AI Studio**, such as documents attached to a chat, the folders of local data sources, the folders of the batch processing, or a bundle of root certificates. A backup keeps only their locations. After a restore, AI Studio lists the settings whose files or folders it cannot find on the new computer.
- Plugins which come with AI Studio, tools AI Studio downloaded such as Pandoc, log files, and temporary files.

### Settings and objects of your configurations

Before the settings go into a backup, AI Studio leaves out every object which a configuration plugin manages: LLM, embedding, and transcription providers, profiles, chat templates, data sources, and document analysis policies. On a computer which receives your configurations, these objects come back from there, along with their API keys, and nothing appears twice. The same holds for configuration plugins the user installed themselves: they go into the backup as plugins and bring their objects back after a restore.

A setting which your configuration locks stays in the backup, together with the configuration which locked it. After a restore, AI Studio treats it like the setting of a withdrawn configuration until your configuration locks it again, see [Withdrawing a configuration](#withdrawing-a-configuration). On a device which receives your configurations, the setting therefore ends up as you configured it. On one without them, the user gets back the value they had chosen before your configuration took the setting over.

### How a restore works

A restore replaces the data of the user with the backup, and it needs a restart: while AI Studio runs, its files are open, and on Windows, they cannot be replaced at all.

1. AI Studio checks the backup. It refuses a backup made by a newer version of AI Studio, and one whose files do not match its list of contents.
2. AI Studio unpacks the backup into a folder `.restore-pending`, one within the data directory and one within the configuration directory. This needs free space for the unpacked backup on top of the current data. Until the restart, the indexing pauses, and AI Studio asks the user to restart now or to discard the restore.
3. At the next start, before AI Studio opens any of its files, it moves the current data into a folder `.restore-previous` and the restored data into its place, piece by piece. A piece for which the backup holds nothing stays as it was.
4. Should a step fail, AI Studio moves everything back, and the user keeps their data as it was. Should even that fail, AI Studio tries again at every start.
5. Once the restored settings have loaded, AI Studio deletes the replaced data from `.restore-previous`, reports the outcome, and lists what remains to do, such as entering API keys again.

A restore leaves the plugins of your organization, the plugins which come with AI Studio, and the log files as they are. When a restore fails, the log file `events.log` in the data directory tells why, in the entries from the source `Restore`.

### Backing up with a tool of your own

If you back up the user profiles of your devices anyway, for example with Restic, Borg, or the backup solution of your organization, the Information page helps: its panel **Your Data Folders** shows where the data and configuration directories lie, and which of their contents belong into a backup. [The data and configuration directories](#the-data-and-configuration-directories) lists their usual places. The panel derives this from the same list the backups of AI Studio follow, and it stays complete even when you set `DataApp.AllowUserToBackUpData` to `false`.

Keep in mind:

- Copy the local index below `databases/` only while AI Studio is closed. While it runs, these files change, and on Windows, they are locked. A copy taken then may not be readable afterward.
- Secrets do not lie in these folders. They stay in the OS keyring.
- On macOS, the data directory lies within the configuration directory.
- No pattern of paths tells the plugins which your MDM solution placed outside `.config` apart from the user's own plugins. The panel lists the folders of those marked with `DEPLOYED_USING_CONFIG_SERVER = true`, so you can leave them out.
- AI Studio stores the paths to its own files, such as transcripts and tokenizers, relative to the data directory. A copy therefore also works on a computer whose folders lie elsewhere, for example under another user name, as long as AI Studio v26.10.1 or later ran before the copy was taken. Paths to the user's own files, such as attachments or the folders of data sources, stay as they are.

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

### Asking users for their API key

With `AllowUserProvidedAPIKey` alone, users have to find the provider in the settings and enter
their key there. Set `PromptForUserAPIKey` instead, and AI Studio asks for the key itself: as long
as such a provider has no API key, the home page shows a panel with an input field for the key.
The panel comes first and is open, even when your configuration hides the other panels of the
home page. AI Studio also stays on the home page after starting, even when another start page is
configured, until the key is there. `PromptForUserAPIKey` implies `AllowUserProvidedAPIKey`, and
it works for LLM, embedding, and transcription providers alike. Other API keys, such as the one of
a web search, are not covered: they belong to the tool settings.

All providers of one configuration which set the option share one panel and one key: the user
enters the key once, and AI Studio stores it for every one of them. Therefore, mark only providers
which accept the same key, for example several models behind one Open WebUI server. Providers
which need another key belong in a configuration of their own.

Before storing the key, AI Studio loads the models of the first of these providers which can list
them. A key the provider rejects is not stored, and the user is told so. When the
provider cannot be reached, or none of the providers can list its models, the key is stored with
a warning, so that an outage does not keep anyone from finishing the setup. A few providers, such
as Perplexity, answer with a fixed list of models whatever the key; AI Studio cannot tell a wrong
key there.

Explain how users obtain their key with `CONFIG["USER_API_KEY_PROMPT"]`. Its Markdown is shown
above the input field and is required; the title is optional:

```lua
CONFIG["USER_API_KEY_PROMPT"] = {
    ["Title"] = "Please add your API key",
    ["Markdown"] = [===[
        To use the AI models of your organization, you need a personal API key:

        1. Open [Open WebUI](https://example.org) and sign in.
        2. Click your profile picture, then "Settings" and "Account".
        3. Create an API key, copy it, and paste it below.
    ]===]
}
```

## Describing your own models

AI Studio knows what the models of the large vendors can do, and reads that knowledge from their
model cards. It cannot know what your own models can do: a fine-tune of your own, a model behind an
internal name, or an engine you configured differently from what the model card says. Two places let
you say it, and they answer different questions.

**One installation of a model: `CapabilityOverrides` on the provider.** Use this when you want to
correct a detail for one provider entry -- an endpoint which accepts no images, or a context window
your operator configured smaller than the model card advertises. It sits right in the provider entry
of your configuration plugin:

```lua
CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
    ["Id"] = "9072b77d-ca81-40da-be6a-861da525ef7b",
    ["InstanceName"] = "Research cluster",
    ["UsedLLMProvider"] = "SELF_HOSTED",
    -- ...
    ["CapabilityOverrides"] = {
        ["MULTIPLE_IMAGE_INPUT"] = false,
        ["CONTEXT_WINDOW"] = 32768,
        ["MAX_IMAGES_PER_REQUEST"] = 4,
    },
}
```

Every key is optional and contradicts only what it names; everything else keeps the answer AI Studio
works out by itself. The full list of keys is documented in
`app/MindWork AI Studio/Plugins/configuration/plugin.lua`. Two notes worth knowing:

- **`CONTEXT_WINDOW` feeds the token counter below the chat input.** A wrong number there misleads
  your users about how much room they have left in a conversation.
- **Your users can set the same values themselves**, in the expert settings of a provider. For a
  provider you deploy, the fields show your numbers and stay locked.

**A model wherever it is reached: a model plugin.** Use this when you run a model of your own and
want AI Studio to treat it correctly everywhere it appears, rather than correcting one provider entry
at a time. A model plugin is its own plugin with `TYPE = "MODEL"` and an ID of its own, deployed in
its own subdirectory of your configuration archive, exactly like an assistant plugin -- see
[Deploying other plugin types](#deploying-other-plugin-types).

`app/MindWork AI Studio/Plugins/models/plugin.lua` is a complete, commented example. In short, each
entry names the model names it describes and then states what those models can do: the capabilities,
how the model reasons, what kind of model it is, its context window, its tokenizer, and how many
images it takes.

Three things decide whether it does what you expect:

- **An entry replaces everything AI Studio would otherwise say about the names it matches.** Write it
  as if AI Studio had never heard of these models: `CAPABILITIES` is therefore required, and it has
  to name the APIs the model answers through. This is also why a single correction belongs in
  `CapabilityOverrides` instead.
- **Write the pattern the way a model name is written**: lower case, hyphens between the parts. A
  pattern written differently can never match anything and is rejected with a message saying so.
- **Name the page and the day.** `SOURCE_URL` and `SOURCE_CHECKED_ON` are required, for the same
  reason AI Studio's own model rules carry them: your entry will outlive whoever wrote it, and a
  statement nobody can check ages into a wrong answer.

A model plugin only ever *describes*. It names no server, carries no API key, and runs no code,
which is why it needs neither an approval nor a security audit the way an assistant plugin does. The
same path-based authority applies as to everything else you deploy: what arrives under your
configuration ID belongs to your organization, and users can neither edit nor remove it. AI Studio
offers users no way to import a model plugin of their own; should one be placed in the local plugin
directory by hand, anything your organization deployed wins over it.

Where two of your own model plugins describe exactly the same model names, the optional `PRIORITY`
decides. Plugins describing different models never get in each other's way, and both are used.

## Giving providers your own icon

By default, AI Studio shows the logo of the underlying AI provider next to each provider entry. When
you would rather show your own project or department logo, point the optional `IconPath` field at an
SVG file inside your configuration plugin:

```lua
CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
    ["Id"] = "9072b77d-ca81-40da-be6a-861da525ef7b",
    ["InstanceName"] = "Corporate OpenAI GPT-4",
    ["UsedLLMProvider"] = "OPEN_AI",
    ["Host"] = "NONE",
    ["Hostname"] = "",
    ["IconPath"] = "assets/project-icon.svg",
    ["Model"] = {
        ["Id"] = "gpt-4",
        ["DisplayName"] = "GPT-4",
    }
}
```

The field works the same way for embedding and transcription providers.

The path is relative to your `plugin.lua` and must stay inside the plugin directory. AI Studio
rejects absolute paths, `..` segments, links pointing out of that directory, files which do not end
in `.svg`, files larger than 32 KiB, and anything which is not well-formed SVG. A rejected or
missing icon is never fatal: AI Studio logs a warning, and the provider loads with its built-in
logo.

Two things to keep in mind when you prepare the icon:

- **Colors belong inside the SVG.** AI Studio shows every icon in an isolated image element, so
  `currentColor` and CSS rules from the app do not reach it. Choose colors which work on both light
  and dark surfaces.
- **Your organization holds the rights.** Whatever icon you ship -- for a provider through
  `IconPath`, or for the configuration plugin itself through its `icon.lua` -- your organization is
  responsible for holding the rights to use it.

## Labeling AI-generated content

Rules on transparency, such as the EU AI Act, may require that a text says when AI generated it, for
example, when your colleagues publish it. AI Studio can add such a notice: the AI answers which users
copy or export then end with a paragraph of its own, in bold, which says that the content was
generated using artificial intelligence (AI). The notice is written in the language of the app. This
is off by default.

### What gets the notice

- **Copied answers:** the copy button of an answer in the chat or in an assistant, and the "Copy
  result" button of the assistants.
- **Exported answers:** every format of the export menu, i.e., Word, OpenDocument, LaTeX, web page,
  and Markdown.
- **Files of the batch processing,** when it writes one file per document.

Content which one more sentence would break, or which goes on to a program or another model instead
of a reader, stays unchanged:

- code blocks copied on their own, and single files or tables exported out of an answer,
- the CSV table of the batch processing,
- the Lua code of the assistant builder and the prompt of the prompt optimizer,
- visual briefings, which AI Studio exports as the very version it checked, byte for byte,
- messages of the users, transcripts, IDs, and paths.

The notice covers what AI Studio copies and exports itself. Text which somebody selects and copies
by hand carries no notice, so the setting supports the rules of your organization, but it cannot
enforce them.

### Switching on the notice

Switch it on for everybody, without a way to opt out:

```lua
CONFIG["SETTINGS"]["DataApp.AddAIGeneratedContentDisclosure"] = true
```

Add `AllowUserOverride` to make it the default of your organization, which users may still change
in the app settings:

```lua
CONFIG["SETTINGS"]["DataApp.AddAIGeneratedContentDisclosure"] = true
CONFIG["SETTINGS"]["DataApp.AddAIGeneratedContentDisclosure.AllowUserOverride"] = true
```

Set it to `false` without `AllowUserOverride` to keep everybody from switching it on. Either way,
users find the setting in the app settings; when you lock it, they see your value there.

## Feature usage statistics (telemetry)

When your organization runs its own AI servers, for example behind a LiteLLM gateway, you may want
to know which features of AI Studio your colleagues use: the chat, the assistants, the agents. AI
Studio can name the feature in the `User-Agent` header of every request it sends to these servers.
Your servers then log it like any other header; LiteLLM, for example, shows it as a tag of the
request. This is off by default, and AI Studio then sends no `User-Agent` at all.

### What your servers receive

The `User-Agent` names the app, its version, the platform, and the feature which sent the request:

| Request sent by | `User-Agent` |
|---|---|
| The chat | `MindWorkAIStudio/26.10.1 (win-x64) Component/CHAT` |
| The translation assistant | `MindWorkAIStudio/26.10.1 (win-x64) Component/TRANSLATION_ASSISTANT` |
| The data source selection agent | `MindWorkAIStudio/26.10.1 (win-x64) Component/AGENT_DATA_SOURCE_SELECTION` |
| An assistant plugin of your organization | `MindWorkAIStudio/26.10.1 (win-x64) Component/DYNAMIC_ASSISTANT Assistant/Cafe-Menu-Writer-Beta` |
| Transcription, embeddings, and model lists | `MindWorkAIStudio/26.10.1 (win-x64)` |

The platform is one of `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and
`osx-arm64`. The feature names are the values of the enumeration in
`app/MindWork AI Studio/Tools/Components.cs`.

Assistant plugins are named only when they come with AI Studio, when your organization deployed
them, or when it approved them, see
[Enterprise approval for assistant plugins](#enterprise-approval-for-assistant-plugins). Users can
build assistants for themselves and name them as they like, possibly after a person or a private
project; such a name never leaves the device, and the request shows up as
`Component/DYNAMIC_ASSISTANT` only. For the header, a name is reduced to ASCII letters, digits,
dots, underscores, and dashes, and cut after 64 characters: "Café Menu Writer (Beta)" becomes
`Cafe-Menu-Writer-Beta`.

The `User-Agent` names neither the user nor their device; the platform only says which operating
system and processor architecture AI Studio runs on. The request itself carries what it always
carries, such as the prompt and the API key; the `User-Agent` adds only which feature sent it.

### Who receives it

Only providers of the types "self-hosted" and "LiteLLM" receive it. AI Studio decides by the type
of the provider, not by its address: a LiteLLM provider receives it wherever its server runs.

- **Cloud providers never receive it**, such as OpenAI, Anthropic, Google, or Mistral, whatever the
  setting says.
- **Other services never learn which feature sent a request**, such as ERI servers, Confluence, or
  web pages.
- **AI Studio sends no usage data to its developers**, with or without this setting.

### What the numbers mean

- **They count requests, not uses.** A single message in the chat may cause several requests:
  each round of tool calls is a request of its own, and the classic RAG process lets its agents
  select and check the data sources before the answer. Some assistants work in several steps, and
  the batch processing sends one request per file.
- **Agents appear under their own name.** When the chat lets an agent select its data sources, the
  request of the agent shows up as `Component/AGENT_DATA_SOURCE_SELECTION`, not as part of the chat.
- **Transcription, embeddings, and model lists name the app only.** Which kind of request it was,
  your server sees from the endpoint; which feature caused it is not sent.

### Switching it on

Switch it on for everybody, without a way to opt out:

```lua
CONFIG["SETTINGS"]["DataApp.ShareFeatureUsageWithSelfHostedServerOperators"] = true
```

Add `AllowUserOverride` to make it the default of your organization, which users may still change
in the app settings:

```lua
CONFIG["SETTINGS"]["DataApp.ShareFeatureUsageWithSelfHostedServerOperators"] = true
CONFIG["SETTINGS"]["DataApp.ShareFeatureUsageWithSelfHostedServerOperators.AllowUserOverride"] = true
```

Set it to `false` without `AllowUserOverride` to keep everybody from switching it on. Either way,
users find the setting in the app settings, together with an explanation of what is sent; when you
lock it, they see your value there. A change applies to the requests which follow; an embedding
run which is already underway finishes as it started.

### Before you switch it on

As long as your server cannot tell the users apart, the numbers say how often each feature is
used, and nothing about anybody in particular. This changes as soon as it can: for example, when
every user has an API key of their own, such as a virtual key in LiteLLM, or when your logs keep
the IP addresses of the devices. The usage data then relates to persons, and data protection law
applies to it. In some countries, such as Germany, evaluating it may also require the consent of
your works council, because it could be used to monitor the behavior or performance of employees.

Clarify this before you switch it on, evaluate the numbers per feature rather than per person, and
tell your colleagues that their requests name the features they use.