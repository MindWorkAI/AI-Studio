window.generateDiff = function (text1, text2, divDiff, divLegend) {
    let wikEdDiff = new WikEdDiff();
    let targetDiv = document.getElementById(divDiff)
    if (!targetDiv) {
        return;
    }

    targetDiv.innerHTML = wikEdDiff.diff(text1, text2);
    targetDiv.classList.add('mud-typography-body1', 'improvedDiff');
    
    let legend = document.getElementById(divLegend);
    if (!legend) {
        return;
    }

    legend.innerHTML = `
    <div class="legend mt-2">
        <h3>Legend</h3>
        <ul class="mt-2">
            <li><span class="wikEdDiffMarkRight" title="Moved block" id="wikEdDiffMark999" onmouseover="wikEdDiffBlockHandler(undefined, this, 'mouseover');"></span> Original block position</li>
            <li><span title="+" class="wikEdDiffInsert">Inserted<span class="wikEdDiffSpace"><span class="wikEdDiffSpaceSymbol"></span> </span>text<span class="wikEdDiffNewline"> </span></span></li>
            <li><span title="−" class="wikEdDiffDelete">Deleted<span class="wikEdDiffSpace"><span class="wikEdDiffSpaceSymbol"></span> </span>text<span class="wikEdDiffNewline"> </span></span></li>
            <li><span class="wikEdDiffBlockLeft" title="◀" id="wikEdDiffBlock999" onmouseover="wikEdDiffBlockHandler(undefined, this, 'mouseover');">Moved<span class="wikEdDiffSpace"><span class="wikEdDiffSpaceSymbol"></span> </span>block<span class="wikEdDiffNewline"> </span></span></li>
        </ul>
    </div>
    `;
}

window.clearDiv = function (divName) {
    let targetDiv = document.getElementById(divName);
    if (!targetDiv) {
        return;
    }

    targetDiv.innerHTML = '';
}

// We add a click handler to the provider group headers so that clicking anywhere on the header expands or collapses the group.
// Right now (August 2026), this is not possible using MudBlazor.
document.addEventListener('click', function (event) {
    const target = event.target
    if (!(target instanceof Element))
        return

    const groupHeaderRow = target.closest('tr')
    if (!groupHeaderRow?.querySelector(':scope > .provider-group-header'))
        return

    if (target.closest('.mud-table-row-expander'))
        return

    groupHeaderRow.querySelector('.mud-table-row-expander')?.click()
})

window.scrollToBottom = function(element) {
    element.scrollIntoView({ behavior: 'smooth', block: 'end', inline: 'nearest' });
}

window.formatChatInputMarkdown = function (inputId, formatType) {
    let input = document.getElementById(inputId)
    if (input && input.tagName !== 'TEXTAREA' && input.tagName !== 'INPUT')
        input = input.querySelector('textarea, input')

    if (!input)
        return ''

    input.focus()

    const value = input.value ?? ''
    const start = input.selectionStart ?? value.length
    const end = input.selectionEnd ?? value.length
    const hasSelection = end > start
    const selectedText = value.substring(start, end)

    let insertedText = ''
    let selectionStart = start
    let selectionEnd = start

    switch (formatType) {
        case 'bold': {
            const text = hasSelection ? selectedText : ''
            insertedText = `**${text}**`
            selectionStart = start + 2
            selectionEnd = selectionStart + text.length
            break
        }

        case 'italic': {
            const text = hasSelection ? selectedText : ''
            insertedText = `*${text}*`
            selectionStart = start + 1
            selectionEnd = selectionStart + text.length
            break
        }

        case 'heading': {
            if (hasSelection) {
                insertedText = selectedText
                    .split('\n')
                    .map(line => line.startsWith('# ') ? line : `# ${line}`)
                    .join('\n')

                selectionStart = start
                selectionEnd = start + insertedText.length
            } else {
                const text = ''
                insertedText = `# ${text}`
                selectionStart = start + 2
                selectionEnd = selectionStart + text.length
            }

            break
        }

        case 'bullet_list': {
            if (hasSelection) {
                insertedText = selectedText
                    .split('\n')
                    .map(line => line.startsWith('- ') ? line : `- ${line}`)
                    .join('\n')

                selectionStart = start
                selectionEnd = start + insertedText.length
            } else {
                insertedText = '- '
                selectionStart = start + 2
                selectionEnd = start + insertedText.length
            }

            break
        }

        case 'code':
        default: {
            if (hasSelection) {
                if (selectedText.includes('\n')) {
                    insertedText = `\`\`\`\n${selectedText}\n\`\`\``
                    selectionStart = start + 4
                    selectionEnd = selectionStart + selectedText.length
                } else {
                    insertedText = `\`${selectedText}\``
                    selectionStart = start + 1
                    selectionEnd = selectionStart + selectedText.length
                }
            } else {
                const text = ''
                insertedText = `\`${text}\``
                selectionStart = start + 1
                selectionEnd = selectionStart + text.length
            }

            break
        }
    }

    const nextValue = value.slice(0, start) + insertedText + value.slice(end)
    input.value = nextValue
    input.setSelectionRange(selectionStart, selectionEnd)
    input.dispatchEvent(new Event('input', { bubbles: true }))

    return nextValue
}

