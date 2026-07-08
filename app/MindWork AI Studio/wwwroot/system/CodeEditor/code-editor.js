import { CodeJar } from "./codejar.js?v=20260707";

const editors = new Map();

const LUA_KEYWORDS = new Set([
    'and', 'break', 'do', 'else', 'elseif', 'end', 'for', 'function', 'goto',
    'if', 'in', 'local', 'not', 'or', 'repeat', 'return', 'then', 'until', 'while'
]);
const LUA_LITERALS = new Set(['false', 'nil', 'true']);
const LUA_BUILT_INS = new Set([
    '_G', '_VERSION', 'assert', 'collectgarbage', 'dofile', 'error', 'getmetatable',
    'ipairs', 'load', 'loadfile', 'next', 'pairs', 'pcall', 'print', 'rawequal',
    'rawget', 'rawlen', 'rawset', 'require', 'select', 'setmetatable', 'tonumber',
    'tostring', 'type', 'xpcall', 'coroutine', 'debug', 'io', 'math', 'os',
    'package', 'string', 'table', 'utf8'
]);

/**
 * Creates a CodeJar editor for the Blazor component instance.
 *
 * CodeJar's public surface is intentionally small:
 * - CodeJar(element, highlighter, options) turns a 'contenteditable' element into an editor.
 * - updateCode(code) replaces the editor content and reruns highlighting.
 * - toString() reads the plain text content back out.
 * - destroy() removes listeners created by CodeJar.
 *
 * The highlighter callback receives the editor DOM node. It must write highlighted
 * HTML back into that node, so every token emitted by our highlighter is HTML-escaped.
 */
export function init(id, element, code, language) {
    const codeJar = CodeJar(element, getHighlighter(language), {
        tab: '    ',
        spellcheck: false
    });
    
    codeJar.updateCode(code ?? '');
    editors.set(id, codeJar);
}

/**
 * Returns the current plain text from a CodeJar instance.
 */
export function getCode(id) {
    return editors.get(id)?.toString() ?? '';
}

/**
 * Replaces the editor content through CodeJar so the cursor/history/highlighter
 * state stays consistent with CodeJar's internal model.
 */
export function setCode(id, code) {
    editors.get(id)?.updateCode(code ?? '');
}

/**
 * Disposes one editor instance and removes it from the JS-side registry.
 */
export function destroy(id) {
    editors.get(id)?.destroy();
    editors.delete(id);
}

function highlightLua(editor) {
    editor.innerHTML = highlightLuaCode(editor.textContent ?? '');
}

function highlightPlainText() {
}

function getHighlighter(language) {
    switch ((language ?? '').toLowerCase()) {
        case 'lua':
            return highlightLua;

        default:
            return highlightPlainText;
    }
}

/**
 * Lightweight Lua highlighter.
 *
 * This intentionally does not use one large regex. Lua comments, long strings
 * (`[[...]]`, `[=[...]=]`), quoted strings and numbers can overlap with words
 * that would otherwise look like keywords or variables. A small scanner lets us
 * consume those regions first and only tokenize identifiers after that.
 */
function highlightLuaCode(code) {
    let html = '';
    let index = 0;
    const localVariables = collectLuaLocalVariables(code);

    while (index < code.length) {
        const char = code[index];
        const next = code[index + 1];

        if (char === '-' && next === '-') {
            const longCommentEnd = readLuaLongBracketEnd(code, index + 2);
            if (longCommentEnd) {
                html += wrapLuaToken(code.slice(index, longCommentEnd.end), 'comment');
                index = longCommentEnd.end;
                continue;
            }

            const lineEnd = findLineEnd(code, index);
            html += wrapLuaToken(code.slice(index, lineEnd), 'comment');
            index = lineEnd;
            continue;
        }

        const longStringEnd = readLuaLongBracketEnd(code, index);
        if (longStringEnd) {
            html += wrapLuaToken(code.slice(index, longStringEnd.end), 'string');
            index = longStringEnd.end;
            continue;
        }

        if (char === '"' || char === "'") {
            const stringEnd = readQuotedStringEnd(code, index, char);
            html += wrapLuaToken(code.slice(index, stringEnd), 'string');
            index = stringEnd;
            continue;
        }

        if (isNumberStart(code, index)) {
            const numberEnd = readNumberEnd(code, index);
            html += wrapLuaToken(code.slice(index, numberEnd), 'number');
            index = numberEnd;
            continue;
        }

        if (isIdentifierStart(char)) {
            const functionCallEnd = readFunctionCallNameEnd(code, index);
            if (functionCallEnd > index) {
                html += wrapLuaToken(code.slice(index, functionCallEnd), 'function');
                index = functionCallEnd;
                continue;
            }

            const identifierEnd = readIdentifierEnd(code, index);
            const identifier = code.slice(index, identifierEnd);
            const previousChar = findPreviousNonWhitespaceChar(code, index);
            html += highlightLuaIdentifier(identifier, localVariables, previousChar);
            index = identifierEnd;
            continue;
        }

        html += escapeHtml(char);
        index++;
    }

    return html;
}

/**
 * Classifies one Lua identifier after the scanner has ruled out comments,
 * strings and numbers.
 */
function highlightLuaIdentifier(identifier, localVariables, previousChar) {
    if (LUA_KEYWORDS.has(identifier))
        return wrapLuaToken(identifier, 'keyword');

    if (LUA_LITERALS.has(identifier))
        return wrapLuaToken(identifier, 'literal');

    if (LUA_BUILT_INS.has(identifier))
        return wrapLuaToken(identifier, 'built-in');

    if (isLuaConstant(identifier))
        return wrapLuaToken(identifier, 'constant');

    if (previousChar === '.' || previousChar === ':')
        return wrapLuaToken(identifier, 'property');

    if (localVariables.has(identifier))
        return wrapLuaToken(identifier, 'variable');

    return escapeHtml(identifier);
}

