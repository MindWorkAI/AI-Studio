require("icon")

-- ------
-- This is an example of an assistant plugin that will build an assistant for you.
-- Please replace the placeholders and assign a valid ID.
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
TYPE = "ASSISTANT"

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

ASSISTANT = {
    ["Title"] = "<Title of your assistant>",
    ["Description"] = "<Description presented to the users, explaining your assistant>",
    ["UI"] = {
        ["Type"] = "FORM",
        ["Children"] = {}
    },
}

-- An example of a assistant that resembles AI Studios translation assistant:
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