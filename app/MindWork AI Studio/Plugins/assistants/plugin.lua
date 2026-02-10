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

-- usage example with the full feature set:
ASSISTANT = {
    ["Title"] = "<main title of assistant>", -- required
    ["Description"] = "<assitant description>", -- required
    ["SystemPrompt"] = "<prompt that fudamentally changes behaviour, personality and task focus of your assistant. Invisible to the user>", -- required
    ["SubmitText"] = "<label for submit button>", -- required
    ["AllowProfiles"] = true, -- if true, allows AiStudios profiles; required
    ["UI"] = {
        ["Type"] = "FORM",
        ["Children"] = {
            {
                ["Type"] = "TEXT_AREA", -- required
                ["Props"] = {
                    ["Name"] = "<unique identifier of this component>", -- required
                    ["Label"] = "<heading of your component>", -- required
                    ["UserPrompt"] = "<direct input of instructions, questions, or tasks by a user>",
                    ["PrefillText"] = "<text to show in the field initially>",
                    ["IsSingleLine"] = false, -- if true, shows a text field instead of an area
                    ["ReadOnly"] = false -- if true, deactivates user input (make sure to provide a PrefillText)
                }
            },
            {
                ["Type"] = "DROPDOWN", -- required
                ["Props"] = {
                    ["Name"] = "<unique identifier of this component>", -- required
                    ["Label"] = "<heading of your component>", -- required
                    ["UserPrompt"] = "<direct input of instructions, questions, or tasks by a user>",
                    ["ValueType"] = "<data type of item values>", -- required
                    ["Default"] = { ["Value"] = "<internal data>", ["Display"] = "<user readable representation>" }, -- required
                    ["Items"] = {
                        { ["Value"] = "<internal data>", ["Display"] = "<user readable representation>" },
                        { ["Value"] = "<internal data>", ["Display"] = "<user readable representation>" },
                    } -- required
                }
            },
            {
                ["Type"] = "SWITCH",
                ["Props"] = {
                    ["Name"] = "<unique identifier of this component>", -- required
                    ["Label"] = "<heading of your component>", -- required
                    ["Value"] = true, -- intial switch state
                    ["UserPrompt"] = "<direct input of instructions, questions, or tasks by a user>",
                    ["LabelOn"] = "<text if state is true>", -- required
                    ["LabelOff"] = "<text if state is false>" -- required
                }
            },
            {
                ["Type"] = "PROVIDER_SELECTION", -- required
                ["Props"] = {
                    ["Name"] = "Anbieter",
                    ["Label"] = "LLM auswählen"
                }
            },
            {
                ["Type"] = "HEADING", -- descriptive component for headings 
                ["Props"] = {
                    ["Text"] = "This is a Section Heading", -- The heading text
                    ["Level"] = 2 -- Heading level, 1 - 3
                }
            },
            {
                ["Type"] = "TEXT", -- descriptive component for normal text
                ["Props"] = {
                    ["Content"] = "This is a paragraph of descriptive text that explains something about the assistant or provides additional information."
                }
            },
        }
    },
}