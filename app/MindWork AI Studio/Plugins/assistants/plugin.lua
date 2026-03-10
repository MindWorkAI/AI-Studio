require("icon")

--[[
  This sample assistant shows how plugin authors map Lua tables into UI components.
  Each component declares a `UserPrompt` which is prepended as a `context` block, followed
  by the actual component value in `user prompt`. See
  `app/MindWork AI Studio/Plugins/assistants/README.md` for the full data-model reference.
]]

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
    ["Description"] = "<assistant description>", -- required
    ["SystemPrompt"] = "<prompt that fundamentally changes behaviour, personality and task focus of your assistant. Invisible to the user>", -- required
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
                    ["Adornment"] = "<Start|End|None>", --  location of the `AdornmentIcon` OR `AdornmentText`; CASE SENSITIV
                    ["AdornmentIcon"] = "Icons.Material.Filled.AppSettingsAlt", -- The Mudblazor icon displayed for the adornment
                    ["AdornmentText"] = "", -- The text displayed for the adornment
                    ["AdornmentColor"] = "<Dark|Error|Info|Inherit|Primary|Secondary|Success|Surface|Tertiary|Transparent|Warning>", -- the color of AdornmentText or AdornmentIcon; CASE SENSITIVE
                    ["Counter"] = 0, -- shows a character counter. When 0, the current character count is displayed. When 1 or greater, the character count and this count are displayed. Defaults to `null` 
                    ["MaxLength"] = 100, -- max number of characters allowed, prevents more input characters; use together with the character counter. Defaults to 524,288
                    ["HelperText"] = "<a helping text rendered under the text area to give hints to users>",
                    ["IsImmediate"] = false, -- changes the value as soon as input is received. Defaults to false but will be true if counter or maxlength is set to reflect changes
                    ["HelperTextOnFocus"] = true, -- if true, shows the helping text only when the user focuses on the text area
                    ["UserPrompt"] = "<direct input of instructions, questions, or tasks by a user>",
                    ["PrefillText"] = "<text to show in the field initially>",
                    ["IsSingleLine"] = false, -- if true, shows a text field instead of an area
                    ["ReadOnly"] = false, -- if true, deactivates user input (make sure to provide a PrefillText)
                    ["Class"] = "<MudBlazor or css classes>",
                    ["Style"] = "<css styles>",
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
                    ["Value"] = true, -- initial switch state
                    ["Disabled"] = false, -- if true, disables user interaction but the value can still be used in the user prompt (use for presentation purposes)
                    ["UserPrompt"] = "<direct input of instructions, questions, or tasks by a user>",
                    ["LabelOn"] = "<text if state is true>",
                    ["LabelOff"] = "<text if state is false>",
                    ["LabelPlacement"] = "<Bottom|End|Left|Right|Start|Top>", -- Defaults to End (right of the switch)
                    ["Icon"] = "Icons.Material.Filled.Bolt", -- places a thumb icon inside the switch
                    ["IconColor"] = "<Dark|Error|Info|Inherit|Primary|Secondary|Success|Surface|Tertiary|Transparent|Warning>", -- color of the thumb icon. Defaults to `Inherit`
                    ["CheckedColor"] = "<Dark|Error|Info|Inherit|Primary|Secondary|Success|Surface|Tertiary|Transparent|Warning>", -- color of the switch if state is true. Defaults to `Inherit`
                    ["UncheckedColor"] = "<Dark|Error|Info|Inherit|Primary|Secondary|Success|Surface|Tertiary|Transparent|Warning>", -- color of the switch if state is false. Defaults to `Inherit`
                }
            },
            {
                ["Type"] = "PROVIDER_SELECTION", -- required
                ["Props"] = {
                    ["Name"] = "Provider",
                    ["Label"] = "Choose LLM"
                }
            },
            -- If you add a PROFILE_SELECTION component, AI Studio will hide the footer selection and use this block instead:
            {
                ["Type"] = "PROFILE_SELECTION",
                ["Props"] = {
                    ["ValidationMessage"] = "<warning message that is shown when the user has not picked a profile>"
                }
            },
            {
                ["Type"] = "HEADING", -- descriptive component for headings 
                ["Props"] = {
                    ["Text"] = "<heading content>", -- required
                    ["Level"] = 2 -- Heading level, 1 - 3
                }
            },
            {
                ["Type"] = "TEXT", -- descriptive component for normal text
                ["Props"] = {
                    ["Content"] = "<text content>"
                }
            },
            {
                ["Type"] = "LIST", -- descriptive list component
                ["Props"] = {
                    ["Items"] = {
                        { 
                            ["Type"] = "LINK", -- required 
                            ["Text"] = "<user readable link text>", 
                            ["Href"] = "<link>" -- required 
                        },
                        { 
                            ["Type"] = "TEXT", -- required 
                            ["Text"] = "<user readable text>"
                        }
                    }
                }
            },
            {
                ["Type"] = "IMAGE",
                ["Props"] = {
                    ["Src"] = "plugin://assets/example.png",
                    ["Alt"] = "SVG-inspired placeholder",
                    ["Caption"] = "Static illustration via the IMAGE component."
                }
            },
            {
                ["Type"] = "WEB_CONTENT_READER", -- allows the user to fetch a URL and clean it
                ["Props"] = {
                    ["Name"] = "<unique identifier of this component>", -- required
                    ["UserPrompt"] = "<help text that explains the purpose of this reader>",
                    ["Preselect"] = false, -- automatically show the reader when the assistant opens
                    ["PreselectContentCleanerAgent"] = true -- run the content cleaner by default
                }
            },
            {
                ["Type"] = "FILE_CONTENT_READER", -- allows the user to load local files
                ["Props"] = {
                    ["Name"] = "<unique identifier of this component>", -- required
                    ["UserPrompt"] = "<help text reminding the user what kind of file they should load>"
                }
            },
            {
                ["Type"] = "COLOR_PICKER",
                ["Props"] = {
                    ["Name"] = "<unique identifier of this component>", -- required
                    ["Label"] = "<heading of your component>", -- required
                    ["Placeholder"] = "<use this as a default color property with HEX code (e.g '#FFFF12') or just show hints to the user>",
                    ["ShowAlpha"] = true, -- weather alpha channels are shown
                    ["ShowToolbar"] = true, -- weather the toolbar to toggle between picker, grid or palette is shown
                    ["ShowModeSwitch"] = true, -- weather switch to toggle between RGB(A), HEX or HSL color mode is shown
                    ["PickerVariant"] = "<Dialog|Inline|Static>", -- different rendering modes: `Dialog` opens the picker in a modal type screen, `Inline` shows the picker next to the input field and `Static` renders the picker widget directly (default); Case sensitiv
                    ["UserPrompt"] = "<help text reminding the user what kind of file they should load>",
                }
            },
        }
    },
}
