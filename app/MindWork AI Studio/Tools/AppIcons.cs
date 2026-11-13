namespace AIStudio.Tools;

/// <summary>
/// The icons of AI Studio: one constant for every meaning an icon has in the app.
/// </summary>
/// <remarks>
/// Every icon the app shows comes from here. A component takes the constant of what the icon means, such as
/// AppIcons.DELETE, and never picks a Lucide or a MudBlazor icon on its own. The constants are named after
/// their meaning in AI Studio, not after their shape: two meanings get two constants even when they share a
/// shape, and changing the icon of a meaning changes one line. To show an icon with a new meaning, add a
/// constant here.
///
/// The values come from LucideIcons, which LucideIconGenerator writes from the code points of the Lucide font,
/// see app/SourceGeneratedMappings/Lucide/README.md. MudBlazor renders them as glyphs of that font. Two kinds
/// of icons keep the ones of MudBlazor: brand logos, such as GITHUB, and the numbered steps STEP_1 to STEP_6,
/// because Lucide has no numbered icons.
/// </remarks>
public static class AppIcons
{
    /// <summary>Adds a new item, such as a provider, a profile, a message, or a file.</summary>
    public const string ADD = LucideIcons.SQUARE_PLUS;

    /// <summary>Creates a new workspace.</summary>
    public const string ADD_WORKSPACE = LucideIcons.FOLDER_PLUS;

    /// <summary>Shows or hides advanced or expert settings.</summary>
    public const string ADVANCED_SETTINGS = LucideIcons.SLIDERS_HORIZONTAL;

    /// <summary>Marks an AI agent that works in the background, such as the security audit agent or the content cleaner when it has no usable provider.</summary>
    public const string AGENT = LucideIcons.BOT;

    /// <summary>Marks the agent that cleans text content, such as removing navigation and ads from a loaded web page.</summary>
    public const string AGENT_CONTENT_CLEANER = LucideIcons.BRUSH_CLEANING;

    /// <summary>Marks the agent that selects the data sources for a request.</summary>
    public const string AGENT_DATA_SOURCE_SELECTION = LucideIcons.LIST_CHECKS;

    /// <summary>Marks the agent that validates retrieval contexts.</summary>
    public const string AGENT_RETRIEVAL_CONTEXT_VALIDATION = LucideIcons.LIST_CHECKS;

    /// <summary>Marks the AI, such as its role in a chat.</summary>
    public const string AI = LucideIcons.SPARKLES;

    /// <summary>Marks the data sources that the AI selected for a question.</summary>
    public const string AI_SELECTED_DATA_SOURCES = LucideIcons.LIST_FILTER;

    /// <summary>Sets the goal of an analysis, such as of a SWOT analysis.</summary>
    public const string ANALYSIS_GOAL = LucideIcons.TARGET;

    /// <summary>Stands for a policy of the document analysis, which defines how documents are analyzed.</summary>
    public const string ANALYSIS_POLICY = LucideIcons.CLIPBOARD_LIST;

    /// <summary>Marks what anonymizing replaced, such as the names in a session shared from the BeyondFacts Lab.</summary>
    public const string ANONYMIZED = LucideIcons.VENETIAN_MASK;

    /// <summary>Marks the streamed answer of an AI, such as when generating or reading it fails.</summary>
    public const string ANSWER_STREAM = LucideIcons.ACTIVITY;

    /// <summary>Marks AI Studio itself, such as its version or its app options.</summary>
    public const string APP = LucideIcons.APP_WINDOW;

    /// <summary>Marks an update of AI Studio, such as checking for one or one being available.</summary>
    public const string APP_UPDATE = LucideIcons.PACKAGE_CHECK;

    /// <summary>Marks the assistants, such as their page in the navigation.</summary>
    public const string ASSISTANTS = LucideIcons.LAYOUT_GRID;

    /// <summary>Marks the agenda planner assistant.</summary>
    public const string ASSISTANT_AGENDA = LucideIcons.CALENDAR_CLOCK;

    /// <summary>Marks the batch processing assistant.</summary>
    public const string ASSISTANT_BATCH_PROCESSING = LucideIcons.FILE_STACK;

    /// <summary>Stands for the assistant BeyondFacts Lab.</summary>
    public const string ASSISTANT_BEYOND_FACTS_LAB = LucideIcons.SPEECH;

    /// <summary>Stands for the assistant Bias of the Day.</summary>
    public const string ASSISTANT_BIAS_OF_THE_DAY = LucideIcons.NOTEBOOK_TEXT;

    /// <summary>Marks the assistant builder, which generates new assistants.</summary>
    public const string ASSISTANT_BUILDER = LucideIcons.WRENCH;

    /// <summary>Marks the coding assistant.</summary>
    public const string ASSISTANT_CODING = LucideIcons.CODE_XML;

    /// <summary>Marks the component tree of an assistant plugin, such as its root form.</summary>
    public const string ASSISTANT_COMPONENTS = LucideIcons.DIAGRAM_FLOW_HORIZONTAL;

    /// <summary>Marks the accordion section component of an assistant plugin.</summary>
    public const string ASSISTANT_COMPONENT_ACCORDION_SECTION = LucideIcons.ROWS_2;

    /// <summary>Marks the grid layout component of an assistant plugin.</summary>
    public const string ASSISTANT_COMPONENT_GRID = LucideIcons.LAYOUT_GRID;

    /// <summary>Marks the document analysis assistant and its policies.</summary>
    public const string ASSISTANT_DOCUMENT_ANALYSIS = LucideIcons.SCAN_TEXT;

    /// <summary>Marks the assistant that writes e-mails.</summary>
    public const string ASSISTANT_EMAIL = LucideIcons.MAIL_PEN;

    /// <summary>Stands in for an assistant that has no icon of its own.</summary>
    public const string ASSISTANT_FALLBACK = LucideIcons.SQUARE_DASHED;

    /// <summary>Marks the grammar and spelling assistant.</summary>
    public const string ASSISTANT_GRAMMAR_SPELLING = LucideIcons.SPELL_CHECK_2;

    /// <summary>Marks the icon finder assistant.</summary>
    public const string ASSISTANT_ICON_FINDER = LucideIcons.SCAN_SEARCH;

    /// <summary>Marks the Job Posting assistant.</summary>
    public const string ASSISTANT_JOB_POSTING = LucideIcons.BRIEFCASE_BUSINESS;

    /// <summary>Marks the legal check assistant.</summary>
    public const string ASSISTANT_LEGAL_CHECK = LucideIcons.GAVEL;

    /// <summary>Marks the localization assistant that translates the texts of AI Studio.</summary>
    public const string ASSISTANT_LOCALIZATION = LucideIcons.LANGUAGES;

    /// <summary>Marks the log viewer assistant.</summary>
    public const string ASSISTANT_LOG_VIEWER = LucideIcons.LOGS;

    /// <summary>Marks the Model Comparison assistant.</summary>
    public const string ASSISTANT_MODEL_COMPARISON = LucideIcons.GIT_COMPARE_ARROWS;

    /// <summary>Marks the My Tasks assistant.</summary>
    public const string ASSISTANT_MY_TASKS = LucideIcons.LIST_TODO;

    /// <summary>Marks an installed assistant that comes from an assistant plugin.</summary>
    public const string ASSISTANT_PLUGIN = LucideIcons.PLUG;

    /// <summary>Marks the prompt optimizer assistant.</summary>
    public const string ASSISTANT_PROMPT_OPTIMIZER = LucideIcons.WAND_SPARKLES;

    /// <summary>Marks the rewrite and improve assistant.</summary>
    public const string ASSISTANT_REWRITE = LucideIcons.PENCIL_SPARKLES;

    /// <summary>Marks the Slide Assistant.</summary>
    public const string ASSISTANT_SLIDES = LucideIcons.PRESENTATION;

    /// <summary>Marks the SWOT analysis assistant.</summary>
    public const string ASSISTANT_SWOT_ANALYSIS = LucideIcons.CHART_QUADRANT;

    /// <summary>Marks the Synonyms assistant.</summary>
    public const string ASSISTANT_SYNONYMS = LucideIcons.BOOK_TYPE;

    /// <summary>Marks the Text Summarizer assistant.</summary>
    public const string ASSISTANT_TEXT_SUMMARIZER = LucideIcons.NOTEPAD_TEXT;

    /// <summary>Marks the visual briefing assistant and its briefings.</summary>
    public const string ASSISTANT_VISUAL_BRIEFING = LucideIcons.LAYOUT_DASHBOARD;

