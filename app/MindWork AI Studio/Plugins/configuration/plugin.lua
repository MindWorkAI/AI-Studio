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

-- An example of a configuration for a self-hosted server:
CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
    ["Id"] = "00000000-0000-0000-0000-000000000000",
    ["InstanceName"] = "<user-friendly name for the combination of server and model>",
    ["UsedLLMProvider"] = "SELF_HOSTED",
    
    -- Allowed values for Host are: LM_STUDIO, LLAMACPP, OLLAMA, and VLLM
    ["Host"] = "OLLAMA",
    ["Hostname"] = "<https address of the server>",
    
    -- Optional: Additional parameters for the API.
    -- Please refer to the documentation of the selected host for details.
    -- Might be something like ... \"temperature\": 0.5 ... for one parameter.
    -- Could be something like ... \"temperature\": 0.5, \"max_tokens\": 1000 ... for multiple parameters.
    -- Please do not add the enclosing curly braces {} here. Also, no trailing comma is allowed.
    ["AdditionalJsonApiParameters"] = "",
    ["Model"] = {
        ["Id"] = "<the model ID>",
        ["DisplayName"] = "<user-friendly name of the model>",
    }
}

CONFIG["SETTINGS"] = {}

-- Configure the update check interval:
-- Allowed values are: NO_CHECK, ONCE_STARTUP, HOURLY, DAILY, WEEKLY
-- CONFIG["SETTINGS"]["DataApp.UpdateInterval"] = "NO_CHECK"

-- Configure how updates are installed:
-- Allowed values are: MANUAL, AUTOMATIC
-- CONFIG["SETTINGS"]["DataApp.UpdateInstallation"] = "MANUAL"

-- Configure the user permission to add providers:
-- Allowed values are: true, false
-- CONFIG["SETTINGS"]["DataApp.AllowUserToAddProvider"] = false

-- Configure the visibility of preview features:
-- Allowed values are: NONE, RELEASE_CANDIDATE, BETA, ALPHA, PROTOTYPE, EXPERIMENTAL
-- Please note:
--      I: that this setting does not hide features that are already enabled.
--     II: lower levels include all features of the higher levels. E.g. BETA includes RELEASE_CANDIDATE features.
-- CONFIG["SETTINGS"]["DataApp.PreviewVisibility"] = "NONE"

-- Configure the enabled preview features:
-- Allowed values are can be found in https://github.com/MindWorkAI/AI-Studio/app/MindWork%20AI%20Studio/Settings/DataModel/PreviewFeatures.cs
-- Examples are PRE_WRITER_MODE_2024, PRE_RAG_2024, PRE_DOCUMENT_ANALYSIS_2025.
-- CONFIG["SETTINGS"]["DataApp.EnabledPreviewFeatures"] = { "PRE_RAG_2024", "PRE_DOCUMENT_ANALYSIS_2025" }

-- Example chat templates for this configuration:
CONFIG["CHAT_TEMPLATES"] = {}

-- A simple example chat template:
CONFIG["CHAT_TEMPLATES"][#CONFIG["CHAT_TEMPLATES"]+1] = {
    ["Id"] = "00000000-0000-0000-0000-000000000000",
    ["Name"] = "<user-friendly name of the chat template>",
    ["SystemPrompt"] = "You are <Company Name>'s helpful AI assistant for <Department Name>. Your task is ...",
    ["PredefinedUserPrompt"] = "Please help me with ...",
    ["AllowProfileUsage"] = true,
    ["ExampleConversation"] = {
        {
            -- Allowed values are: USER, AI, SYSTEM
            ["Role"] = "USER",
            ["Content"] = "Hello! Can you help me with a quick task?"
        },
        {
            -- Allowed values are: USER, AI, SYSTEM
            ["Role"] = "AI",
            ["Content"] = "Of course. What do you need?"
        }
    }
}
