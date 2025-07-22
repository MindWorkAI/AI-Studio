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
CATEGORIES = { "ASSISTANT" }

-- The target groups for the plugin:
TARGET_GROUPS = { "EVERYONE" }

-- The flag for whether the plugin is maintained:
IS_MAINTAINED = true

-- When the plugin is deprecated, this message will be shown to users:
DEPRECATION_MESSAGE = ""

ASSISTANT = {
    ["Title"] = "Grammatik- und Rechtschreibprüfung",
    ["Description"] = "Grammatik und Rechtschreibung eines Textes überprüfen.",
    ["UI"] = {
        ["Type"] = "FORM",
        ["Children"] = {
            {
                ["Type"] = "TEXT_AREA",
                ["Props"] = {
                    ["Name"] = "input",
                    ["Label"] = "Ihre Eingabe zur Überprüfung"
                }
            },
            {
                ["Type"] = "DROPDOWN",
                ["ValueType"] = "string",
                ["Default"] = { ["Value"] = "", ["Display"] = "Sprache nicht angeben." },
                ["Items"] = {
                    { ["Value"] = "de-DE", ["Display"] = "Deutsch" },
                    { ["Value"] = "en-UK", ["Display"] = "Englisch (UK)" },
                    { ["Value"] = "en-US", ["Display"] = "Englisch (US)" },
                },
                ["Props"] = {
                    ["Name"] = "language",
                    ["Label"] = "Sprache",
                }
            },
            {
                ["Type"] = "PROVIDER_SELECTION",
                ["Props"] = {
                    ["Name"] = "Anbieter",
                    ["Label"] = "LLM auswählen"
                }
            },
            {
                ["Type"] = "BUTTON",
                ["Props"] = {
                    ["Name"] = "submit",
                    ["Text"] = "Korrekturlesen",
                    ["Action"] = "OnSubmit"
                }
            },
        }
    },
}