    /// <summary>Shows all files attached to a chat.</summary>
    public const string ATTACHED_FILES = LucideIcons.FILE_STACK;

    /// <summary>Attaches files or marks attachments, such as those of a chat or a mail.</summary>
    public const string ATTACH_FILE = LucideIcons.PAPERCLIP;

    /// <summary>Marks the audience or target groups something is meant for, such as a presentation or a plugin.</summary>
    public const string AUDIENCE = LucideIcons.USERS_ROUND;

    /// <summary>Marks the choice of the audience's age group in the slide assistant.</summary>
    public const string AUDIENCE_AGE_GROUP = LucideIcons.CAKE;

    /// <summary>Selects how much expertise the audience has, such as for the slides of the Slide Assistant.</summary>
    public const string AUDIENCE_EXPERTISE = LucideIcons.GRADUATION_CAP;

    /// <summary>Marks the choice of the audience's organizational level in the slide assistant.</summary>
    public const string AUDIENCE_ORGANIZATIONAL_LEVEL = LucideIcons.NETWORK;

    /// <summary>Marks an audio file.</summary>
    public const string AUDIO_FILE = LucideIcons.FILE_MUSIC;

    /// <summary>Marks that a model can take audio as input.</summary>
    public const string AUDIO_INPUT = LucideIcons.AUDIO_LINES;

    /// <summary>Shows how confident the security audit of an assistant plugin is.</summary>
    public const string AUDIT_CONFIDENCE = LucideIcons.GAUGE;

    /// <summary>Signals that the security audit of an assistant plugin could not be completed.</summary>
    public const string AUDIT_INCOMPLETE = LucideIcons.CLIPBOARD_CLOCK;

    /// <summary>Shows that an assistant plugin changed since its security audit.</summary>
    public const string AUDIT_MISMATCH = LucideIcons.SHIELD_X;

    /// <summary>Shows that the security audit rated an assistant plugin as safe.</summary>
    public const string AUDIT_SAFE = LucideIcons.BADGE_CHECK;

    /// <summary>Backs up the data of AI Studio.</summary>
    public const string BACKUP = LucideIcons.ARCHIVE_ARROW_DOWN;

    /// <summary>Marks something as blocked, such as a provider prohibited by the organization or a plugin below the required security level.</summary>
    public const string BLOCKED = LucideIcons.BAN;

    /// <summary>Opens a dialog to choose a file or a folder.</summary>
    public const string BROWSE = LucideIcons.FOLDER_SEARCH;

    /// <summary>Marks a bug, such as the link to report one.</summary>
    public const string BUG = LucideIcons.BUG;

    /// <summary>Marks a build tool, such as the .NET SDK or the Rust compiler.</summary>
    public const string BUILD_TOOLCHAIN = LucideIcons.HAMMER;

    /// <summary>Marks businesses that support AI Studio.</summary>
    public const string BUSINESS_SUPPORTER = LucideIcons.BUILDING;

    /// <summary>Cancels a running operation, such as a backup or a batch run.</summary>
    public const string CANCEL = LucideIcons.CIRCLE_X;

    /// <summary>Marks an operation that was canceled, such as an assistant run or a media import.</summary>
    public const string CANCELED = LucideIcons.CIRCLE_SLASH;

    /// <summary>Marks the category of an item, such as an assistant.</summary>
    public const string CATEGORY = LucideIcons.SHAPES;

    /// <summary>Marks root certificates, such as the path of a certificate bundle.</summary>
    public const string CERTIFICATE = LucideIcons.CERTIFICATE;

    /// <summary>Marks the changelog of AI Studio.</summary>
    public const string CHANGELOG = LucideIcons.FILE_CLOCK;

    /// <summary>Marks a chat, such as the chat page or a chat in a workspace.</summary>
    public const string CHAT = LucideIcons.MESSAGE_SQUARE_TEXT;

    /// <summary>Marks a chat template.</summary>
    public const string CHAT_TEMPLATE = LucideIcons.MESSAGE_SQUARE_TEXT;

    /// <summary>Marks an item in a list of advantages or goals.</summary>
    public const string CHECKLIST_ITEM = LucideIcons.CIRCLE_CHECK_BIG;

    /// <summary>Clears an input, such as a search, a filter, or a shortcut.</summary>
    public const string CLEAR = LucideIcons.ERASER;

    /// <summary>Clears a chosen folder, such as the picture library of the slide assistant.</summary>
    public const string CLEAR_FOLDER = LucideIcons.FOLDER_X;

    /// <summary>Closes or dismisses something, such as an overlay or a suggestion.</summary>
    public const string CLOSE = LucideIcons.X;

    /// <summary>Marks source code, such as a Lua plugin, a programming language, or code contributions.</summary>
    public const string CODE = LucideIcons.CODE;

    /// <summary>Stands for a color, such as a color picker or a color variant.</summary>
    public const string COLOR = LucideIcons.PALETTE;

    /// <summary>Marks the name of a company, such as the employer in a job posting.</summary>
    public const string COMPANY = LucideIcons.BUILDING;

    /// <summary>Marks the messages of a compiler that the coding assistant takes as input.</summary>
    public const string COMPILER_MESSAGES = LucideIcons.TRIANGLE_EXCLAMATION_POINT;

    /// <summary>Marks the accordion layout component of an assistant plugin.</summary>
    public const string COMPONENT_ACCORDION = LucideIcons.LIST_COLLAPSE;

    /// <summary>Marks the button component of an assistant plugin.</summary>
    public const string COMPONENT_BUTTON = LucideIcons.MOUSE_POINTER_CLICK;

    /// <summary>Stands for the button group component of an assistant plugin.</summary>
    public const string COMPONENT_BUTTON_GROUP = LucideIcons.COLUMNS_3;

    /// <summary>Stands for the dropdown component of an assistant plugin.</summary>
    public const string COMPONENT_DROPDOWN = LucideIcons.SQUARE_CHEVRON_DOWN;

    /// <summary>Marks the layout item component of an assistant plugin.</summary>
    public const string COMPONENT_LAYOUT_ITEM = LucideIcons.LAYOUT_PANEL_TOP;

    /// <summary>Stands for the paper layout component of an assistant plugin, a surface that groups other components.</summary>
    public const string COMPONENT_LAYOUT_PAPER = LucideIcons.RECTANGLE_HORIZONTAL;

    /// <summary>Stands for the stack layout component of an assistant plugin, which places components above each other.</summary>
    public const string COMPONENT_LAYOUT_STACK = LucideIcons.LAYERS;

    /// <summary>Marks the properties of a component in the plugin audit.</summary>
    public const string COMPONENT_PROPERTIES = LucideIcons.SLIDERS_HORIZONTAL;

    /// <summary>Confirms a draft or takes over a suggestion.</summary>
    public const string CONFIRM = LucideIcons.CHECK;

    /// <summary>Reports that content was shortened to fit, such as a material or retrieved passages.</summary>
    public const string CONTENT_SHORTENED = LucideIcons.SCISSORS;

    /// <summary>Marks the kind of source that content came from, such as in a prompt injection alert.</summary>
    public const string CONTENT_SOURCE = LucideIcons.FILE_INPUT;

    /// <summary>Sets the context window of a model in tokens.</summary>
    public const string CONTEXT_WINDOW = LucideIcons.RULER;

    /// <summary>Stands for contributions to moderation, design, the wiki, and documentation.</summary>
    public const string CONTRIBUTION_DESIGN_AND_DOCS = LucideIcons.PEN_TOOL;

    /// <summary>Marks the people who contribute content to AI Studio.</summary>
    public const string CONTRIBUTORS = LucideIcons.HANDSHAKE;

    /// <summary>Confirms that content was copied to the clipboard.</summary>
    public const string COPIED = LucideIcons.CHECK_CHECK;

    /// <summary>Copies content, such as text to the clipboard or prompts into a folder.</summary>
    public const string COPY = LucideIcons.COPY;

    /// <summary>Marks a country, such as the legal framework of a job posting.</summary>
    public const string COUNTRY = LucideIcons.FLAG;

    /// <summary>Creates a chat template from the current chat.</summary>
    public const string CREATE_CHAT_TEMPLATE = LucideIcons.BOOKMARK_PLUS;

    /// <summary>Marks the column separator of a CSV results table.</summary>
    public const string CSV_SEPARATOR = LucideIcons.SEPARATOR_VERTICAL;

