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
So that MindWork AI Studio knows where to load which configuration, this information must be provided as metadata on employees’ devices. Currently, the following options are available:

- **Registry** (only available for Microsoft Windows): On Windows devices, AI Studio first tries to read the information from the registry. The registry information can be managed and distributed centrally as a so-called Group Policy Object (GPO).

- **Environment variables**: On all operating systems (on Windows as a fallback after the registry), AI Studio tries to read the configuration metadata from environment variables.

The following keys and values (registry) and variables are checked and read:

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `config_id` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID`: This must be a valid [GUID](https://en.wikipedia.org/wiki/Universally_unique_identifier#Globally_unique_identifier). It uniquely identifies the configuration. You can use an ID per department, institute, or even per person.

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `delete_config_id` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_DELETE_CONFIG_ID`: This is a configuration ID that should be removed. This is helpful if an employee moves to a different department or leaves the organization.

- Key `HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT`, value `config_server_url` or variable `MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL`: An HTTP or HTTPS address using an IP address or DNS name. This is the web server from which AI Studio attempts to load the specified configuration as a ZIP file.

Let's assume as example that `https://intranet.my-company.com:30100/ai-studio/configuration` is the server address and `9072b77d-ca81-40da-be6a-861da525ef7b` is the configuration ID. AI Studio will derive the following address from this information: `https://intranet.my-company.com:30100/ai-studio/configuration/9072b77d-ca81-40da-be6a-861da525ef7b.zip`. Important: The configuration ID will always be written in lowercase, even if it is configured in uppercase. If `9072B77D-CA81-40DA-BE6A-861DA525EF7B` is configured, the same address will be derived. Your web server must be configured accordingly.

Finally, AI Studio will send a GET request and download the ZIP file. The ZIP file only contains the files necessary for the configuration. It's normal to include a file for an icon along with the actual configuration plugin.

Approximately every 16 minutes, AI Studio checks the metadata of the ZIP file by reading the [ETag](https://en.wikipedia.org/wiki/HTTP_ETag). When the ETag was not changed, no download will be performed. Make sure that your web server supports this.

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

## Example AI Studio configuration
The latest example of an AI Studio configuration via configuration plugin can always be found in the repository in the `app/MindWork AI Studio/Plugins/configuration` folder. Here are the links to the files:

- [The icon](../app/MindWork%20AI%20Studio/Plugins/configuration/icon.lua)
- [The configuration with explanations](../app/MindWork%20AI%20Studio/Plugins/configuration/plugin.lua)

Please note that the icon must be an SVG vector graphic. Raster graphics like PNGs, GIFs, and others aren’t supported. You can use the sample icon, which looks like a gear.

Currently, you can configure the following things:
- Any number of self-hosted LLM providers (a combination of server and model), but currently only without API keys
- The update behavior of AI Studio

All other settings can be made by the user themselves. If you need additional settings, feel free to create an issue in our planning repository: https://github.com/MindWorkAI/Planning/issues

In the coming months, we will allow more settings, such as:
- Using API keys for providers
- Configuration of embedding providers for RAG
- Configuration of data sources for RAG
- Configuration of chat templates
- Configuration of assistant plugins (for example, your own assistants for your company or specific departments)