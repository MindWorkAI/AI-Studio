require("contentHome")
require("icon")

-- The ID for this plugin:
ID = "97dfb1ba-50c4-4440-8dfa-6575daf543c8"

-- The icon for the plugin:
ICON_SVG = SVG

-- The name of the plugin:
NAME = "MindWork AI Studio - US English"

-- The description of the plugin:
DESCRIPTION = "This plugin provides US English language support for MindWork AI Studio."

-- The version of the plugin:
VERSION = "1.0.0"

-- The type of the plugin:
TYPE = "LANGUAGE"

-- The authors of the plugin:
AUTHORS = {"MindWork AI Community"}

-- The support contact for the plugin:
SUPPORT_CONTACT = "MindWork AI Community"

-- The source URL for the plugin:
SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"

-- The categories for the plugin:
CATEGORIES = { "CORE" }

-- The target groups for the plugin:
TARGET_GROUPS = { "EVERYONE" }

-- The flag for whether the plugin is maintained:
IS_MAINTAINED = true

-- When the plugin is deprecated, this message will be shown to users:
DEPRECATION_MESSAGE = ""

-- The IETF BCP 47 tag for the language. It's the ISO 639 language
-- code followed by the ISO 3166-1 country code:
IETF_TAG = "en-US"

-- The language name in the user's language:
LANG_NAME = "English (United States)"

UI_TEXT_CONTENT = {
    HOME = CONTENT_HOME,
    AISTUDIO = {
        PAGES = {
            T2331588413 = "Let's get started",
        },
    }
}