    /// <summary>Marks an entry the user fills in by hand, such as a mail server that is not in the list.</summary>
    public const string CUSTOM_ENTRY = LucideIcons.PENCIL;

    /// <summary>Marks an array, such as a JSON array.</summary>
    public const string DATA_ARRAY = LucideIcons.BRACKETS;

    /// <summary>Marks the backup and restore of your AI Studio data.</summary>
    public const string DATA_BACKUP = LucideIcons.DATABASE_BACKUP;

    /// <summary>Marks a structured object, such as a JSON object, a dictionary, or a JSON file.</summary>
    public const string DATA_OBJECT = LucideIcons.BRACES;

    /// <summary>Marks the choice of a data source.</summary>
    public const string DATA_SOURCE = LucideIcons.DATABASE;

    /// <summary>Shows that the data sources need no attention, because nothing is indexing and nothing failed.</summary>
    public const string DATA_SOURCES_UP_TO_DATE = LucideIcons.DATABASE_CHECK;

    /// <summary>Marks a single value in a data tree, such as a JSON value or another value of a plugin.</summary>
    public const string DATA_VALUE = LucideIcons.VARIABLE;

    /// <summary>Marks a date, such as a date field or the date picker component.</summary>
    public const string DATE = LucideIcons.CALENDAR;

    /// <summary>Marks a period, such as a date range field or the date range picker component.</summary>
    public const string DATE_RANGE = LucideIcons.CALENDAR_RANGE;

    /// <summary>Deletes or removes an item, such as a chat, a provider, or an attachment.</summary>
    public const string DELETE = LucideIcons.TRASH;

    /// <summary>Deletes the current chat and starts a new one.</summary>
    public const string DELETE_CHAT_AND_START_NEW = LucideIcons.MESSAGE_SQUARE_X;

    /// <summary>Marks a free-text description or context, such as the description of a data source, a plugin, or the context of a word.</summary>
    public const string DESCRIPTION = LucideIcons.SCROLL_TEXT;

    /// <summary>Selects the desktop device, such as for the preview of a visual briefing.</summary>
    public const string DEVICE_DESKTOP = LucideIcons.MONITOR;

    /// <summary>Stands for the preview on a mobile phone.</summary>
    public const string DEVICE_MOBILE = LucideIcons.SMARTPHONE;

    /// <summary>Marks a document, such as a Markdown file.</summary>
    public const string DOCUMENT = LucideIcons.FILE_TEXT;

    /// <summary>Downloads something from the internet, such as Pandoc or the content of a web page.</summary>
    public const string DOWNLOAD = LucideIcons.DOWNLOAD;

    /// <summary>Marks the download of an archive, such as the one for Pandoc.</summary>
    public const string DOWNLOAD_ARCHIVE = LucideIcons.FILE_ARCHIVE;

    /// <summary>Marks the download of an installer, such as the one for Pandoc.</summary>
    public const string DOWNLOAD_INSTALLER = LucideIcons.PACKAGE;

    /// <summary>Marks a draft, such as an unconfirmed result, the draft of an assistant, or the drafts folder of a mailbox.</summary>
    public const string DRAFT = LucideIcons.FILE_PEN_LINE;

    /// <summary>Marks a button that opens a menu below it.</summary>
    public const string DROPDOWN_MENU = LucideIcons.CHEVRON_DOWN;

    /// <summary>Duplicates an item, such as a chat.</summary>
    public const string DUPLICATE = LucideIcons.COPY_PLUS;

    /// <summary>Edits an item, such as a message, a provider, or a profile.</summary>
    public const string EDIT = LucideIcons.PENCIL;

    /// <summary>Shows how much time has elapsed, such as for a running task.</summary>
    public const string ELAPSED_TIME = LucideIcons.TIMER;

    /// <summary>Marks e-mail, such as a mail address to write to or a contact address.</summary>
    public const string EMAIL = LucideIcons.MAIL;

    /// <summary>Shows that data sources are queued for or being embedded in the background.</summary>
    public const string EMBEDDING_IN_PROGRESS = LucideIcons.REFRESH_CW;

    /// <summary>Stands for an embedding provider, which turns text into vectors.</summary>
    public const string EMBEDDING_PROVIDER = LucideIcons.CHART_SCATTER;

    /// <summary>Marks an embedding vector.</summary>
    public const string EMBEDDING_VECTOR = LucideIcons.BRACKETS;

    /// <summary>Enables an item, such as an installed assistant plugin, or shows that it is enabled.</summary>
    public const string ENABLE = LucideIcons.TOGGLE_RIGHT;

    /// <summary>Stands for encrypting a connection, such as the transport security of a mailbox.</summary>
    public const string ENCRYPTION = LucideIcons.LOCK_KEYHOLE;

    /// <summary>Stands for a value chosen from a fixed set of options.</summary>
    public const string ENUM_VALUE = LucideIcons.TAGS;

    /// <summary>Stands for the External Retrieval Interface (ERI), which connects AI Studio to an organization's data.</summary>
    public const string ERI = LucideIcons.SERVER;

    /// <summary>Marks a saved ERI server configuration.</summary>
    public const string ERI_SERVER = LucideIcons.SERVER;

    /// <summary>Reports a real error, such as a failed operation or a failed state. Shown in red; a danger or a notice takes WARNING instead.</summary>
    public const string ERROR = LucideIcons.MONITOR_X;

    /// <summary>Marks an event or occasion, such as an upcoming event in the BeyondFacts Lab or the occasion of a presentation.</summary>
    public const string EVENT = LucideIcons.CALENDAR;

    /// <summary>Marks an event that has already taken place.</summary>
    public const string EVENT_TOOK_PLACE = LucideIcons.CALENDAR_CHECK;

    /// <summary>Marks a field for an example, such as an example prompt.</summary>
    public const string EXAMPLE = LucideIcons.LIGHTBULB;

    /// <summary>Marks the files that a data source excludes.</summary>
    public const string EXCLUDED_FILES = LucideIcons.FUNNEL_X;

    /// <summary>Marks the folders that a data source excludes.</summary>
    public const string EXCLUDED_FOLDERS = LucideIcons.FOLDER_MINUS;

    /// <summary>Reports that executable files are not allowed.</summary>
    public const string EXECUTABLE_BLOCKED = LucideIcons.FILE_X;

    /// <summary>Leaves the fullscreen view.</summary>
    public const string EXIT_FULLSCREEN = LucideIcons.MINIMIZE;

    /// <summary>Collapses a section or closes a list, such as a dropdown.</summary>
    public const string EXPAND_LESS = LucideIcons.CHEVRON_UP;

    /// <summary>Expands a section or opens a list, such as a dropdown.</summary>
    public const string EXPAND_MORE = LucideIcons.CHEVRON_DOWN;

    /// <summary>Exports an item to a file, such as a visual briefing or a plugin archive.</summary>
    public const string EXPORT = LucideIcons.FILE_DOWN;

    /// <summary>Exports settings as a section for an enterprise configuration.</summary>
    public const string EXPORT_CONFIGURATION = LucideIcons.FILE_OUTPUT;

    /// <summary>Marks a single file, such as a file path field.</summary>
    public const string FILE = LucideIcons.FILE;

    /// <summary>Shows the text the AI receives from a file, such as from an attached document.</summary>
    public const string FILE_CONTENT_PREVIEW = LucideIcons.FILE_SEARCH;

    /// <summary>Marks a number of files, such as the matching files of a data source.</summary>
    public const string FILE_COUNT = LucideIcons.FILES;

    /// <summary>Marks a file filter, such as the file patterns a data source includes.</summary>
    public const string FILE_FILTER = LucideIcons.FUNNEL;

    /// <summary>Stands for the HTML file format, such as when exporting content.</summary>
    public const string FILE_FORMAT_HTML = LucideIcons.CODE_XML;

    /// <summary>Marks the LaTeX format for exports.</summary>
    public const string FILE_FORMAT_LATEX = LucideIcons.SIGMA;

    /// <summary>Marks the Markdown file format, such as for an export.</summary>
    public const string FILE_FORMAT_MARKDOWN = LucideIcons.TEXT_INITIAL;

    /// <summary>Marks the OpenDocument text format for exports.</summary>
    public const string FILE_FORMAT_ODT = LucideIcons.FILE_TEXT;

    /// <summary>Marks the table file formats CSV and TSV, such as for an export.</summary>
    public const string FILE_FORMAT_TABLE = LucideIcons.SHEET;

    /// <summary>Marks the Microsoft Word format for exports.</summary>
    public const string FILE_FORMAT_WORD = LucideIcons.FILE_TYPE;

