-- ------
-- This is an example of a model plugin. Please replace
-- the placeholders and assign a valid ID.
-- All IDs should be lower-case.
-- ------

-- The ID for this plugin:
ID = "00000000-0000-0000-0000-000000000000"

-- The name of the plugin:
NAME = "<Company Name> - Models of <Department Name>"

-- The description of the plugin:
DESCRIPTION = "Describes the models <Company Name> runs itself"

-- The version of the plugin:
VERSION = "1.0.0"

-- The type of the plugin:
TYPE = "MODEL"

-- The priority of this model plugin. Optional, defaults to 0.
--
-- It only matters when two of your model plugins describe exactly the same
-- model names. The plugin with the higher priority wins then. Two plugins
-- describing different models never get in each other's way, and both are used.
--
-- The priority never lifts a locally placed model plugin above one of your
-- organization: what your IT department deployed always wins.
PRIORITY = 0

-- The authors of the plugin:
AUTHORS = {"<Company Name>"}

-- The support contact for the plugin:
SUPPORT_CONTACT = "<IT Department of Company Name>"

-- The source URL for the plugin. Can be a HTTP(S) URL or a mailto link:
SOURCE_URL = "<Any internal Git repository>"

-- The categories for the plugin:
CATEGORIES = { "CORE" }

-- The target groups for the plugin:
TARGET_GROUPS = { "EVERYONE" }

-- The flag for whether the plugin is maintained:
IS_MAINTAINED = true

-- When the plugin is deprecated, this message will be shown to users:
DEPRECATION_MESSAGE = ""

-- ------
-- What a model plugin is for
-- ------
--
-- AI Studio knows what the models of the large vendors can do. It cannot know
-- what your own models can do: a fine-tune of your own, a model behind an
-- internal name, or an engine you configured differently from its model card.
-- This is where you tell it.
--
-- A model plugin only describes. It names no server, carries no API key, and
-- runs no code. Which server a model is reached through stays where it was: in
-- the LLM providers of your configuration plugin.
--
-- Each entry below replaces what AI Studio would otherwise work out about the
-- model names it matches. It is the whole statement about them, which is why
-- CAPABILITIES is required: write each entry as if AI Studio knew nothing about
-- these models at all.
--
-- If you only want to correct one detail of a model AI Studio already knows --
-- one provider which accepts no images, say -- do not write an entry here. Use
-- the CapabilityOverrides of that LLM provider in your configuration plugin
-- instead. Your users can set the same thing in the expert settings of their
-- provider, and both win over everything below.

MODELS = {}

