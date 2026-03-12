window.generateDiff = function (text1, text2, divDiff, divLegend) {
    let wikEdDiff = new WikEdDiff();
    let targetDiv = document.getElementById(divDiff)
    targetDiv.innerHTML = wikEdDiff.diff(text1, text2);
    targetDiv.classList.add('mud-typography-body1', 'improvedDiff');
    
    let legend = document.getElementById(divLegend);
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
