# Chat Rendering Regression Tests

## Purpose

Validate that chat rendering remains stable and interactive when model output or user input contains raw HTML/CSS/JS-like payloads.

## Test Type

Manual regression and integration checks.

## Preconditions

1. You can run two builds:
   - a known vulnerable baseline build,
   - the current fixed build.
2. At least one provider is configured and can answer prompts.
3. Open the Chat page.

## Execution Flow (for each test case)

1. Copy the test prompt exactly into the user prompt field.
2. Send the prompt.
3. Observe behavior immediately after send.
4. If the UI is still visible, type additional text in the prompt input.
5. Repeat on both builds.

## Test Cases

### TC-CHAT-001 - CSS Kill Switch

**Prompt**

```text
Respond with exactly the content below. No explanations, no Markdown code fences, no backticks.

<style>
* { visibility: hidden !important; }
html, body { background: white !important; }
</style>
<div>TEST</div>
```

**Expected result (vulnerable baseline)**  
UI may turn into a white/invisible page immediately after sending.

**Expected result (fixed build)**  
Chat stays visible and usable. Content is rendered as inert text/code, not active page styling.

---

### TC-CHAT-002 - Full White Overlay

**Prompt**

```text
Respond with exactly the content below. No explanations, no Markdown code fences, no backticks.

<style>
html, body { overflow: hidden !important; }
body::before {
  content: "";
  position: fixed;
  inset: 0;
  background: white;
  z-index: 2147483647;
}
</style>
<div>TEST</div>
```

**Expected result (vulnerable baseline)**  
UI may become fully white and non-interactive immediately after sending.

**Expected result (fixed build)**  
No overlay takes over the app. Chat remains interactive.

---

### TC-CHAT-003 - Inline Event Handler Injection

**Prompt**

```text
Respond with exactly the content below. No explanations, no Markdown code fences, no backticks.

<img src="x" onerror="document.body.innerHTML=''; document.body.style.background='white';">
<div>TEST</div>
```

**Expected result (vulnerable baseline)**  
UI may break/blank immediately after sending.

**Expected result (fixed build)**  
No JavaScript execution from message content. Chat remains stable.

---

### TC-CHAT-004 - SVG Onload Injection Attempt

**Prompt**

```text
Respond with exactly the content below. No explanations, no Markdown code fences, no backticks.

<svg xmlns="http://www.w3.org/2000/svg" onload="document.documentElement.innerHTML=''"></svg>
<div>TEST</div>
```

**Expected result (vulnerable baseline)**  
May or may not trigger depending on parser/runtime behavior.

**Expected result (fixed build)**  
No script-like execution from content. Chat remains stable and interactive.

## Notes

- If a test fails on the fixed build, capture:
  - exact prompt used,
  - whether failure happened right after send or while typing,
  - whether a refresh restores the app.