function wrapLuaToken(text, tokenClass) {
    return `<span class="lua-token lua-${tokenClass}">${escapeHtml(text)}</span>`;
}

function escapeHtml(text) {
    return text
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
}

function findLineEnd(code, start) {
    const lineEnd = code.indexOf('\n', start);
    return lineEnd < 0 ? code.length : lineEnd;
}

function readQuotedStringEnd(code, start, quote) {
    let index = start + 1;
    while (index < code.length) {
        if (code[index] === '\\') {
            index += 2;
            continue;
        }

        if (code[index] === quote)
            return index + 1;

        index++;
    }

    return code.length;
}

function readLuaLongBracketEnd(code, start) {
    if (code[start] !== '[')
        return null;

    let equalsCount = 0;
    let index = start + 1;
    while (code[index] === '=') {
        equalsCount++;
        index++;
    }

    if (code[index] !== '[')
        return null;

    const close = `]${'='.repeat(equalsCount)}]`;
    const closeIndex = code.indexOf(close, index + 1);
    return {
        end: closeIndex < 0 ? code.length : closeIndex + close.length
    };
}

function isNumberStart(code, index) {
    const char = code[index];
    const next = code[index + 1];
    return isDigit(char) || char === '.' && isDigit(next);
}

/**
 * Reads a permissive Lua number token.
 *
 * The character class covers decimal numbers, hex numbers (`0xff`), exponents
 * (`1e-3`, `0x1p+4`) and separators/dots used while the user is still typing.
 */
function readNumberEnd(code, start) {
    let index = start;
    while (index < code.length && /[0-9a-fA-FxXpPeE+\-_.]/.test(code[index]))
        index++;

    return index;
}

function isIdentifierStart(char) {
    return /[A-Za-z_]/.test(char);
}

function readIdentifierEnd(code, start) {
    let index = start + 1;
    while (index < code.length && /[A-Za-z0-9_]/.test(code[index]))
        index++;

    return index;
}

function isDigit(char) {
    return /[0-9]/.test(char);
}

/**
 * Detects function-call expressions such as `print(`, `table.insert(` or
 * `object:method(` and colors the whole call target as a function.
 */
function readFunctionCallNameEnd(code, start) {
    let index = readIdentifierEnd(code, start);
    let hasMember = false;

    while (code[index] === '.' || code[index] === ':') {
        const memberStart = index + 1;
        if (!isIdentifierStart(code[memberStart]))
            break;

        hasMember = true;
        index = readIdentifierEnd(code, memberStart);
    }

    const nextIndex = skipWhitespace(code, index);
    if (code[nextIndex] === '(' && (hasMember || LUA_BUILT_INS.has(code.slice(start, index))))
        return index;

    return -1;
}

/**
 * Collects names declared after `local` so later identifier tokens can be styled
 * as local variables. The scanner skips comments and strings first to avoid
 * treating text inside them as declarations.
 */
function collectLuaLocalVariables(code) {
    const variables = new Set();
    let index = 0;

    while (index < code.length) {
        const char = code[index];
        const next = code[index + 1];

        if (char === '-' && next === '-') {
            const longCommentEnd = readLuaLongBracketEnd(code, index + 2);
            index = longCommentEnd?.end ?? findLineEnd(code, index);
            continue;
        }

        const longStringEnd = readLuaLongBracketEnd(code, index);
        if (longStringEnd) {
            index = longStringEnd.end;
            continue;
        }

        if (char === '"' || char === "'") {
            index = readQuotedStringEnd(code, index, char);
            continue;
        }

        if (!isIdentifierStart(char)) {
            index++;
            continue;
        }

        const identifierEnd = readIdentifierEnd(code, index);
        const identifier = code.slice(index, identifierEnd);
        if (identifier !== 'local') {
            index = identifierEnd;
            continue;
        }

        index = readLocalDeclarationVariables(code, identifierEnd, variables);
    }

    return variables;
}

function readLocalDeclarationVariables(code, start, variables) {
    let index = skipWhitespace(code, start);

    if (code.startsWith('function', index) && !isIdentifierPart(code[index + 'function'.length])) {
        index = skipWhitespace(code, index + 'function'.length);
        if (isIdentifierStart(code[index])) {
            const functionNameEnd = readIdentifierEnd(code, index);
            variables.add(code.slice(index, functionNameEnd));
            return functionNameEnd;
        }

        return index;
    }

    while (index < code.length) {
        index = skipWhitespace(code, index);
        if (!isIdentifierStart(code[index]))
            break;

        const nameEnd = readIdentifierEnd(code, index);
        variables.add(code.slice(index, nameEnd));
        index = skipWhitespace(code, nameEnd);

        if (code[index] !== ',')
            break;

        index++;
    }

    return index;
}

function findPreviousNonWhitespaceChar(code, start) {
    let index = start - 1;
    while (index >= 0 && /\s/.test(code[index]))
        index--;

    return index < 0 ? '' : code[index];
}

function skipWhitespace(code, start) {
    let index = start;
    while (index < code.length && /\s/.test(code[index]))
        index++;

    return index;
}

function isIdentifierPart(char) {
    return /[A-Za-z0-9_]/.test(char ?? '');
}

function isLuaConstant(identifier) {
    // Constants are a convention here, not Lua syntax: `TRANSLATION_SYSTEM_PROMPT`.
    return identifier.length > 1 && /^[A-Z][A-Z0-9_]*$/.test(identifier);
}
