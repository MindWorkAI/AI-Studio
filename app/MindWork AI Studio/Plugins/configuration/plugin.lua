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

-- True when this plugin is deployed by an enterprise configuration server:
DEPLOYED_USING_CONFIG_SERVER = false

-- The authors of the plugin:
AUTHORS = {"<Company Name>"}

-- The support contact for the plugin:
SUPPORT_CONTACT = "<IT Department of Company Name>"

-- The source URL for the plugin. Can be a HTTP(S) URL or an mailto link.
-- You may link to an internal documentation page, a Git repository, or
-- to a support or wiki page.
--
-- A mailto link could look like:
-- SOURCE_URL = "mailto:helpdesk@company.org?subject=Help"
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
--     -- Recognized reasoning parameters, such as reasoning_effort, thinking, think, and chat_template_kwargs.enable_thinking, may affect whether AI Studio shows the reasoning icon for this provider.
--     -- Please do not add the enclosing curly braces {} here. Also, no trailing comma is allowed.
--     ["AdditionalJsonApiParameters"] = "",
--
--     -- Optional: expert capability overrides.
--     -- Allowed keys are exactly:
--     -- AUDIO_INPUT, MULTIPLE_IMAGE_INPUT, SPEECH_INPUT, VIDEO_INPUT,
--     -- OPTIONAL_REASONING, ALWAYS_REASONING, REASONING_BY_DEFAULT
--     -- Allowed values are booleans only.
--     -- For default-on reasoning (rhinking), set OPTIONAL_REASONING and REASONING_BY_DEFAULT to true.
--     -- ALWAYS_REASONING means the model cannot disable reasoning (thinking).
--     -- Missing keys keep the automatic capability detection result.
--     -- ["CapabilityOverrides"] = {
--     --     ["VIDEO_INPUT"] = false,
--     -- },
--
--     -- Optional: Hugging Face inference provider. Only relevant for UsedLLMProvider = HUGGINGFACE.
--     -- Allowed values are: CEREBRAS, NEBIUS_AI_STUDIO, SAMBANOVA, NOVITA, HYPERBOLIC, TOGETHER_AI, FIREWORKS, HF_INFERENCE_API
--     -- ["HFInferenceProvider"] = "NOVITA",
--
--     -- Optional: Encrypted API key for cloud providers or secured on-premise models.
--     -- The API key must be encrypted using the enterprise encryption secret.
--     -- Format: "ENC:v1:<base64-encoded encrypted data>"
--     -- The encryption secret must be configured via:
--     --   Windows Registry: HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT\config_encryption_secret
--     --   Environment variable: MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET
--     -- You can export an encrypted API key from an existing provider using the export button in the settings.
--     -- ["APIKey"] = "ENC:v1:<base64-encoded encrypted data>",
--
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
--
--     -- Optional: Encrypted API key (see LLM_PROVIDERS example for details)
--     -- ["APIKey"] = "ENC:v1:<base64-encoded encrypted data>",
--
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
--
--     -- Optional: Encrypted API key (see LLM_PROVIDERS example for details)
--     -- ["APIKey"] = "ENC:v1:<base64-encoded encrypted data>",
--
--     ["Model"] = {
--         ["Id"] = "<the model ID, e.g., nomic-embed-text>",
--         ["DisplayName"] = "<user-friendly name of the model>",
--     }
-- }

-- ERI v1 data sources for retrieval-augmented generation:
CONFIG["DATA_SOURCES"] = {}

-- Example: ERI v1 data source with a shared access token.
-- CONFIG["DATA_SOURCES"][#CONFIG["DATA_SOURCES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly data source name>",
--     ["Type"] = "ERI_V1",
--     ["Hostname"] = "<https address of the ERI server>",
--     ["Port"] = 443,
--     ["AuthMethod"] = "TOKEN",
--     ["Token"] = "ENC:v1:<base64-encoded encrypted token>",
--     ["SecurityPolicy"] = "SELF_HOSTED",
--     ["SelectedRetrievalId"] = "<retrieval process ID from the ERI server>",
--     ["MaxMatches"] = 10,
-- }