    /// <summary>Reports that a file, or a part of it, could not be found, such as after it was deleted, renamed, or moved.</summary>
    public const string FILE_NOT_FOUND = LucideIcons.FILE_QUESTION_MARK;

    /// <summary>Marks the size of files, such as a file size, the total size of matching files, or the size limit of a data source.</summary>
    public const string FILE_SIZE = LucideIcons.RULER_DIMENSION_LINE;

    /// <summary>Warns that a file's content does not match its file extension.</summary>
    public const string FILE_TYPE_MISMATCH = LucideIcons.FILE_EXCLAMATION_POINT;

    /// <summary>Marks the financial support of AI Studio.</summary>
    public const string FINANCIAL_SUPPORT = LucideIcons.HAND_COINS;

    /// <summary>Marks flagged items, such as the flagged folder of a mailbox.</summary>
    public const string FLAGGED = LucideIcons.FLAG;

    /// <summary>Marks a folder, such as a data folder, a mail folder, or a chosen directory.</summary>
    public const string FOLDER = LucideIcons.FOLDER;

    /// <summary>Marks a group of folders, such as the data folders of AI Studio or a folder with subfolders.</summary>
    public const string FOLDERS = LucideIcons.FOLDERS;

    /// <summary>Opens a folder or shows a file in the file manager of the system, also in messages about it.</summary>
    public const string FOLDER_OPEN = LucideIcons.FOLDER_OPEN;

    /// <summary>Formats the selected text in bold.</summary>
    public const string FORMAT_BOLD = LucideIcons.LETTER_B;

    /// <summary>Formats the selected text as a bulleted list.</summary>
    public const string FORMAT_BULLET_LIST = LucideIcons.LIST;

    /// <summary>Formats the selected text as code.</summary>
    public const string FORMAT_CODE = LucideIcons.CODE;

    /// <summary>Formats the selected text in italics.</summary>
    public const string FORMAT_ITALIC = LucideIcons.ITALIC;

    /// <summary>Shows a dialog or an editor in fullscreen.</summary>
    public const string FULLSCREEN = LucideIcons.MAXIMIZE;

    /// <summary>Lets the AI generate content, such as a draft, a result, or a briefing.</summary>
    public const string GENERATE = LucideIcons.SPARKLES;

    /// <summary>The GitHub logo for the links to the repositories of AI Studio. A brand logo, so it stays the one of MudBlazor.</summary>
    public const string GITHUB = Icons.Custom.Brands.GitHub;

    /// <summary>Marks or inserts a heading, such as the Markdown heading in the chat input or the heading component of an assistant plugin.</summary>
    public const string HEADING = LucideIcons.LETTER_H;

    /// <summary>Shows help, such as examples for file patterns or whether the lab fits a task.</summary>
    public const string HELP = LucideIcons.CIRCLE_QUESTION_MARK;

    /// <summary>Marks something hidden or switched off, such as a hidden secret, hidden files, or a tool nobody can use.</summary>
    public const string HIDDEN = LucideIcons.EYE_OFF;

    /// <summary>Hides a search field, such as the search of the workspaces.</summary>
    public const string HIDE_SEARCH = LucideIcons.SEARCH_SLASH;

    /// <summary>Hides the sidebar, such as the workspaces.</summary>
    public const string HIDE_SIDEBAR = LucideIcons.PANEL_LEFT_CLOSE;

    /// <summary>Marks the home page of AI Studio.</summary>
    public const string HOME = LucideIcons.HOUSE;

    /// <summary>Marks the hostname of a server, such as an ERI server.</summary>
    public const string HOSTNAME = LucideIcons.NETWORK;

    /// <summary>Selects an icon library, such as in the Icon Finder assistant.</summary>
    public const string ICON_SOURCE = LucideIcons.SHAPES;

    /// <summary>Stands for ideas, such as the vision of AI Studio, feature suggestions, or ideas and concepts from supporters.</summary>
    public const string IDEA = LucideIcons.LIGHTBULB;

    /// <summary>Marks a unique identifier, such as the ID of a plugin or the fingerprint of a certificate.</summary>
    public const string IDENTIFIER = LucideIcons.FINGERPRINT_PATTERN;

    /// <summary>Stands for an image, such as an image file, image material, or the image input of a model.</summary>
    public const string IMAGE = LucideIcons.IMAGE;

    /// <summary>Reports that an image could not be read or prepared.</summary>
    public const string IMAGE_BROKEN = LucideIcons.IMAGE_OFF;

    /// <summary>Signals that an image cannot be used, such as an unsupported format or a model that cannot read images.</summary>
    public const string IMAGE_NOT_SUPPORTED = LucideIcons.IMAGE_OFF;

    /// <summary>Imports an item from a file, such as a provider, a profile, a data source, a backup, or a visual briefing.</summary>
    public const string IMPORT = LucideIcons.IMPORT;

    /// <summary>Marks a field for aspects the model should focus on.</summary>
    public const string IMPORTANT_ASPECTS = LucideIcons.LIST;

    /// <summary>Marks a content that an imported plugin brings along.</summary>
    public const string INCLUDED_CONTENT = LucideIcons.CIRCLE_PLUS;

    /// <summary>Marks whether a local directory data source includes its subfolders.</summary>
    public const string INCLUDE_SUBFOLDERS = LucideIcons.FOLDER_TREE;

    /// <summary>Shows information, such as details about an item or the Information page.</summary>
    public const string INFO = LucideIcons.INFO;

    /// <summary>Marks a row of details, such as a configuration value on the information page.</summary>
    public const string INFO_ROW = LucideIcons.MOVE_RIGHT;

    /// <summary>Stands for what goes into an assistant, such as a typical input.</summary>
    public const string INPUT = LucideIcons.LOG_IN;

    /// <summary>Marks the text an assistant works on, such as the input to translate, check, or summarize.</summary>
    public const string INPUT_TEXT = LucideIcons.SCAN_TEXT;

    /// <summary>Stands for installing software, such as installing Pandoc or the way AI Studio is installed.</summary>
    public const string INSTALLATION = LucideIcons.MONITOR_DOWN;

    /// <summary>Marks the automatic installation of a dependency, such as Pandoc.</summary>
    public const string INSTALL_AUTOMATIC = LucideIcons.WAND;

    /// <summary>Marks the manual installation of a dependency, such as Pandoc.</summary>
    public const string INSTALL_MANUAL = LucideIcons.TOOLBOX;

    /// <summary>Stands for the introduction to AI Studio.</summary>
    public const string INTRODUCTION = LucideIcons.BOOK_OPEN;

    /// <summary>Marks a text detail of a job posting, such as the mandatory information, the job description, the qualifications, or the responsibilities.</summary>
    public const string JOB_DETAILS = LucideIcons.CLIPBOARD_LIST;

    /// <summary>Marks the model which judges the answers of other models, such as the judge of the Model Comparison assistant.</summary>
    public const string JUDGE = LucideIcons.GAVEL;

    /// <summary>Stands for a secret credential, such as an API key, a password, or an encryption secret.</summary>
    public const string KEY = LucideIcons.KEY_ROUND;

    /// <summary>Stands for a keyboard shortcut, such as defining a shortcut or a problem with one.</summary>
    public const string KEYBOARD_SHORTCUT = LucideIcons.KEYBOARD;

    /// <summary>Selects or shows a language, such as the target language of an assistant or the language of the operating system.</summary>
    public const string LANGUAGE = LucideIcons.LANGUAGES;

    /// <summary>Marks the date of the last change, such as of a plugin.</summary>
    public const string LAST_CHANGED = LucideIcons.CALENDAR_CLOCK;

    /// <summary>Changes what a direct chat launcher tile opens.</summary>
    public const string LAUNCHER_SETTINGS = LucideIcons.SLIDERS_HORIZONTAL;

    /// <summary>Marks the business purpose of a slide layout.</summary>
    public const string LAYOUT_PURPOSE_BUSINESS = LucideIcons.BRIEFCASE_BUSINESS;

    /// <summary>Marks the slide layouts for general purposes, and layouts of an unknown purpose.</summary>
    public const string LAYOUT_PURPOSE_GENERAL = LucideIcons.LAYOUT_TEMPLATE;

    /// <summary>Marks the slide layouts for science.</summary>
    public const string LAYOUT_PURPOSE_SCIENCE = LucideIcons.TEST_TUBE_DIAGONAL;

