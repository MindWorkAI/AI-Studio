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