-- An example: a fine-tune an organization serves on its own vLLM.
-- MODELS[#MODELS+1] = {
--
--     -- Which model names this entry describes. Write it the way a model name
--     -- is written: lower case, hyphens between the parts. A pattern which is
--     -- written differently can never match anything and is rejected.
--     ["PATTERN"] = "acme-assistant",
--
--     -- How the pattern is bound to the name. Optional, defaults to SEGMENT.
--     --
--     --   EXACT     The pattern is the whole model name.
--     --   PREFIX    The name begins with the pattern, at a part boundary.
--     --             "acme-assistant" then also covers "acme-assistant-7b".
--     --   SEGMENT   The pattern appears in the name as whole parts. This is
--     --             the one to reach for.
--     --   SUBSTRING The pattern appears anywhere in the name, boundaries or
--     --             not. The last resort, for names a vendor glued together.
--     --
--     -- Note that a dot separates versions rather than name parts: a pattern
--     -- "acme-assistant-3" does not match "acme-assistant-3.1". Write the
--     -- version you mean.
--     ["MATCH"] = "PREFIX",
--
--     -- Optional: further name parts the name has to carry, and name parts
--     -- whose presence rules this entry out. This is how you describe two
--     -- variants which share a name.
--     -- ["ALSO_CONTAINS"] = { "vision" },
--     -- ["NOT_CONTAINS"] = { "base" },
--
--     -- Optional: restrict this entry to one LLM provider, for the case where
--     -- the same name means different things depending on who serves it.
--     -- Allowed values are: OPEN_AI, ANTHROPIC, MISTRAL, GOOGLE, X, DEEP_SEEK,
--     -- ALIBABA_CLOUD, PERPLEXITY, OPEN_ROUTER, HETZNER, IONOS, LITE_LLM,
--     -- FIREWORKS, GROQ, HUGGINGFACE, SELF_HOSTED, HELMHOLTZ, GWDG
--     ["ONLY_ON"] = "SELF_HOSTED",
--
--     -- Optional: restrict this entry to models of one vendor. Only gateways
--     -- which name the vendor alongside the model, such as OpenRouter, can
--     -- answer this at all.
--     -- ["ONLY_FROM"] = "META",
--
--     -- What these models can do. Required: this entry replaces everything
--     -- AI Studio would otherwise say about them.
--     -- Name one capability per entry. Allowed values are:
--     --   TEXT_INPUT, AUDIO_INPUT, SINGLE_IMAGE_INPUT, MULTIPLE_IMAGE_INPUT,
--     --   SPEECH_INPUT, VIDEO_INPUT, TEXT_OUTPUT, AUDIO_OUTPUT, IMAGE_OUTPUT,
--     --   SPEECH_OUTPUT, VIDEO_OUTPUT, EMBEDDING, REALTIME, FUNCTION_CALLING,
--     --   WEB_SEARCH, CHAT_COMPLETION_API, RESPONSES_API
--     -- Name at least the APIs the model answers through, otherwise AI Studio
--     -- does not know how to talk to it.
--     ["CAPABILITIES"] = {
--         "TEXT_INPUT",
--         "MULTIPLE_IMAGE_INPUT",
--         "TEXT_OUTPUT",
--         "FUNCTION_CALLING",
--         "CHAT_COMPLETION_API",
--     },
--
--     -- How the model reasons (thinks). Optional, defaults to NONE.
--     -- Allowed values are:
--     --   NONE           The model does not reason.
--     --   OPTIONAL       Reasoning can be switched on, and is off by default.
--     --   ON_BY_DEFAULT  Reasoning is on unless a parameter switches it off.
--     --   ALWAYS         Reasoning cannot be switched off.
--     -- Whether the indicator lights up also depends on the additional API
--     -- parameters of the configured provider.
--     ["REASONING"] = "OPTIONAL",
--
--     -- What the model is made for. Optional, defaults to CHAT.
--     -- Allowed values are: CHAT, TEXT_COMPLETION, EMBEDDING, RERANKING,
--     -- IMAGE_GENERATION, VIDEO_GENERATION, TRANSCRIPTION, SPEECH_SYNTHESIS,
--     -- REALTIME, COMPUTER_USE, OCR, MODERATION, OTHER
--     -- This decides which lists the model appears in. Use OTHER for entries
--     -- which are no models at all.
--     ["KIND"] = "CHAT",
--
--     -- Optional: how many tokens the model reads and writes in one
--     -- conversation, as it is served.
--     ["CONTEXT_WINDOW"] = 131072,
--
--     -- Optional: what an operator can raise that window to. Only state this
--     -- when you also state CONTEXT_WINDOW, and never below it.
--     -- ["CONTEXT_WINDOW_RAISABLE_TO"] = 262144,
--
--     -- Optional: which tokenizer counts this model's tokens. Both keys
--     -- belong together, because the kind says how the ID would be read.
--     -- Allowed kinds are: HUGGING_FACE, TIKTOKEN, PROVIDER_API, NONE
--     -- AI Studio records the reference; it does not fetch a tokenizer.
--     -- ["TOKENIZER_KIND"] = "HUGGING_FACE",
--     -- ["TOKENIZER_ID"] = "acme/assistant",
--
--     -- Optional: how many images the model accepts. Both numbers exist and
--     -- are not the same one, so state whichever your source names. Zero is a
--     -- real answer here; leaving a key out means nobody knows.
--     -- Note that vLLM accepts one image per prompt unless the operator
--     -- raised --limit-mm-per-prompt.
--     -- ["MAX_IMAGES_PER_MESSAGE"] = 1,
--     -- ["MAX_IMAGES_PER_REQUEST"] = 8,
--
--     -- Where all of this was read, and when somebody last looked. Required.
--     -- A model card changes without telling anybody, and a statement nobody
--     -- can check ages into a defect. Your entry will outlive whoever wrote
--     -- it, so name the page and the day: it is what lets the next
--     -- administrator find out in a minute whether it still holds.
--     ["SOURCE_URL"] = "https://intranet.company.org/ai/acme-assistant",
--     ["SOURCE_CHECKED_ON"] = "2026-09-12",
--     ["SOURCE_NOTE"] = "Internal model card: tools, images, 128k context",
-- }