    /// <summary>Marks the slide layouts for teaching.</summary>
    public const string LAYOUT_PURPOSE_TEACHING = LucideIcons.GRADUATION_CAP;

    /// <summary>Marks legal matters, such as the law area of slide layouts or the mandatory information of an organization.</summary>
    public const string LEGAL = LucideIcons.SCALE;

    /// <summary>Shows a license, such as the license of AI Studio or of Pandoc.</summary>
    public const string LICENSE = LucideIcons.SCALE;

    /// <summary>Stands for a web link or a reference, such as a link to more information or a URL field.</summary>
    public const string LINK = LucideIcons.LINK;

    /// <summary>Marks the Linux package type AI Studio was installed with.</summary>
    public const string LINUX_PACKAGE = LucideIcons.PACKAGE;

    /// <summary>Stands for a list of points, such as bullet points, an agenda's content list, or the list component of an assistant plugin.</summary>
    public const string LIST = LucideIcons.LIST;

    /// <summary>Stands for an LLM provider, such as configuring providers or selecting one.</summary>
    public const string LLM_PROVIDER = LucideIcons.CPU_AI;

    /// <summary>Marks a field for a location, such as a meeting place or a work location.</summary>
    public const string LOCATION = LucideIcons.MAP_PIN;

    /// <summary>Shows that something is locked, such as a feature your organization manages or a blocked assistant.</summary>
    public const string LOCK = LucideIcons.LOCK;

    /// <summary>Filters log lines by the logger that wrote them.</summary>
    public const string LOGGER = LucideIcons.FOLDER_CODE;

    /// <summary>Marks the log files of AI Studio.</summary>
    public const string LOG_FILES = LucideIcons.NOTEBOOK_TEXT;

    /// <summary>Stands for the level of log entries, such as when filtering the log by level.</summary>
    public const string LOG_LEVEL = LucideIcons.SIGNAL;

    /// <summary>Filters log lines by the details of their source.</summary>
    public const string LOG_SOURCE_DETAILS = LucideIcons.FILE_CODE;

    /// <summary>Stands for a mailbox, such as the mailbox tools or a whole mailbox in the folder picker.</summary>
    public const string MAILBOX = LucideIcons.MAIL;

    /// <summary>Stands for the tool that counts the mails in the user's mailboxes.</summary>
    public const string MAIL_COUNT = LucideIcons.HASH;

    /// <summary>Marks the mail folder that holds all messages.</summary>
    public const string MAIL_FOLDER_ALL = LucideIcons.MAILS;

    /// <summary>Marks the archive folder of a mailbox.</summary>
    public const string MAIL_FOLDER_ARCHIVE = LucideIcons.ARCHIVE;

    /// <summary>Stands for the folder of important mails in a mailbox.</summary>
    public const string MAIL_FOLDER_IMPORTANT = LucideIcons.BADGE_EXCLAMATION_POINT;

    /// <summary>Stands for the inbox folder of a mailbox.</summary>
    public const string MAIL_FOLDER_INBOX = LucideIcons.INBOX;

    /// <summary>Stands for the junk folder of a mailbox.</summary>
    public const string MAIL_FOLDER_JUNK = LucideIcons.BAN;

    /// <summary>Marks the mail folder of sent messages.</summary>
    public const string MAIL_FOLDER_SENT = LucideIcons.SEND;

    /// <summary>Marks the trash folder of a mailbox.</summary>
    public const string MAIL_FOLDER_TRASH = LucideIcons.TRASH;

    /// <summary>Stands for the tool that reads a mail from the user's mailboxes.</summary>
    public const string MAIL_READ = LucideIcons.MAIL_OPEN;

    /// <summary>Stands for the tool that searches the mails of the user's mailboxes.</summary>
    public const string MAIL_SEARCH = LucideIcons.MAIL_SEARCH;

    /// <summary>Lets you pick items by hand, such as the available data sources.</summary>
    public const string MANUAL_SELECTION = LucideIcons.MOUSE_POINTER_CLICK;

    /// <summary>Marks the mathematics tools, such as the calculator.</summary>
    public const string MATHEMATICS = LucideIcons.CALCULATOR;

    /// <summary>Stands for the purpose area medicine.</summary>
    public const string MEDICINE = LucideIcons.STETHOSCOPE;

    /// <summary>Marks the topic of a meeting in the agenda assistant.</summary>
    public const string MEETING_TOPIC = LucideIcons.NOTEPAD_TEXT;

    /// <summary>Stands for recording voice, such as the record button, a recording, or speech input of a model.</summary>
    public const string MICROPHONE = LucideIcons.MIC;

    /// <summary>Signals that voice recording or transcription is not available or failed.</summary>
    public const string MICROPHONE_OFF = LucideIcons.MIC_OFF;

    /// <summary>Marks the AI model a provider uses.</summary>
    public const string MODEL = LucideIcons.CPU_AI;

    /// <summary>Marks a field for the moderator of a meeting.</summary>
    public const string MODERATOR = LucideIcons.USER_STAR;

    /// <summary>Marks the motivation behind AI Studio on the information page.</summary>
    public const string MOTIVATION = LucideIcons.LIGHTBULB;

    /// <summary>Moves a chat into a workspace.</summary>
    public const string MOVE_TO_WORKSPACE = LucideIcons.FOLDER_INPUT;

    /// <summary>Marks the name or title of an item, such as a profile, a data source, or a meeting.</summary>
    public const string NAME = LucideIcons.TAG;

    /// <summary>Marks the network port of a server, such as of an ERI server.</summary>
    public const string NETWORK_PORT = LucideIcons.ETHERNET_PORT;

    /// <summary>Starts a new chat in a workspace.</summary>
    public const string NEW_CHAT = LucideIcons.MESSAGE_SQUARE_PLUS;

    /// <summary>Creates a new folder, such as a workspace or a mail folder.</summary>
    public const string NEW_FOLDER = LucideIcons.FOLDER_PLUS;

    /// <summary>Starts a temporary chat that belongs to no workspace.</summary>
    public const string NEW_TEMPORARY_CHAT = LucideIcons.MESSAGE_SQUARE_DASHED;

    /// <summary>Goes on to the next version or step.</summary>
    public const string NEXT = LucideIcons.ARROW_RIGHT;

    /// <summary>Shows that something has not been created yet, such as a result.</summary>
    public const string NOT_CREATED = LucideIcons.CIRCLE;

    /// <summary>Marks something that is not public and therefore stays off the web, such as a private event.</summary>
    public const string NOT_PUBLIC = LucideIcons.GLOBE_LOCK;

    /// <summary>Marks a step that has not started yet, such as a research of the BeyondFacts Lab.</summary>
    public const string NOT_STARTED = LucideIcons.CIRCLE_DASHED;

    /// <summary>Removes the selected template, so that no template is used.</summary>
    public const string NO_TEMPLATE = LucideIcons.SQUARE_DASHED;

    /// <summary>Stands for a number, such as a numeric field or a numeric value.</summary>
    public const string NUMBER = LucideIcons.HASH;

    /// <summary>Marks a numeric limit, such as the token limit of a chunk or the batch size of an embedding.</summary>
    public const string NUMERIC_LIMIT = LucideIcons.HASH;

    /// <summary>Marks the objective of a meeting in the agenda assistant.</summary>
    public const string OBJECTIVE = LucideIcons.GOAL;

    /// <summary>Opens an assistant in AI Studio.</summary>
    public const string OPEN_ASSISTANT = LucideIcons.SQUARE_ARROW_RIGHT;

    /// <summary>Opens something outside AI Studio, such as a web page in the browser or a file in its program.</summary>
    public const string OPEN_EXTERNAL = LucideIcons.EXTERNAL_LINK;

    /// <summary>Marks a point the user still has to take care of, such as after a data restore.</summary>
    public const string OPEN_POINT = LucideIcons.LIST_TODO;

    /// <summary>Marks the open-source projects that AI Studio uses.</summary>
    public const string OPEN_SOURCE_PROJECTS = LucideIcons.LIBRARY;

    /// <summary>Marks what the organization manages, such as an enterprise configuration, a provider, or a profile.</summary>
    public const string ORGANIZATION = LucideIcons.BUILDING;

    /// <summary>Shows or sets where a chat may send data after reading a data source.</summary>
    public const string OUTBOUND_DATA_RESTRICTION = LucideIcons.GLOBE_LOCK;

    /// <summary>Shows that a result no longer fits its inputs and should be created again.</summary>
    public const string OUTDATED = LucideIcons.ROTATE_CW_CLOCK;

