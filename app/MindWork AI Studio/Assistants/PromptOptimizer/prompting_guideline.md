# 1 – Be Clear and Direct

LLMs respond best to clear, explicit instructions. Being specific about your desired output improves results. If you want high-quality work, ask for it directly rather than expecting the model to guess.

Think of the LLM as a skilled new employee: They do not know your specific workflows yet. The more precisely you explain what you want, the better the result.

**Golden Rule:** If a colleague would be confused by your prompt without extra context, the LLM will be too.

**Less Effective:**
```text
Create an analytics dashboard
```

**More Effective:**
```text
Create an analytics dashboard. Include relevant features and interactions. Go beyond the basics to create a fully-featured implementation.
```

# 2 – Add Examples and Context to Improve Performance

Providing examples, context, or the reason behind your instructions helps the model understand your goals.

**Less Effective:**
```text
NEVER use ellipses
```

**More Effective:**
```text
Your response will be read aloud by a text-to-speech engine, so never use ellipses since the engine will not know how to pronounce them.
```

The model can generalize from the explanation.

# 3 – Use Sequential Steps

When the order of tasks matters, provide instructions as a numbered list.

**Example:**
```text
1. Analyze the provided text for key themes.
2. Extract the top 5 most frequent terms.
3. Format the output as a table with columns: Term, Frequency, Context.
```

# 4 – Structure Prompts with Markers

Headings (e.g., `#` or `###`) or quotation marks (`"""`) help the model parse complex prompts, especially when mixing instructions, context, and data.

**Less Effective:**
```text
{text input here}

Summarize the text above as a bullet point list of the most important points.
```

**More Effective:**
```text
# Text: 
"""{text input here}"""

# Task: 
Summarize the text above as a bullet point list of the most important points.
```

# 5 – Give the LLM a Role

Setting a role in your prompt focuses the LLM's behavior and tone. Even a single sentence makes a difference.

**Example:**
```text
You are a helpful coding assistant specializing in Python.
```
```text
You are a senior marketing expert with 10 years of experience in the aerospace industry.
```

# 6 – Prompt Language

LLMs are primarily trained on English text. They generally perform best with prompts written in **English**, especially for complex tasks.

*   **Recommendation:** Write your prompts in English.
*   **If needed:** You can ask the LLM to respond in your native language (e.g., "Answer in German").
*   **Note:** This is especially important for smaller models, which may have limited multilingual capabilities.

