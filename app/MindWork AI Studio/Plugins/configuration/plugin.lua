require("icon")

-- ------
-- This is an example of a configuration plugin. Please replace
-- the placeholders and assign a valid ID.
-- All IDs should be lower-case.
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
-- CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["InstanceName"] = "<user-friendly name for the combination of server and model>",
--     ["UsedLLMProvider"] = "SELF_HOSTED",
-- 
--     -- Allowed values for Host are: LM_STUDIO, LLAMACPP, OLLAMA, and VLLM
--     ["Host"] = "OLLAMA",
--     ["Hostname"] = "<https address of the server>",
-- 
--     -- Optional: Additional parameters for the API.
--     -- Please refer to the documentation of the selected host for details.
--     -- Might be something like ... \"temperature\": 0.5 ... for one parameter.
--     -- Could be something like ... \"temperature\": 0.5, \"max_tokens\": 1000 ... for multiple parameters.
--     -- Please do not add the enclosing curly braces {} here. Also, no trailing comma is allowed.
--     ["AdditionalJsonApiParameters"] = "",
--     ["Model"] = {
--         ["Id"] = "<the model ID>",
--         ["DisplayName"] = "<user-friendly name of the model>",
--     }
-- }

-- Transcription providers for voice-to-text functionality:
CONFIG["TRANSCRIPTION_PROVIDERS"] = {}

-- An example of a transcription provider configuration:
-- CONFIG["TRANSCRIPTION_PROVIDERS"][#CONFIG["TRANSCRIPTION_PROVIDERS"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly name for the transcription provider>",
--     ["UsedLLMProvider"] = "SELF_HOSTED",
--
--     -- Allowed values for Host are: LM_STUDIO, LLAMACPP, OLLAMA, VLLM, and WHISPER_CPP
--     ["Host"] = "WHISPER_CPP",
--     ["Hostname"] = "<https address of the server>",
--     ["Model"] = {
--         ["Id"] = "<the model ID>",
--         ["DisplayName"] = "<user-friendly name of the model>",
--     }
-- }

-- Embedding providers for local RAG (Retrieval-Augmented Generation) functionality:
CONFIG["EMBEDDING_PROVIDERS"] = {}

-- An example of an embedding provider configuration:
-- CONFIG["EMBEDDING_PROVIDERS"][#CONFIG["EMBEDDING_PROVIDERS"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly name for the embedding provider>",
--     ["UsedLLMProvider"] = "SELF_HOSTED",
--
--     -- Allowed values for Host are: LM_STUDIO, LLAMACPP, OLLAMA, and VLLM
--     ["Host"] = "OLLAMA",
--     ["Hostname"] = "<https address of the server>",
--     ["Model"] = {
--         ["Id"] = "<the model ID, e.g., nomic-embed-text>",
--         ["DisplayName"] = "<user-friendly name of the model>",
--     }
-- }

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

-- Configure the preselected profile.
-- It must be one of the profile IDs defined in CONFIG["PROFILES"].
-- Please note: using an empty string ("") will lock the preselected profile selection, even though no valid preselected profile is found.
-- CONFIG["SETTINGS"]["DataApp.PreselectedProfile"] = "00000000-0000-0000-0000-000000000000"

-- Configure the transcription provider for voice-to-text functionality.
-- It must be one of the transcription provider IDs defined in CONFIG["TRANSCRIPTION_PROVIDERS"].
-- Without a selected transcription provider, dictation and transcription features will be disabled.
-- Please note: using an empty string ("") will lock the selection and disable dictation/transcription.
-- CONFIG["SETTINGS"]["DataApp.UseTranscriptionProvider"] = "00000000-0000-0000-0000-000000000000"

-- Configure which assistants should be hidden from the UI.
-- Allowed values are:
--   GRAMMAR_SPELLING_ASSISTANT, ICON_FINDER_ASSISTANT, REWRITE_ASSISTANT,
--   TRANSLATION_ASSISTANT, AGENDA_ASSISTANT, CODING_ASSISTANT,
--   TEXT_SUMMARIZER_ASSISTANT, EMAIL_ASSISTANT, LEGAL_CHECK_ASSISTANT,
--   SYNONYMS_ASSISTANT, MY_TASKS_ASSISTANT, JOB_POSTING_ASSISTANT,
--   BIAS_DAY_ASSISTANT, ERI_ASSISTANT, DOCUMENT_ANALYSIS_ASSISTANT,
--   I18N_ASSISTANT
-- CONFIG["SETTINGS"]["DataApp.HiddenAssistants"] = { "ERI_ASSISTANT", "I18N_ASSISTANT" }