    /// <summary>Stands for what comes out of an assistant, such as the expected output or the output mode.</summary>
    public const string OUTPUT = LucideIcons.LOG_OUT;

    /// <summary>Marks Pandoc, the document converter that AI Studio uses.</summary>
    public const string PANDOC = LucideIcons.FILE_COG;

    /// <summary>Reports that Pandoc is missing or not usable, for example in a wrong version.</summary>
    public const string PANDOC_UNAVAILABLE = LucideIcons.PACKAGE_X;

    /// <summary>Marks a parameter, such as a parameter of a retrieval process.</summary>
    public const string PARAMETER = LucideIcons.SLIDERS_VERTICAL;

    /// <summary>Marks the participants of a meeting, such as their number.</summary>
    public const string PARTICIPANTS = LucideIcons.USERS;

    /// <summary>Marks the check of which retrieved passages fit the question.</summary>
    public const string PASSAGE_RELEVANCE_CHECK = LucideIcons.LIST_CHECKS;

    /// <summary>Marks the component that reads PDF files, such as the PDFium version on the information page.</summary>
    public const string PDF_READER = LucideIcons.FILE_SCAN;

    /// <summary>Marks something that waits or is still in progress, such as a queued job or a configuration being loaded.</summary>
    public const string PENDING = LucideIcons.HOURGLASS;

    /// <summary>Stands for a group of people, such as the authors of a plugin or individual contributors.</summary>
    public const string PEOPLE = LucideIcons.USERS;

    /// <summary>Stands for a picture library, a folder of pictures to choose from.</summary>
    public const string PICTURE_LIBRARY = LucideIcons.IMAGES;

    /// <summary>Marks a plugin, such as an assistant plugin, a configuration plugin, or the plugins page.</summary>
    public const string PLUGIN = LucideIcons.PLUG;

    /// <summary>Marks the file structure of a plugin.</summary>
    public const string PLUGIN_STRUCTURE = LucideIcons.FOLDER_TREE;

    /// <summary>Validates a generated assistant plugin before it is installed.</summary>
    public const string PLUGIN_VALIDATION = LucideIcons.FILE_CHECK;

    /// <summary>Marks a saved set of inputs which can be loaded again, such as a preset of the Model Comparison assistant.</summary>
    public const string PRESET = LucideIcons.BOOKMARK;

    /// <summary>Shows a read-only view or a preview of an item, such as a profile or an assistant draft.</summary>
    public const string PREVIEW = LucideIcons.EYE;

    /// <summary>Marks a feature in the alpha preview stage.</summary>
    public const string PREVIEW_ALPHA = LucideIcons.CHEVRON_FIRST;

    /// <summary>Marks a preview feature in the beta stage.</summary>
    public const string PREVIEW_BETA = LucideIcons.HOURGLASS;

    /// <summary>Previews content on a tablet-sized screen.</summary>
    public const string PREVIEW_DEVICE_TABLET = LucideIcons.TABLET;

    /// <summary>Marks an experimental preview feature.</summary>
    public const string PREVIEW_EXPERIMENTAL = LucideIcons.FLASK_CONICAL;

    /// <summary>Marks a feature in the prototype preview stage.</summary>
    public const string PREVIEW_PROTOTYPE = LucideIcons.HOURGLASS;

    /// <summary>Marks a preview feature that is a release candidate.</summary>
    public const string PREVIEW_RELEASE_CANDIDATE = LucideIcons.ROCKET;

    /// <summary>Goes back to the previous version or step.</summary>
    public const string PREVIOUS = LucideIcons.ARROW_LEFT;

    /// <summary>Explains which of your data stays private, such as what the research of the BeyondFacts Lab never receives.</summary>
    public const string PRIVACY = LucideIcons.SHIELD_USER;

    /// <summary>Marks a profile, such as the profile selection component of an assistant plugin.</summary>
    public const string PROFILE = LucideIcons.CIRCLE_USER_ROUND;

    /// <summary>Marks a prompt or instructions for the model, such as a system prompt or the instructions of a batch run.</summary>
    public const string PROMPT = LucideIcons.NOTEBOOK_PEN;

    /// <summary>Marks the advanced prompt building of an assistant plugin.</summary>
    public const string PROMPT_BUILDING = LucideIcons.BLOCKS;

    /// <summary>Stands for the prompt guide, which the prompt optimizer follows.</summary>
    public const string PROMPT_GUIDE = LucideIcons.BOOK_OPEN_TEXT;

    /// <summary>Selects how confidential content is, such as the protection level of a visual briefing.</summary>
    public const string PROTECTION_LEVEL = LucideIcons.SHIELD_LOCK;

    /// <summary>Marks the provenance database, which stores what the AI received and where it came from.</summary>
    public const string PROVENANCE_STORE = LucideIcons.SCROLL_TEXT;

    /// <summary>Marks the choice of an AI provider.</summary>
    public const string PROVIDER = LucideIcons.SERVER;

    /// <summary>Shows or sets a provider confidence level, such as the confidence of a provider or the one a data source or tool requires.</summary>
    public const string PROVIDER_CONFIDENCE = LucideIcons.SHIELD_LOCK;

    /// <summary>Reports that a request to an AI provider or another remote service failed.</summary>
    public const string PROVIDER_ERROR = LucideIcons.CLOUD_OFF;

    /// <summary>Marks a field for your questions.</summary>
    public const string QUESTIONS = LucideIcons.MESSAGE_CIRCLE_QUESTION_MARK;

    /// <summary>Stands for the quick start guide.</summary>
    public const string QUICK_START = LucideIcons.ROCKET;

    /// <summary>Marks the tool that reads a web page.</summary>
    public const string READ_WEB_PAGE = LucideIcons.NEWSPAPER;

    /// <summary>Stands for the reasoning (thinking) of a model.</summary>
    public const string REASONING = LucideIcons.BRAIN;

    /// <summary>Recompiles a visual briefing from its existing artifacts.</summary>
    public const string RECOMPILE = LucideIcons.FILE_COG;

    /// <summary>Creates a new version with a different design.</summary>
    public const string REDESIGN = LucideIcons.PAINTBRUSH;

    /// <summary>Creates the reflection folder of the BeyondFacts Lab, which collects the confirmed work word for word.</summary>
    public const string REFLECTION_FOLDER = LucideIcons.FOLDER_ARCHIVE;

    /// <summary>Reloads or loads data again, such as a list, a status, or the available models.</summary>
    public const string REFRESH = LucideIcons.REFRESH_CW;

    /// <summary>Generates a result again, such as an answer or a visual briefing.</summary>
    public const string REGENERATE = LucideIcons.REFRESH_CCW;

    /// <summary>Links a source to a file again, such as after the file moved.</summary>
    public const string RELINK = LucideIcons.FILE_SYMLINK;

    /// <summary>Removes an item from a list or a setup, such as a source or what a plugin had set up.</summary>
    public const string REMOVE = LucideIcons.CIRCLE_MINUS;

    /// <summary>Removes items from the index of a data source, such as deleted mails.</summary>
    public const string REMOVE_FROM_INDEX = LucideIcons.LIST_X;

    /// <summary>Renames an item, such as a chat, a workspace, or a project.</summary>
    public const string RENAME = LucideIcons.PENCIL_LINE;

    /// <summary>Repairs an item, such as a data source by indexing it anew.</summary>
    public const string REPAIR = LucideIcons.WRENCH;

    /// <summary>Reports that an item replaced another one, such as a newly selected prompt guide file.</summary>
    public const string REPLACE = LucideIcons.ARROW_LEFT_RIGHT;

    /// <summary>Resets something to its start, such as an assistant's inputs, a conversation, or a history.</summary>
    public const string RESET = LucideIcons.ROTATE_CCW;

    /// <summary>Stands for restarting AI Studio.</summary>
    public const string RESTART_APP = LucideIcons.POWER;

    /// <summary>Restores your data from a backup, which replaces all data.</summary>
    public const string RESTORE_BACKUP = LucideIcons.ARCHIVE_ARROW_UP;

    /// <summary>Restores the default value of a setting or a field.</summary>
    public const string RESTORE_DEFAULT = LucideIcons.UNDO_2;

    /// <summary>Sets the column of a results table that holds the AI answer, such as in batch processing.</summary>
    public const string RESULT_COLUMN = LucideIcons.COLUMNS_3;

    /// <summary>Resumes a paused process.</summary>
    public const string RESUME = LucideIcons.PLAY;

