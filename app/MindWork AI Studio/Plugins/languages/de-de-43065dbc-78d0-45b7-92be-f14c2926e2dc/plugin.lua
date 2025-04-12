require("contentHome")
require("icon")

-- The ID for this plugin:
ID = "43065dbc-78d0-45b7-92be-f14c2926e2dc"

-- The icon for the plugin:
ICON_SVG = SVG

-- The name of the plugin:
NAME = "MindWork AI Studio - German / Deutsch"

-- The description of the plugin:
DESCRIPTION = "Dieses Plugin bietet deutsche Sprachunterstützung für MindWork AI Studio."

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
IETF_TAG = "de-DE"

-- The language name in the user's language:
LANG_NAME = "Deutsch (Deutschland)"

UI_TEXT_CONTENT = {
    HOME = CONTENT_HOME,
    AISTUDIO = {
        PAGES = {
            HOME = {
                T2331588413 = "Lass uns anfangen",
            },

            CHAT = {
                T3718856736 = "Vorläufiger Chat",
            }
        },
    }
}
