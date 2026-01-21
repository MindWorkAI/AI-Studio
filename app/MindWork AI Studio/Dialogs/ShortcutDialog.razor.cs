using AIStudio.Components;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Dialogs;

/// <summary>
/// A dialog for capturing and configuring keyboard shortcuts.
/// </summary>
public partial class ShortcutDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    /// <summary>
    /// The initial shortcut value (in internal format, e.g., "CmdOrControl+1").
    /// </summary>
    [Parameter]
    public string InitialShortcut { get; set; } = string.Empty;

    /// <summary>
    /// The name/identifier of the shortcut for conflict detection.
    /// </summary>
    [Parameter]
    public string ShortcutName { get; set; } = string.Empty;

    private ElementReference hiddenInput;
    private string currentShortcut = string.Empty;
    private string validationMessage = string.Empty;
    private Severity validationSeverity = Severity.Info;
    private bool hasValidationError;

    // Current key state
    private bool hasCtrl;
    private bool hasShift;
    private bool hasAlt;
    private bool hasMeta;
    private string? currentKey;

    private bool isFirstRender = true;

    #region Overrides of ComponentBase

    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.currentShortcut = this.InitialShortcut;
        this.ParseExistingShortcut();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        // Auto-focus the hidden input when the dialog opens
        if (this.isFirstRender)
        {
            this.isFirstRender = false;
            await this.hiddenInput.FocusAsync();
        }
    }

    #endregion

    private void ParseExistingShortcut()
    {
        if (string.IsNullOrWhiteSpace(this.currentShortcut))
            return;

        // Parse the existing shortcut to set the state
        var parts = this.currentShortcut.Split('+');
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "cmdorcontrol":
                case "commandorcontrol":
                case "ctrl":
                case "control":
                case "cmd":
                case "command":
                    this.hasCtrl = true;
                    break;
                
                case "shift":
                    this.hasShift = true;
                    break;
                
                case "alt":
                    this.hasAlt = true;
                    break;
                
                case "meta":
                case "super":
                    this.hasMeta = true;
                    break;
                
                default:
                    this.currentKey = part;
                    break;
            }
        }
    }

    private async Task FocusInput()
    {
        // Focus the hidden input to capture keyboard events
        await this.hiddenInput.FocusAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // Ignore pure modifier key presses
        if (IsModifierKey(e.Code))
        {
            this.UpdateModifiers(e);
            this.currentKey = null;
            this.UpdateShortcutString();
            return;
        }

        // Update modifiers
        this.UpdateModifiers(e);

        // Get the key
        this.currentKey = TranslateKeyCode(e.Code);

        // Validate: must have at least one modifier + a key
        if (!this.hasCtrl && !this.hasShift && !this.hasAlt && !this.hasMeta)
        {
            this.validationMessage = T("Please include at least one modifier key (Ctrl, Shift, Alt, or Cmd).");
            this.validationSeverity = Severity.Warning;
            this.hasValidationError = true;
            this.StateHasChanged();
            return;
        }

        // Build the shortcut string
        this.UpdateShortcutString();

        // Validate the shortcut
        await this.ValidateShortcut();
    }

    private void UpdateModifiers(KeyboardEventArgs e)
    {
        this.hasCtrl = e.CtrlKey || e.MetaKey; // Treat Meta (Cmd on Mac) same as Ctrl for cross-platform
        this.hasShift = e.ShiftKey;
        this.hasAlt = e.AltKey;
        this.hasMeta = e is { MetaKey: true, CtrlKey: false }; // Only set meta if not already using ctrl
    }

    private void UpdateShortcutString()
    {
        var parts = new List<string>();

        if (this.hasCtrl)
            parts.Add("CmdOrControl");
        
        if (this.hasShift)
            parts.Add("Shift");
        
        if (this.hasAlt)
            parts.Add("Alt");

        if (!string.IsNullOrWhiteSpace(this.currentKey))
            parts.Add(this.currentKey);

        this.currentShortcut = parts.Count > 0 ? string.Join("+", parts) : string.Empty;
        this.StateHasChanged();
    }

    private async Task ValidateShortcut()
    {
        if (string.IsNullOrWhiteSpace(this.currentShortcut) || string.IsNullOrWhiteSpace(this.currentKey))
        {
            this.validationMessage = string.Empty;
            this.hasValidationError = false;
            return;
        }

        // Check if the shortcut is valid by trying to register it with Rust
        var result = await this.RustService.ValidateShortcut(this.currentShortcut);
        if (result.IsValid)
        {
            if (result.HasConflict)
            {
                this.validationMessage = string.Format(T("This shortcut conflicts with: {0}"), result.ConflictDescription);
                this.validationSeverity = Severity.Warning;
                this.hasValidationError = false; // Allow saving, but warn
            }
            else
            {
                this.validationMessage = T("Shortcut is valid and available.");
                this.validationSeverity = Severity.Success;
                this.hasValidationError = false;
            }
        }
        else
        {
            this.validationMessage = string.Format(T("Invalid shortcut: {0}"), result.ErrorMessage);
            this.validationSeverity = Severity.Error;
            this.hasValidationError = true;
        }

        this.StateHasChanged();
    }

    private string GetDisplayShortcut()
    {
        if (string.IsNullOrWhiteSpace(this.currentShortcut))
            return string.Empty;

        // Convert internal format to display format
        return this.currentShortcut
            .Replace("CmdOrControl", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl")
            .Replace("CommandOrControl", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl");
    }

    private void ClearShortcut()
    {
        this.currentShortcut = string.Empty;
        this.currentKey = null;
        this.hasCtrl = false;
        this.hasShift = false;
        this.hasAlt = false;
        this.hasMeta = false;
        this.validationMessage = string.Empty;
        this.hasValidationError = false;
        this.StateHasChanged();
    }

    private void Cancel() => this.MudDialog.Cancel();

    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.currentShortcut));

    /// <summary>
    /// Checks if the key code represents a modifier key.
    /// </summary>
    private static bool IsModifierKey(string code) => code switch
    {
        "ShiftLeft" or "ShiftRight" => true,
        "ControlLeft" or "ControlRight" => true,
        "AltLeft" or "AltRight" => true,
        "MetaLeft" or "MetaRight" => true,
        
        _ => false,
    };

    /// <summary>
    /// Translates a JavaScript KeyboardEvent.code to Tauri shortcut format.
    /// </summary>
    private static string TranslateKeyCode(string code) => code switch
    {
        // Letters
        "KeyA" => "A",
        "KeyB" => "B",
        "KeyC" => "C",
        "KeyD" => "D",
        "KeyE" => "E",
        "KeyF" => "F",
        "KeyG" => "G",
        "KeyH" => "H",
        "KeyI" => "I",
        "KeyJ" => "J",
        "KeyK" => "K",
        "KeyL" => "L",
        "KeyM" => "M",
        "KeyN" => "N",
        "KeyO" => "O",
        "KeyP" => "P",
        "KeyQ" => "Q",
        "KeyR" => "R",
        "KeyS" => "S",
        "KeyT" => "T",
        "KeyU" => "U",
        "KeyV" => "V",
        "KeyW" => "W",
        "KeyX" => "X",
        "KeyY" => "Y",
        "KeyZ" => "Z",

        // Numbers
        "Digit0" => "0",
        "Digit1" => "1",
        "Digit2" => "2",
        "Digit3" => "3",
        "Digit4" => "4",
        "Digit5" => "5",
        "Digit6" => "6",
        "Digit7" => "7",
        "Digit8" => "8",
        "Digit9" => "9",

        // Function keys
        "F1" => "F1",
        "F2" => "F2",
        "F3" => "F3",
        "F4" => "F4",
        "F5" => "F5",
        "F6" => "F6",
        "F7" => "F7",
        "F8" => "F8",
        "F9" => "F9",
        "F10" => "F10",
        "F11" => "F11",
        "F12" => "F12",
        "F13" => "F13",
        "F14" => "F14",
        "F15" => "F15",
        "F16" => "F16",
        "F17" => "F17",
        "F18" => "F18",
        "F19" => "F19",
        "F20" => "F20",
        "F21" => "F21",
        "F22" => "F22",
        "F23" => "F23",
        "F24" => "F24",

        // Special keys
        "Space" => "Space",
        "Enter" => "Enter",
        "Tab" => "Tab",
        "Escape" => "Escape",
        "Backspace" => "Backspace",
        "Delete" => "Delete",
        "Insert" => "Insert",
        "Home" => "Home",
        "End" => "End",
        "PageUp" => "PageUp",
        "PageDown" => "PageDown",

        // Arrow keys
        "ArrowUp" => "Up",
        "ArrowDown" => "Down",
        "ArrowLeft" => "Left",
        "ArrowRight" => "Right",

        // Numpad
        "Numpad0" => "Num0",
        "Numpad1" => "Num1",
        "Numpad2" => "Num2",
        "Numpad3" => "Num3",
        "Numpad4" => "Num4",
        "Numpad5" => "Num5",
        "Numpad6" => "Num6",
        "Numpad7" => "Num7",
        "Numpad8" => "Num8",
        "Numpad9" => "Num9",
        "NumpadAdd" => "NumAdd",
        "NumpadSubtract" => "NumSubtract",
        "NumpadMultiply" => "NumMultiply",
        "NumpadDivide" => "NumDivide",
        "NumpadDecimal" => "NumDecimal",
        "NumpadEnter" => "NumEnter",

        // Punctuation
        "Minus" => "Minus",
        "Equal" => "Equal",
        "BracketLeft" => "BracketLeft",
        "BracketRight" => "BracketRight",
        "Backslash" => "Backslash",
        "Semicolon" => "Semicolon",
        "Quote" => "Quote",
        "Backquote" => "Backquote",
        "Comma" => "Comma",
        "Period" => "Period",
        "Slash" => "Slash",

        // Default: return as-is
        _ => code,
    };
}