-- Configure a global shortcut for starting and stopping dictation.
-- 
-- The format follows the Rust and Tauri conventions. Especially,
-- when you want to use the CTRL key on Windows (or the CMD key on macOS),
-- please use "CmdOrControl" as the key name. All parts of the shortcut
-- must be separated by a plus sign (+).
--
-- Examples are: "CmdOrControl+Shift+D", "Alt+F9", "F8"
-- CONFIG["SETTINGS"]["DataApp.ShortcutVoiceRecording"] = "CmdOrControl+1"

-- Example chat templates for this configuration:
CONFIG["CHAT_TEMPLATES"] = {}

-- A simple example chat template:
-- CONFIG["CHAT_TEMPLATES"][#CONFIG["CHAT_TEMPLATES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly name of the chat template>",
--     ["SystemPrompt"] = "You are <Company Name>'s helpful AI assistant for <Department Name>. Your task is ...",
--     ["PredefinedUserPrompt"] = "Please help me with ...",
--     ["AllowProfileUsage"] = true,
--     ["ExampleConversation"] = {
--         {
--             -- Allowed values are: USER, AI, SYSTEM
--             ["Role"] = "USER",
--             ["Content"] = "Hello! Can you help me with a quick task?"
--         },
--         {
--             -- Allowed values are: USER, AI, SYSTEM
--             ["Role"] = "AI",
--             ["Content"] = "Of course. What do you need?"
--         }
--     }
-- }

-- An example chat template with file attachments:
-- This template automatically attaches specified files when the user selects it.
-- CONFIG["CHAT_TEMPLATES"][#CONFIG["CHAT_TEMPLATES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000001",
--     ["Name"] = "Document Analysis Template",
--     ["SystemPrompt"] = "You are an expert document analyst. Please analyze the attached documents and provide insights.",
--     ["PredefinedUserPrompt"] = "Please analyze the attached company guidelines and summarize the key points.",
--     ["AllowProfileUsage"] = true,
--     -- Optional: Pre-attach files that will be automatically included when using this template.
--     -- These files will be loaded when the user selects this chat template.
--     -- Note: File paths must be absolute paths and accessible to all users.
--     ["FileAttachments"] = {
--         "G:\\Company\\Documents\\Guidelines.pdf",
--         "G:\\Company\\Documents\\CompanyPolicies.docx"
--     },
--     ["ExampleConversation"] = {
--         {
--             ["Role"] = "USER",
--             ["Content"] = "I have attached the company documents for analysis."
--         },
--         {
--             ["Role"] = "AI",
--             ["Content"] = "Thank you. I'll analyze the documents and provide a comprehensive summary."
--         }
--     }
-- }

-- Document analysis policies for this configuration:
CONFIG["DOCUMENT_ANALYSIS_POLICIES"] = {}

-- An example document analysis policy:
-- CONFIG["DOCUMENT_ANALYSIS_POLICIES"][#CONFIG["DOCUMENT_ANALYSIS_POLICIES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["PolicyName"] = "Compliance Summary Policy",
--     ["PolicyDescription"] = "Summarizes compliance-relevant clauses, obligations, and deadlines found in provided documents.",
--     
--     ["AnalysisRules"] = [===[
--                             Focus on compliance obligations, deadlines, and required actions.
--                             Ignore marketing content and high-level summaries.
--                             Flag any ambiguous or missing information.
--                             ]===],
--     
--     ["OutputRules"] = [===[
--                         Provide a Markdown report with headings for Obligations, Deadlines,
--                         and Open Questions.
--                         ]===],
-- 
--     -- Optional: minimum provider confidence required for this policy.
--     -- Allowed values are: NONE, VERY_LOW, LOW, MODERATE, MEDIUM, HIGH
--     ["MinimumProviderConfidence"] = "MEDIUM",
-- 
--     -- Optional: preselect a provider or profile by ID.
--     -- The IDs must exist in CONFIG["LLM_PROVIDERS"] or CONFIG["PROFILES"].
--     ["PreselectedProvider"] = "00000000-0000-0000-0000-000000000000",
--     ["PreselectedProfile"] = "00000000-0000-0000-0000-000000000000"
-- }

-- Profiles for this configuration:
CONFIG["PROFILES"] = {}

-- A simple profile template:
-- CONFIG["PROFILES"][#CONFIG["PROFILES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly name of the profile>",
--     ["NeedToKnow"] = "I like to cook in my free time. My favorite meal is ...",
--     ["Actions"] = "Please always ensure the portion size is ..."
-- }