-- Example: ERI v1 data source with a shared username and password.
-- CONFIG["DATA_SOURCES"][#CONFIG["DATA_SOURCES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly data source name>",
--     ["Type"] = "ERI_V1",
--     ["Hostname"] = "<https address of the ERI server>",
--     ["Port"] = 443,
--     ["AuthMethod"] = "USERNAME_PASSWORD",
--     ["UsernamePasswordMode"] = "SHARED_USERNAME_AND_PASSWORD",
--     ["Username"] = "<shared username>",
--     ["Password"] = "ENC:v1:<base64-encoded encrypted password>",
--     ["SecurityPolicy"] = "SELF_HOSTED",
--     ["SelectedRetrievalId"] = "<retrieval process ID from the ERI server>",
--     ["MaxMatches"] = 10,
-- }

-- Example: ERI v1 data source using the user's username and a shared password.
-- CONFIG["DATA_SOURCES"][#CONFIG["DATA_SOURCES"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Name"] = "<user-friendly data source name>",
--     ["Type"] = "ERI_V1",
--     ["Hostname"] = "<https address of the ERI server>",
--     ["Port"] = 443,
--     ["AuthMethod"] = "USERNAME_PASSWORD",
--     ["UsernamePasswordMode"] = "OS_USERNAME_SHARED_PASSWORD",
--     ["Password"] = "ENC:v1:<base64-encoded encrypted password>",
--     ["SecurityPolicy"] = "SELF_HOSTED",
--     ["SelectedRetrievalId"] = "<retrieval process ID from the ERI server>",
--     ["MaxMatches"] = 10,
-- }

CONFIG["SETTINGS"] = {}

-- Configure the update check interval:
-- Allowed values are: NO_CHECK, DISABLE_UPDATES, ONCE_STARTUP, HOURLY, DAILY, WEEKLY
-- NO_CHECK disables automatic checks, but users can still check and install updates manually.
-- DISABLE_UPDATES is intended for enterprise configurations and disables all update checks
-- and installations. It is not offered as a selectable option in the normal app settings.
-- CONFIG["SETTINGS"]["DataApp.UpdateInterval"] = "NO_CHECK"

-- Configure how updates are installed:
-- Allowed values are: MANUAL, AUTOMATIC
-- CONFIG["SETTINGS"]["DataApp.UpdateInstallation"] = "MANUAL"

-- Configure the page that should be opened when AI Studio starts.
-- Allowed values are: HOME, CHAT, ASSISTANTS, INFORMATION, PLUGINS, SUPPORTERS, SETTINGS
-- CONFIG["SETTINGS"]["DataApp.StartPage"] = "CHAT"
--
-- Allow users to change the configured start page locally.
-- Allowed values are: true, false
-- When set to true, the configured start page becomes the organization default,
-- but users can still choose another start page in the app settings.
-- CONFIG["SETTINGS"]["DataApp.StartPage.AllowUserOverride"] = true

-- Configure whether the quick start guide is shown on the welcome page.
-- CONFIG["SETTINGS"]["DataApp.ShowQuickStartGuide"] = false

-- Configure whether the built-in introduction is shown on the welcome page.
-- CONFIG["SETTINGS"]["DataApp.ShowIntroduction"] = false

-- Configure whether the last changelog is shown on the welcome page.
-- CONFIG["SETTINGS"]["DataApp.ShowLastChangelog"] = false

-- Configure whether the vision panel is shown on the welcome page.
-- CONFIG["SETTINGS"]["DataApp.ShowVision"] = false

-- Configure the user permission to add providers:
-- CONFIG["SETTINGS"]["DataApp.AllowUserToAddProvider"] = false

