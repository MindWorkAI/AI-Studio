require("icon")

-- ------
-- This is an example of a configuration plugin. Please replace
-- the placeholders and assign a valid ID.
-- ------

-- The ID for this plugin:
ID = "00000000-0000-0000-0000-000000000000"

-- The icon for the plugin:
ICON_SVG = SVG

-- The name of the plugin:
NAME = "<Company Name> - Configuration for <Department Name>"

-- The description of the plugin:
DESCRIPTION = "This is a pre-defined configuration of <Company Name>"

-- The version of the plugin:
VERSION = "1.0.0"

-- The type of the plugin:
TYPE = "CONFIGURATION"

-- The authors of the plugin:
AUTHORS = {"<Company Name>"}

-- The support contact for the plugin:
SUPPORT_CONTACT = "<IT Department of Company Name>"

-- The source URL for the plugin:
SOURCE_URL = "<Any internal Git repository>"

-- The categories for the plugin:
CATEGORIES = { "CORE" }

-- The target groups for the plugin:
TARGET_GROUPS = { "EVERYONE" }

-- The flag for whether the plugin is maintained:
IS_MAINTAINED = true

-- When the plugin is deprecated, this message will be shown to users:
DEPRECATION_MESSAGE = ""

CONFIG = {}
CONFIG["LLM_PROVIDERS"] = {}

-- An example of a configuration for a self-hosted ollama server:
CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
    ["Id"] = "00000000-0000-0000-0000-000000000000",
    ["InstanceName"] = "<user-friendly name for the combination of server and model>",
    ["UsedLLMProvider"] = "SELF_HOSTED",
    ["Host"] = "OLLAMA",
    ["Hostname"] = "<https address of the ollama server>",
    ["Model"] = {
        ["Id"] = "<the ollama model ID>",
        ["DisplayName"] = "<user-friendly name of the model>",
    }
}

CONFIG["SETTINGS"] = {}

-- Configure the update behavior:
-- Allowed values are: NO_CHECK, ONCE_STARTUP, HOURLY, DAILY, WEEKLY
-- CONFIG["SETTINGS"]["DataApp.UpdateBehavior"] = "NO_CHECK"

-- Configure the user permission to add providers:
-- Allowed values are: true, false
-- CONFIG["SETTINGS"]["DataApp.DontAllowUserToAddProvider"] = false