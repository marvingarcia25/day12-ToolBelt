# day12_ToolBelt

A browser chat assistant where a **local** language model calls your own C# code.

It runs `llama3.2:3b` through [Ollama](https://ollama.com) — fully offline, no API keys, nothing leaves the machine. The model doesn't just reply; it decides when to *act* and calls real backend functions: do some math, check the time, or save and recall notes. You watch each tool call happen live in the UI.

## What it does

You chat. Under the hood, the model is given a set of tools and picks which to call:

| Tool | What it does |
|------|--------------|
| `get_current_time` | Returns the current date/time |
| `calculate` | Evaluates a simple arithmetic expression (arithmetic only, input-whitelisted) |
| `add_note` | Saves a note to an in-memory store |
| `search_notes` | Finds notes matching a query |
| `list_notes` | Lists everything saved |

The notes tools turn the backend into the model's memory:

> **You:** Remember that I parked on level 3.
> **Assistant:** *🔧 add_note("Parked on level 3")* → Saved.
> **You:** Where did I park?
> **Assistant:** *🔧 search_notes("parked")* → You parked on level 3.

## How it works

A single streaming agent loop:

1. The browser POSTs your message; the server opens a Server-Sent Events stream.
2. The server calls Ollama with the full conversation **plus the tool schemas**.
   - Text tokens stream straight to the browser.
   - If the model requests a tool, the backend runs it, streams the result back, appends it to the history, and loops.
   - When a turn finishes with text and no tool call, that's the answer.
3. A max-iteration guard (6) keeps it from looping forever.

Everything is streamed over SSE, so you see the model thinking, calling tools, and answering in real time.

## Stack

- **.NET 8** minimal API (ASP.NET Core)
- **Ollama** running `llama3.2:3b` (must report the `tools` capability)
- Vanilla JS + Tailwind (CDN) front end — single `wwwroot/index.html`, no build step
- xUnit tests

## Running it

**Prerequisites**

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com) running locally, with the model pulled:
  ```
  ollama pull llama3.2:3b
  ```

**Start the app**

```
dotnet run --project ToolBelt.csproj
```

Then open the URL it prints (e.g. `http://localhost:5114`).

Config lives in `appsettings.json`:

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Model": "llama3.2:3b"
}
```

## Tests

```
dotnet test ToolBelt.Tests/ToolBelt.Tests.csproj
```

Covers the calculator's safe-eval guard, the notes store, tool dispatch, and the agent loop itself (with a fake Ollama client that returns a tool call then a final answer).

## A note on the model

`llama3.2:3b` is small, so tool selection isn't perfect — early on it would try to *calculate* "level 3" instead of saving a note. A short system prompt that explains when each tool is for fixed it for normal phrasing. With a 3B model, being explicit about intent matters more than the code.

---

Day 12 of building a small thing every day.