-- Configure whether administration settings are visible in the UI:
-- CONFIG["SETTINGS"]["DataApp.ShowAdminSettings"] = true

-- Configure the visibility of preview features:
-- Allowed values are: NONE, RELEASE_CANDIDATE, BETA, ALPHA, PROTOTYPE, EXPERIMENTAL
-- Please note:
--      I: that this setting does not hide features that are already enabled.
--     II: lower levels include all features of the higher levels. E.g. BETA includes RELEASE_CANDIDATE features.
-- CONFIG["SETTINGS"]["DataApp.PreviewVisibility"] = "NONE"

-- Configure the enabled preview features:
-- Allowed values are can be found in https://github.com/MindWorkAI/AI-Studio/blob/main/app/MindWork%20AI%20Studio/Settings/DataModel/PreviewFeatures.cs
-- Examples are PRE_WRITER_MODE_2024 and PRE_RAG_2024.
-- CONFIG["SETTINGS"]["DataApp.EnabledPreviewFeatures"] = { "PRE_RAG_2024" }

-- Configure the preselected provider.
-- It must be one of the provider IDs defined in CONFIG["LLM_PROVIDERS"].
-- Please note: using an empty string ("") will lock the preselected provider selection, even though no valid preselected provider is found.
-- CONFIG["SETTINGS"]["DataApp.PreselectedProvider"] = "00000000-0000-0000-0000-000000000000"

-- Configure the preselected profile.
-- It must be one of the profile IDs defined in CONFIG["PROFILES"].
-- Please note: using an empty string ("") will lock the preselected profile selection, even though no valid preselected profile is found.
-- CONFIG["SETTINGS"]["DataApp.PreselectedProfile"] = "00000000-0000-0000-0000-000000000000"

-- Configure chat-specific preselected options.
-- This must be enabled for the chat-specific provider, profile, and chat template to take effect.
-- CONFIG["SETTINGS"]["DataChat.PreselectOptions"] = true
--
-- Configure the preselected provider for chats.
-- It must be one of the provider IDs defined in CONFIG["LLM_PROVIDERS"].
-- CONFIG["SETTINGS"]["DataChat.PreselectedProvider"] = "00000000-0000-0000-0000-000000000000"
--
-- Configure the preselected profile for chats.
-- It must be one of the profile IDs defined in CONFIG["PROFILES"].
-- Please note: using an empty string ("") means chats will use the app default profile.
-- Please note: using "00000000-0000-0000-0000-000000000000" means chats will use no profile.
-- CONFIG["SETTINGS"]["DataChat.PreselectedProfile"] = "00000000-0000-0000-0000-000000000000"
--
-- Configure the preselected chat template for chats.
-- It must be one of the chat template IDs defined in CONFIG["CHAT_TEMPLATES"].
-- Please note: using an empty string ("") or "00000000-0000-0000-0000-000000000000" means chats will use no chat template.
-- CONFIG["SETTINGS"]["DataChat.PreselectedChatTemplate"] = "00000000-0000-0000-0000-000000000000"
--
--
-- Configure default data source options for new chats.
--
-- Controls whether data sources are off by default:
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourcesDisabled"] = false

-- Controls whether AI Studio asks an agent to choose data sources:
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourcesAutomaticSelection"] = true

-- Controls whether retrieved data is validated by an agent:
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourcesAutomaticValidation"] = true

-- Must contain IDs from CONFIG["DATA_SOURCES"] or user-configured data sources.
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourceIds"] = {
--     "00000000-0000-0000-0000-000000000000",
-- }
--
-- Configure whether default chat data source options are applied when assistant results are sent to chat.
-- Allowed values are: NO_DATA_SOURCES, APPLY_STANDARD_CHAT_DATA_SOURCE_OPTIONS
-- CONFIG["SETTINGS"]["DataChat.SendToChatDataSourceBehavior"] = "APPLY_STANDARD_CHAT_DATA_SOURCE_OPTIONS"
--
-- Allow users to change any configured chat default locally.
-- Allowed values are: true, false
-- CONFIG["SETTINGS"]["DataChat.PreselectOptions.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedProvider.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedProfile.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedChatTemplate.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourcesDisabled.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourcesAutomaticSelection.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourcesAutomaticValidation.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.PreselectedDataSourceIds.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataChat.SendToChatDataSourceBehavior.AllowUserOverride"] = true