const escapeHandlers = new Map()

window.registerEscapeHandler = function (id, dotNetReference) {
    window.unregisterEscapeHandler(id)

    const handler = function (event) {
        if (event.key !== 'Escape')
            return

        event.preventDefault()
        event.stopPropagation()
        dotNetReference.invokeMethodAsync('HandleEscapeKeyAsync').catch(() => {})
    }

    document.addEventListener('keydown', handler, true)
    escapeHandlers.set(id, handler)
}

window.unregisterEscapeHandler = function (id) {
    const handler = escapeHandlers.get(id)
    if (!handler)
        return

    document.removeEventListener('keydown', handler, true)
    escapeHandlers.delete(id)
}

const localShortcutHandlers = new Map()

function tauriKeyFromKeyboardCode(code) {
    if (/^Key[A-Z]$/.test(code))
        return code.substring(3)

    if (/^Digit[0-9]$/.test(code))
        return code.substring(5)

    if (/^F(?:[1-9]|1[0-9]|2[0-4])$/.test(code))
        return code

    const keys = {
        Space: 'Space', Enter: 'Enter', Tab: 'Tab', Escape: 'Escape', Backspace: 'Backspace',
        Delete: 'Delete', Insert: 'Insert', Home: 'Home', End: 'End', PageUp: 'PageUp', PageDown: 'PageDown',
        ArrowUp: 'Up', ArrowDown: 'Down', ArrowLeft: 'Left', ArrowRight: 'Right',
        Numpad0: 'Num0', Numpad1: 'Num1', Numpad2: 'Num2', Numpad3: 'Num3', Numpad4: 'Num4',
        Numpad5: 'Num5', Numpad6: 'Num6', Numpad7: 'Num7', Numpad8: 'Num8', Numpad9: 'Num9',
        NumpadAdd: 'NumAdd', NumpadSubtract: 'NumSubtract', NumpadMultiply: 'NumMultiply',
        NumpadDivide: 'NumDivide', NumpadDecimal: 'NumDecimal', NumpadEnter: 'NumEnter',
        Minus: 'Minus', Equal: 'Equal', BracketLeft: 'BracketLeft', BracketRight: 'BracketRight',
        Backslash: 'Backslash', Semicolon: 'Semicolon', Quote: 'Quote', Backquote: 'Backquote',
        Comma: 'Comma', Period: 'Period', Slash: 'Slash'
    }

    return keys[code] ?? code
}

function parseTauriShortcut(shortcut) {
    const expected = { ctrl: false, shift: false, alt: false, meta: false, key: '' }
    const isMac = /Mac|iPhone|iPad|iPod/.test(navigator.platform)

    for (const rawPart of shortcut.split('+')) {
        const part = rawPart.trim().toLowerCase()
        switch (part) {
            case 'cmdorcontrol':
            case 'commandorcontrol':
                expected[isMac ? 'meta' : 'ctrl'] = true
                break
            case 'ctrl':
            case 'control':
                expected.ctrl = true
                break
            case 'cmd':
            case 'command':
            case 'meta':
            case 'super':
                expected.meta = true
                break
            case 'shift':
                expected.shift = true
                break
            case 'alt':
            case 'option':
                expected.alt = true
                break
            default:
                expected.key = rawPart.trim()
                break
        }
    }

    return expected
}