    /// <summary>Transcribes an audio or video source again.</summary>
    public const string RETRANSCRIBE = LucideIcons.AUDIO_LINES;

    /// <summary>Reports that a data source could not be searched, so the search results are incomplete.</summary>
    public const string RETRIEVAL_GAP = LucideIcons.SEARCH_EXCLAMATION_POINT;

    /// <summary>Revises an assistant plugin with the help of the AI.</summary>
    public const string REVISE_ASSISTANT = LucideIcons.PENCIL_SPARKLES;

    /// <summary>Opens the project roadmap of AI Studio.</summary>
    public const string ROADMAP = LucideIcons.MILESTONE;

    /// <summary>Rolls a chat back to an earlier response.</summary>
    public const string ROLLBACK = LucideIcons.ROTATE_CCW_CLOCK;

    /// <summary>Marks a field for rules that something must follow, such as additional rules for a generated assistant.</summary>
    public const string RULES = LucideIcons.LIST_CHECKS;

    /// <summary>Marks an operation that is still running, such as an assistant or a media import.</summary>
    public const string RUNNING = LucideIcons.LOADER_CIRCLE;

    /// <summary>Stands for a runtime that AI Studio runs on, such as .NET or Tauri.</summary>
    public const string RUNTIME = LucideIcons.CPU;

    /// <summary>Marks a run of a model comparison which got no vote, because a model did not answer or the vote was skipped.</summary>
    public const string RUN_WITHOUT_VOTE = LucideIcons.CIRCLE_SLASH;

    /// <summary>Saves something, such as a chat, a setting, or a plugin.</summary>
    public const string SAVE = LucideIcons.SAVE;

    /// <summary>Saves the current inputs as a new preset, such as a setup of the Model Comparison assistant.</summary>
    public const string SAVE_AS_PRESET = LucideIcons.BOOKMARK_PLUS;

    /// <summary>Signals that AI Studio could not save or write something, such as a file, a conversation, or settings.</summary>
    public const string SAVE_FAILED = LucideIcons.SAVE_OFF;

    /// <summary>Searches or filters items, such as chats, log lines, or the matches of a data source.</summary>
    public const string SEARCH = LucideIcons.SEARCH;

    /// <summary>Stands for the database that holds the search index of the data sources.</summary>
    public const string SEARCH_INDEX = LucideIcons.DATABASE_SEARCH;

    /// <summary>Shows that a search found nothing, such as no chats or no log lines.</summary>
    public const string SEARCH_NO_RESULTS = LucideIcons.SEARCH_X;

    /// <summary>Marks security in general, such as the security badge of an assistant plugin.</summary>
    public const string SECURITY = LucideIcons.SHIELD;

    /// <summary>Stands for the security audit of assistant plugins itself, such as starting it or its settings.</summary>
    public const string SECURITY_AUDIT = LucideIcons.SHIELD;

    /// <summary>Shows that an assistant plugin failed its security audit: the result is below the required minimum level.</summary>
    public const string SECURITY_AUDIT_FAILED = LucideIcons.SHIELD_EXCLAMATION_POINT;

    /// <summary>Shows that an assistant plugin passed its security audit: the result meets the required minimum level.</summary>
    public const string SECURITY_AUDIT_PASSED = LucideIcons.SHIELD_CHECK;

    /// <summary>Marks something blocked for security reasons, such as a failed integrity check or a provider below the required confidence level.</summary>
    public const string SECURITY_BLOCKED = LucideIcons.SHIELD_X;

    /// <summary>Marks a plugin that the security audit rates as dangerous.</summary>
    public const string SECURITY_DANGEROUS = LucideIcons.OCTAGON_X;

    /// <summary>Marks the findings of the security audit of a plugin.</summary>
    public const string SECURITY_FINDINGS = LucideIcons.TRIANGLE_EXCLAMATION_POINT;

    /// <summary>Marks an assistant plugin that has no security audit yet.</summary>
    public const string SECURITY_NOT_AUDITED = LucideIcons.SHIELD_X;

    /// <summary>Warns about a possible security risk, such as suspected prompt injections or an assistant plugin that needs an audit.</summary>
    public const string SECURITY_WARNING = LucideIcons.SHIELD_EXCLAMATION_POINT;

    /// <summary>Stands for the semantic search tool, which searches the data sources by meaning.</summary>
    public const string SEMANTIC_SEARCH = LucideIcons.TEXT_SEARCH;

    /// <summary>Sends a message to the AI.</summary>
    public const string SEND = LucideIcons.SEND;

    /// <summary>Sends a result or a message to an assistant or to the chat.</summary>
    public const string SEND_TO = LucideIcons.FORWARD;

    /// <summary>Marks the choice of the sentence structure, such as active or passive voice.</summary>
    public const string SENTENCE_STRUCTURE = LucideIcons.PILCROW;

    /// <summary>Marks a server or host, such as the hostname of a provider, a mail server, or an allowed host.</summary>
    public const string SERVER = LucideIcons.SERVER;

    /// <summary>Opens or marks settings, such as the settings of an assistant, the chat, or the data sources.</summary>
    public const string SETTINGS = LucideIcons.SETTINGS;

    /// <summary>Shares an item with the native share sheet of the operating system, such as a plugin archive.</summary>
    public const string SHARE = LucideIcons.SHARE;

    /// <summary>Shares a session with a research team, such as a preparation of the BeyondFacts Lab with the team which develops it.</summary>
    public const string SHARE_WITH_RESEARCH = LucideIcons.HEART_HANDSHAKE;

    /// <summary>Shows the sidebar, such as the workspaces.</summary>
    public const string SHOW_SIDEBAR = LucideIcons.PANEL_LEFT_OPEN;

    /// <summary>Marks files that are skipped for good, such as files whose embedding failed permanently.</summary>
    public const string SKIPPED = LucideIcons.SKIP_FORWARD;

    /// <summary>Marks slides or a PowerPoint file, such as the slide deck the Slide Assistant plans and creates, or its template.</summary>
    public const string SLIDES = LucideIcons.PRESENTATION;

    /// <summary>Sets the footer text of slides.</summary>
    public const string SLIDE_FOOTER = LucideIcons.PANEL_BOTTOM;

    /// <summary>Selects the layout of a slide, such as the layout the Slide Assistant uses for a purpose.</summary>
    public const string SLIDE_LAYOUT = LucideIcons.LAYOUT_TEMPLATE;

    /// <summary>Marks the slide layouts for structure and framing, such as title or agenda slides.</summary>
    public const string SLIDE_LAYOUT_STRUCTURE = LucideIcons.LIST_ORDERED;

    /// <summary>Shows the sources that an answer is based on.</summary>
    public const string SOURCES = LucideIcons.LINK;

    /// <summary>Marks a source code repository, such as AI Studio on GitHub or the repository of a third-party component.</summary>
    public const string SOURCE_REPOSITORY = LucideIcons.GIT_FORK;

    /// <summary>Shows a status as a colored dot, such as the indexing status of a data source.</summary>
    public const string STATUS_INDICATOR = LucideIcons.CIRCLE;

    /// <summary>Marks the first step of a numbered procedure. Lucide has no numbered icons, so it stays the one of MudBlazor.</summary>
    public const string STEP_1 = Icons.Material.Filled.LooksOne;

    /// <summary>Marks the second step of a numbered procedure. Lucide has no numbered icons, so it stays the one of MudBlazor.</summary>
    public const string STEP_2 = Icons.Material.Filled.LooksTwo;

    /// <summary>Marks the third step of a numbered procedure. Lucide has no numbered icons, so it stays the one of MudBlazor.</summary>
    public const string STEP_3 = Icons.Material.Filled.Looks3;

    /// <summary>Marks the fourth step of a numbered procedure. Lucide has no numbered icons, so it stays the one of MudBlazor.</summary>
    public const string STEP_4 = Icons.Material.Filled.Looks4;

    /// <summary>Marks the fifth step of a numbered procedure. Lucide has no numbered icons, so it stays the one of MudBlazor.</summary>
    public const string STEP_5 = Icons.Material.Filled.Looks5;

    /// <summary>Marks the sixth step of a numbered procedure. Lucide has no numbered icons, so it stays the one of MudBlazor.</summary>
    public const string STEP_6 = Icons.Material.Filled.Looks6;

    /// <summary>Stops running work, such as generating an answer, recording, or transcribing media.</summary>
    public const string STOP = LucideIcons.SQUARE;