-- Configure the transcription provider for voice-to-text functionality.
-- It must be one of the transcription provider IDs defined in CONFIG["TRANSCRIPTION_PROVIDERS"].
-- Without a selected transcription provider, dictation and transcription features will be disabled.
-- Please note: using an empty string ("") will lock the selection and disable dictation/transcription.
-- CONFIG["SETTINGS"]["DataApp.UseTranscriptionProvider"] = "00000000-0000-0000-0000-000000000000"

-- Configure which assistants should be hidden from the UI.
-- Allowed values are:
--   GRAMMAR_SPELLING_ASSISTANT, ICON_FINDER_ASSISTANT, REWRITE_ASSISTANT,
--   PROMPT_OPTIMIZER_ASSISTANT, TRANSLATION_ASSISTANT, AGENDA_ASSISTANT,
--   CODING_ASSISTANT, TEXT_SUMMARIZER_ASSISTANT, EMAIL_ASSISTANT,
--   LEGAL_CHECK_ASSISTANT, SYNONYMS_ASSISTANT, MY_TASKS_ASSISTANT,
--   JOB_POSTING_ASSISTANT, BIAS_DAY_ASSISTANT, ERI_ASSISTANT,
--   DOCUMENT_ANALYSIS_ASSISTANT, SLIDE_BUILDER_ASSISTANT, I18N_ASSISTANT,
--   LOG_VIEWER_ASSISTANT
-- CONFIG["SETTINGS"]["DataApp.HiddenAssistants"] = { "ERI_ASSISTANT", "I18N_ASSISTANT" }

-- Configure enterprise approvals for assistant plugins.
-- Each approval is matched only by the current SHA-256 hash over all Lua files
-- in the assistant plugin folder, in canonical sorted order.
-- When the hash matches, the assistant plugin is treated as SAFE immediately and
-- no user-run security audit is required.
-- You can generate the exact hash with the build-script command:
--   dotnet run --project app/Build -- assistant-plugin-hash "<plugin-dir>" --lua-snippet
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.EnterpriseApprovedPlugins"] = {
--     {
--         ["PluginHash"] = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
--         ["DisplayName"] = "Name of Plugin",
--         ["Comment"] = "Optional comment",
--         ["ApprovedBy"] = "Optional Approver",
--         ["ApprovedAtUtc"] = "2026-07-02T09:30:00Z",
--     }
-- }

-- Configure a global shortcut for starting and stopping dictation.
-- 
-- The format follows the Rust and Tauri conventions. Especially,
-- when you want to use the CTRL key on Windows (or the CMD key on macOS),
-- please use "CmdOrControl" as the key name. All parts of the shortcut
-- must be separated by a plus sign (+).
--
-- Examples are: "CmdOrControl+Shift+D", "Alt+F9", "F8"
-- CONFIG["SETTINGS"]["DataApp.ShortcutVoiceRecording"] = "CmdOrControl+1"

-- Configure the minimum provider confidence level required for individual tools.
-- Tool IDs include: web_search, read_web_page
-- Allowed values are: NONE, UNTRUSTED, VERY_LOW, LOW, MODERATE, MEDIUM, HIGH
-- Defaults: web_search = MEDIUM, read_web_page = MEDIUM, but higher confidence is recommended
-- CONFIG["SETTINGS"]["DataTools.MinimumProviderConfidenceByToolId"] = {
--     ["web_search"] = "MEDIUM",
--     ["read_web_page"] = "MEDIUM"
-- }