window.localShortcut = {
    register: function (id, shortcut, dotNetReference) {
        this.unregister(id)
        const expected = parseTauriShortcut(shortcut)
        if (!expected.key)
            return

        const handler = function (event) {
            if (event.repeat
                || event.ctrlKey !== expected.ctrl
                || event.shiftKey !== expected.shift
                || event.altKey !== expected.alt
                || event.metaKey !== expected.meta
                || tauriKeyFromKeyboardCode(event.code).toLowerCase() !== expected.key.toLowerCase())
                return

            event.preventDefault()
            event.stopPropagation()
            dotNetReference.invokeMethodAsync('OnLocalShortcutPressed').catch(() => {})
        }

        document.addEventListener('keydown', handler, true)
        localShortcutHandlers.set(id, handler)
    },

    unregister: function (id) {
        const handler = localShortcutHandlers.get(id)
        if (!handler)
            return

        document.removeEventListener('keydown', handler, true)
        localShortcutHandlers.delete(id)
    }
}

// What floats above the page without ever being a drop target. Two of these take part in hit testing as
// MudBlazor 8.15 stands: an open .mud-popover -- a closed one already declines pointer events through
// .mud-popover:not(.mud-popover-open) -- and .mud-snackbar, which asks for them explicitly with
// pointer-events: auto even though its container declines them, and snackbars appear constantly in this
// app. Without this list, a drag would be answered by whatever happens to float on screen rather than by
// the page below it. The remaining three are named because they surround those two: .mud-tooltip is the
// content of a popover, while #mud-snackbar-container and .mud-badge-wrapper carry pointer-events: none
// today and therefore never reach a hit test at all. Should a MudBlazor version drop that, they are
// covered here already. Children of all of them have to be skipped as well, which is why the test below
// uses closest rather than matches.
const skippedDropOverlays = '.mud-popover, .mud-tooltip, .mud-snackbar, #mud-snackbar-container, .mud-badge-wrapper'

// The drop zones of the app, addressed by the cursor position of a native drag and drop event.
//
// The arbitration between overlapping zones is left to the browser, and it can be: MudBlazor 8.15 gives
// neither .mud-dialog-container nor .mud-overlay a pointer-events: none. Both fill the viewport while a
// dialog is open, so a point beside the dialog box hits the container, and nothing there is a drop zone. A
//  drop, therefore, cannot reach through an open dialog into the page behind it -- the very thing the app
// used to enforce by counting layers in C#. That single CSS property carries this whole design, so it
// belongs on the checklist for every MudBlazor major version, starting with the move to 9.
window.dropZones = {

    // Names the drop zone at the given viewport position, or null when there is none.
    //
    // The stack of elements is walked from the top down rather than asking for the topmost one alone,
    // because the topmost one may be an overlay from the list above and skipping it has to reveal what
    // lies beneath. The first element which is not skipped ends the walk, whether it belongs to a drop
    // zone or not: anything unknown blocks on purpose, so a drop can never slip through something the
    // user sees as being in the way. Within that element, closest resolves from the inside out, so a
    // specific zone inside a page-wide one wins -- which is exactly the precedence we want.
    hitTest: function (x, y) {
        for (const element of document.elementsFromPoint(x, y)) {
            if (element.closest(skippedDropOverlays))
                continue

            return element.closest('[data-drop-zone-id]')?.getAttribute('data-drop-zone-id') ?? null
        }

        return null
    },

    // Every drop zone currently in the DOM, in document order. This is for diagnostics only: when a drop
    // lands nowhere, it answers the question of which zones would have been available at that moment.
    list: function () {
        return Array.from(document.querySelectorAll('[data-drop-zone-id]'), zone => zone.getAttribute('data-drop-zone-id'))
    }
}