    /// <summary>Reports success or a completed, confirmed, or configured state.</summary>
    public const string SUCCESS = LucideIcons.CIRCLE_CHECK;

    /// <summary>Marks the supporters of AI Studio.</summary>
    public const string SUPPORTERS = LucideIcons.HEART;

    /// <summary>Marks the support contact of a plugin.</summary>
    public const string SUPPORT_CONTACT = LucideIcons.HEADSET;

    /// <summary>Marks an on/off value, such as the switch component of an assistant plugin or a boolean value.</summary>
    public const string SWITCH = LucideIcons.TOGGLE_RIGHT;

    /// <summary>Marks a chat message from the system role, such as the system prompt.</summary>
    public const string SYSTEM_ROLE = LucideIcons.COG;

    /// <summary>Marks disappearing chats, which AI Studio deletes after a while.</summary>
    public const string TEMPORARY_CHAT = LucideIcons.CLOCK_FADING;

    /// <summary>Marks a test configuration plugin that nobody deployed and that is valid until AI Studio restarts.</summary>
    public const string TEST_CONFIGURATION = LucideIcons.TEST_TUBE_DIAGONAL;

    /// <summary>Tests a provider by sending it a request.</summary>
    public const string TEST_PROVIDER = LucideIcons.PLUG_ZAP;

    /// <summary>Marks a multi-line text input, such as the text area component of an assistant plugin.</summary>
    public const string TEXT_AREA = LucideIcons.TEXT_ALIGN_START;

    /// <summary>Marks the choice of how complex a text should be.</summary>
    public const string TEXT_COMPLEXITY = LucideIcons.LAYERS;

    /// <summary>Marks a single-line text input, such as the text component of an assistant plugin.</summary>
    public const string TEXT_INPUT = LucideIcons.TEXT_CURSOR_INPUT;

    /// <summary>Stands for time, such as a time of day, a duration, or a timestamp.</summary>
    public const string TIME = LucideIcons.CLOCK;

    /// <summary>Signals that a request took too long and timed out.</summary>
    public const string TIMEOUT = LucideIcons.TIMER_OFF;

    /// <summary>Marks the tokenizer that splits text into tokens, such as the one of an embedding.</summary>
    public const string TOKENIZER = LucideIcons.WHOLE_WORD;

    /// <summary>Marks the token overlap between neighboring chunks.</summary>
    public const string TOKEN_OVERLAP = LucideIcons.SQUARES_INTERSECT;

    /// <summary>Marks a tool that the AI can call, or the tool settings.</summary>
    public const string TOOL = LucideIcons.WRENCH;

    /// <summary>Marks the tool that computes mathematical expressions symbolically.</summary>
    public const string TOOL_SYMBOLIC_MATH = LucideIcons.SQUARE_FUNCTION;

    /// <summary>Marks the trademarks and brand assets of AI Studio.</summary>
    public const string TRADEMARKS = LucideIcons.CIRCLE_LETTER_R;

    /// <summary>Marks the Translation assistant, which translates text into another language.</summary>
    public const string TRANSLATION = LucideIcons.LANGUAGES;

    /// <summary>Shows that something is trusted or approved, such as a plugin approved by your organization or a provider trusted for security checks.</summary>
    public const string TRUSTED = LucideIcons.BADGE_CHECK;

    /// <summary>Marks the type or kind of something, such as the type of an embedding or a plugin.</summary>
    public const string TYPE = LucideIcons.SHAPES;

    /// <summary>Marks the UI framework of AI Studio, such as its version.</summary>
    public const string UI_FRAMEWORK = LucideIcons.BLOCKS;

    /// <summary>Marks something unknown, such as an unknown chat role, file format, audit result, or check result.</summary>
    public const string UNKNOWN = LucideIcons.BADGE_QUESTION_MARK;

    /// <summary>Shows that something is unlocked and may be used.</summary>
    public const string UNLOCK = LucideIcons.LOCK_OPEN;

    /// <summary>Signals that a file type is not supported at this place.</summary>
    public const string UNSUPPORTED_FILE_TYPE = LucideIcons.FILE_X;

    /// <summary>Creates a new version of a result from its current sources, keeping its structure and design.</summary>
    public const string UPDATE_CONTENT = LucideIcons.REFRESH_CCW_DOT;

    /// <summary>Marks the description of when an item is used, such as an embedding.</summary>
    public const string USAGE_CONDITION = LucideIcons.SIGNPOST;

    /// <summary>Marks the user, such as the user name of the operating system.</summary>
    public const string USER = LucideIcons.USER;

    /// <summary>Marks a field for a username to sign in, such as for a mailbox or an ERI server.</summary>
    public const string USERNAME = LucideIcons.USER_ROUND;

    /// <summary>Moves a result back into the input field so that you can work on it again.</summary>
    public const string USE_AS_INPUT = LucideIcons.TEXT_CURSOR_INPUT;

    /// <summary>Marks a list value, such as in the metadata of an assistant plugin.</summary>
    public const string VALUE_LIST = LucideIcons.LIST;

    /// <summary>Marks a missing value in the value tree of the plugin audit.</summary>
    public const string VALUE_NULL = LucideIcons.CIRCLE_OFF;

    /// <summary>Marks a text value in the value tree of the plugin audit.</summary>
    public const string VALUE_TEXT = LucideIcons.CASE_SENSITIVE;

    /// <summary>Marks the vector database of AI Studio.</summary>
    public const string VECTOR_STORE = LucideIcons.DATABASE;

    /// <summary>Marks the version of an item, such as of a plugin.</summary>
    public const string VERSION = LucideIcons.GIT_COMMIT_HORIZONTAL;

    /// <summary>Lists the versions of AI Studio and the components it uses.</summary>
    public const string VERSIONS = LucideIcons.PACKAGE;

    /// <summary>Marks video files.</summary>
    public const string VIDEO = LucideIcons.FILM;

    /// <summary>Shows that a hidden value is visible, such as a secret in an input field.</summary>
    public const string VISIBLE = LucideIcons.EYE;

    /// <summary>Marks the transcription of speech into text, such as of a voice recording or a media file.</summary>
    public const string VOICE_TRANSCRIPTION = LucideIcons.AUDIO_LINES;

    /// <summary>Marks a run of a model comparison which got your vote.</summary>
    public const string VOTED = LucideIcons.VOTE;

    /// <summary>Marks the answer which lost a vote of a model comparison.</summary>
    public const string VOTE_LOST = LucideIcons.CIRCLE_X;

    /// <summary>Marks a run of a model comparison which still waits for your vote.</summary>
    public const string VOTE_MISSING = LucideIcons.CIRCLE_QUESTION_MARK;

    /// <summary>Marks two answers which a vote of a model comparison found equally good.</summary>
    public const string VOTE_TIE = LucideIcons.CIRCLE_EQUAL;

    /// <summary>Marks the answer which won a vote of a model comparison.</summary>
    public const string VOTE_WON = LucideIcons.CIRCLE_CHECK;

    /// <summary>Signals a warning.</summary>
    public const string WARNING = LucideIcons.TRIANGLE_EXCLAMATION_POINT;

    /// <summary>Stands for the web, such as reading a web page, the web search tool, or researching a topic on the web.</summary>
    public const string WEB = LucideIcons.GLOBE;

    /// <summary>Marks a website, such as the homepage of MindWork AI.</summary>
    public const string WEBSITE = LucideIcons.GLOBE;

    /// <summary>Marks the research of the BeyondFacts Lab on the web and the report it writes from it.</summary>
    public const string WEB_RESEARCH = LucideIcons.SCROLL_TEXT;

    /// <summary>Withdraws a confirmation, such as of a work state in the BeyondFacts Lab.</summary>
    public const string WITHDRAW = LucideIcons.UNDO_2;

    /// <summary>Marks a word or phrase to work with, such as the one the Synonyms assistant finds synonyms for.</summary>
    public const string WORD_OR_PHRASE = LucideIcons.CASE_SENSITIVE;

    /// <summary>Marks a workspace that groups chats, such as in the workspace tree or the workspace selection.</summary>
    public const string WORKSPACE = LucideIcons.FOLDER_KANBAN;

    /// <summary>Shows your workspaces with their chats.</summary>
    public const string WORKSPACES = LucideIcons.FOLDER_TREE;

    /// <summary>Marks the writer mode.</summary>
    public const string WRITER = LucideIcons.PEN_LINE;

    /// <summary>Marks the writing style an assistant uses for a text.</summary>
    public const string WRITING_STYLE = LucideIcons.FEATHER;
}