-- Configure the SearXNG instance URL used by the Web Search tool.
-- You can enter either the instance root URL or the /search endpoint.
-- CONFIG["SETTINGS"]["DataTools.WebSearchBaseUrl"] = "https://searxng.website/"
-- CONFIG["SETTINGS"]["DataTools.WebSearchBaseUrl.AllowUserOverride"] = false

-- Configure private or VPN hosts that the Read Web Page tool may access.
-- Public web pages do not need to be listed here.
-- Private hosts listed here still require a provider with HIGH confidence before any page content is sent to the model.
-- For hosts on this allowlist, AI Studio also tries the current user's operating-system sign-in
-- automatically when the server requests integrated authentication (for example Kerberos or NTLM).
-- This does not reuse Firefox cookies or an existing browser session.
-- Separate host patterns with commas. Wildcards only match subdomains, so add the root domain separately if needed.
-- Examples:
-- CONFIG["SETTINGS"]["DataTools.ReadWebPageAllowedPrivateHosts"] = "dlr.de, *.dlr.de"
-- CONFIG["SETTINGS"]["DataTools.ReadWebPageAllowedPrivateHosts.AllowUserOverride"] = false

-- Configure the HTTP timeout for external requests, in seconds.
-- The default is 3600 (1 hour).
-- CONFIG["SETTINGS"]["DataApp.HttpClientTimeoutSeconds"] = 3600

-- Configure additional root certificates for external HTTPS requests.
--
-- This is intended for managed Linux/Flatpak deployments where organization-internal
-- HTTPS certificates chain to a private root CA that is not visible inside the sandbox.
-- The file must be a PEM bundle with one or more root CA certificates and must be
-- readable by AI Studio.
--
-- IMPORTANT: A configuration plugin cannot fix the very first download of that same
-- configuration plugin. For bootstrapping enterprise configuration downloads, deploy
-- the equivalent environment variables before AI Studio starts:
--
-- MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATES_ENABLED=true
-- MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH=/path/in/sandbox/company-root-cas.pem
-- MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS=*.intra.example.org;data.example.org
--
-- CONFIG["SETTINGS"]["DataApp.ExternalHttpCustomRootCertificatesEnabled"] = true
-- CONFIG["SETTINGS"]["DataApp.ExternalHttpCustomRootCertificateBundlePath"] = "/path/in/sandbox/company-root-cas.pem"
-- CONFIG["SETTINGS"]["DataApp.ExternalHttpCustomRootCertificateAllowedHosts"] = { "*.intra.example.org", "eri.example.org" }

-- Configure provider confidence settings.
-- These settings apply to LLM providers, embedding providers, and transcription providers.
--
-- Configure a predefined confidence scheme.
-- Allowed values are: TRUST_ALL, TRUST_USA_EUROPE, TRUST_USA, TRUST_EUROPE, TRUST_ASIA, LOCAL_TRUST_ONLY, CUSTOM
-- CONFIG["SETTINGS"]["DataConfidence.ConfidenceScheme"] = "TRUST_EUROPE"
--
-- Configure whether users can still change the confidence scheme locally.
-- Allowed values are: true, false
-- When set to true, the configured confidence scheme becomes the organization default,
-- but users can still choose another scheme in the app settings.
-- CONFIG["SETTINGS"]["DataConfidence.ConfidenceScheme.AllowUserOverride"] = true
--
-- Configure whether confidence levels are shown in the UI.
-- CONFIG["SETTINGS"]["DataConfidence.ShowProviderConfidence"] = true
--
-- Configure an app-wide minimum confidence level.
-- Allowed values are: NONE, VERY_LOW, LOW, MODERATE, MEDIUM, HIGH
-- CONFIG["SETTINGS"]["DataConfidence.EnforceGlobalMinimumConfidence"] = true
-- CONFIG["SETTINGS"]["DataConfidence.GlobalMinimumConfidence"] = "MEDIUM"
--
-- Configure whether users can change the app-wide minimum confidence level locally.
-- CONFIG["SETTINGS"]["DataConfidence.EnforceGlobalMinimumConfidence.AllowUserOverride"] = false
-- CONFIG["SETTINGS"]["DataConfidence.GlobalMinimumConfidence.AllowUserOverride"] = false
--
-- Configure a custom confidence scheme.
-- This is used when DataConfidence.ConfidenceScheme is set to CUSTOM.
-- Allowed provider keys are: OPEN_AI, ANTHROPIC, MISTRAL, GOOGLE, X, DEEP_SEEK, ALIBABA_CLOUD,
--   PERPLEXITY, OPEN_ROUTER, FIREWORKS, GROQ, HUGGINGFACE, SELF_HOSTED, HELMHOLTZ, GWDG
-- Allowed confidence values are: UNTRUSTED, VERY_LOW, LOW, MODERATE, MEDIUM, HIGH
-- CONFIG["SETTINGS"]["DataConfidence.CustomConfidenceScheme"] = {
--     ["OPEN_AI"] = "MODERATE",
--     ["ANTHROPIC"] = "MODERATE",
--     ["MISTRAL"] = "HIGH",
--     ["GOOGLE"] = "LOW",
--     ["X"] = "LOW",
--     ["DEEP_SEEK"] = "LOW",
--     ["ALIBABA_CLOUD"] = "LOW",
--     ["PERPLEXITY"] = "MODERATE",
--     ["OPEN_ROUTER"] = "MODERATE",
--     ["FIREWORKS"] = "MODERATE",
--     ["GROQ"] = "MODERATE",
--     ["HUGGINGFACE"] = "MODERATE",
--     ["SELF_HOSTED"] = "HIGH",
--     ["HELMHOLTZ"] = "HIGH",
--     ["GWDG"] = "HIGH",
-- }
--
-- Configure whether users can change the custom confidence scheme locally.
-- CONFIG["SETTINGS"]["DataConfidence.CustomConfidenceScheme.AllowUserOverride"] = false
--
-- Configure provider instances trusted by your organization for data-source security checks.
-- These IDs may refer to LLM providers, embedding providers, or transcription providers
-- defined in this configuration. Trusted providers are treated like self-hosted providers
-- only for data-source security checks and related local data warnings.
-- CONFIG["SETTINGS"]["DataSourceSecuritySettings.TrustedProviderIds"] = {
--     "00000000-0000-0000-0000-000000000000",
--     "00000000-0000-0000-0000-000000000001",
-- }

-- Configure the data source selection agent.
-- This agent is used when chat data source options enable AI-based data source selection.
-- The provider must be one of the provider IDs defined in CONFIG["LLM_PROVIDERS"].
-- CONFIG["SETTINGS"]["DataAgentDataSourceSelection.PreselectAgentOptions"] = true
-- CONFIG["SETTINGS"]["DataAgentDataSourceSelection.PreselectedAgentProvider"] = "00000000-0000-0000-0000-000000000000"
-- CONFIG["SETTINGS"]["DataAgentDataSourceSelection.PreselectAgentOptions.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataAgentDataSourceSelection.PreselectedAgentProvider.AllowUserOverride"] = true

-- Configure the retrieval context validation agent.
-- This agent is used when retrieval context validation is enabled globally and chat data source options enable AI-based validation.
-- The provider must be one of the provider IDs defined in CONFIG["LLM_PROVIDERS"].
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.EnableRetrievalContextValidation"] = true
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.PreselectAgentOptions"] = true
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.PreselectedAgentProvider"] = "00000000-0000-0000-0000-000000000000"
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.NumParallelValidations"] = 3
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.EnableRetrievalContextValidation.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.PreselectAgentOptions.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.PreselectedAgentProvider.AllowUserOverride"] = true
-- CONFIG["SETTINGS"]["DataAgentRetrievalContextValidation.NumParallelValidations.AllowUserOverride"] = true

-- Configure assistant plugin security audits.
--
-- Configure whether assistant plugins must be audited before users can activate them.
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.RequireAuditBeforeActivation"] = true
--
-- Configure a dedicated provider for assistant plugin audits.
-- It must be one of the provider IDs defined in CONFIG["LLM_PROVIDERS"].
-- Without a selected audit provider, AI Studio uses the app-wide default provider.
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.PreselectedAgentProvider"] = "00000000-0000-0000-0000-000000000000"
--
-- Configure the minimum audit level assistant plugins must meet.
-- Allowed values are: UNKNOWN, DANGEROUS, CAUTION, SAFE
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.MinimumLevel"] = "CAUTION"
--
-- Configure whether activation is blocked when the audit result is below the minimum level.
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.BlockActivationBelowMinimum"] = true
--
-- Configure whether new or changed assistant plugins are audited automatically in the background.
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.AutomaticallyAuditAssistants"] = false
--
-- Configure whether users can change assistant plugin audit settings locally.
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.RequireAuditBeforeActivation.AllowUserOverride"] = false
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.PreselectedAgentProvider.AllowUserOverride"] = false
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.MinimumLevel.AllowUserOverride"] = false
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.BlockActivationBelowMinimum.AllowUserOverride"] = false
-- CONFIG["SETTINGS"]["DataAssistantPluginAudit.AutomaticallyAuditAssistants.AllowUserOverride"] = false

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
--     -- Note: File paths can be absolute paths that are accessible to all users, or relative paths
--     -- inside this plugin folder, for example "attachments/00000000-0000-0000-0000-000000000001/Guidelines.pdf".
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

-- Introduction texts shown as expansion panels on the welcome page:
CONFIG["INTRODUCTIONS"] = {}

-- An example introduction:
-- CONFIG["INTRODUCTIONS"][#CONFIG["INTRODUCTIONS"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Title"] = "Welcome to Your Organization's AI Studio",
--     ["Version"] = "1",
--     ["Index"] = 1,
--     ["Markdown"] = [===[
--                         ## Getting Started
--
--                         This AI Studio installation is managed by your organization.
--                         Please use the preconfigured providers and follow your internal
--                         AI usage guidelines.
--
--                         Further information is available in the [internal wiki](https://example.org/wiki).
--                         ]===]
-- }

-- Mandatory infos that users must explicitly accept before using AI Studio:
-- AI Studio asks users again when Version, Title, or Markdown change.
-- Changing Version additionally allows the UI to communicate that a new version is available.
CONFIG["MANDATORY_INFOS"] = {}

-- An example mandatory info:
-- CONFIG["MANDATORY_INFOS"][#CONFIG["MANDATORY_INFOS"]+1] = {
--     ["Id"] = "00000000-0000-0000-0000-000000000000",
--     ["Title"] = "AI Usage Requirements",
--     ["Version"] = "1",
--     ["Markdown"] = [===[
--                         ## Usage Requirements
--
--                         Before using this AI offering, please ensure that:
--
--                         - you have completed the required internal training,
--                         - generated output is clearly labeled where necessary,
--                         - results are reviewed by a human before reuse,
--                         - all internal policies and applicable law are followed.
--
--                         Further information is available in the [internal wiki](https://example.org/wiki).
--                         ]===],
--     ["AcceptButtonText"] = "Yes, I comply with these requirements",
--     ["RejectButtonText"] = "Stop. I do not agree to these requirements"
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
--     ["PreselectedProfile"] = "00000000-0000-0000-0000-000000000000",
--
--     -- Optional: hide the policy definition section in the UI.
--     -- When set to true, users will only see the document selection interface
--     -- and cannot view or modify the policy settings.
--     -- This is useful for enterprise configurations where policy details should remain hidden.
--     -- Allowed values are: true, false (default: false)
--     ["HidePolicyDefinition"] = false
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
