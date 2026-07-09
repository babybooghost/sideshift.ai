# SideShift AI

Do not include AI attribution or "Co-Authored-By" lines in commit messages or PR descriptions.

## What this is

Desktop overlay app (Electron) that analyzes whatever is **on screen** — text or code, in any AI website/app — and lets the user open focused side-chats about a highlighted region without touching the underlying app.

- **No reverse engineering.** No session cookies, no private endpoints, no header spoofing.
- **Screen analysis only.** Capture a screen region -> send pixels (and OCR text) to the official Anthropic API using the user's own API key.
- **App-agnostic.** Works over Claude, ChatGPT, Gemini, IDEs, PDFs — anything on the display.

## Stack

- Electron (main process: transparent always-on-top overlay, global hotkey, screen capture).
- F# compiled to JS via Fable 5.
- Elmish / Feliz for state + React rendering.
- Vite bundles the renderer.
- User's Anthropic API key stored locally via Electron `safeStorage`.

## Architecture

```
electron/main.js      Overlay window, global shortcut, desktopCapturer, IPC
electron/preload.js   contextBridge: capture / stream / secure key storage
electron/anthropic.js Official Anthropic API streaming (SSE)
src/*.fs              Fable/Elmish renderer (Types/State/Api/View/Main)
```

## Conventions

- Strict F#. Elmish message-update loop drives an array of active side-widgets.
- No AI attribution anywhere in git history or docs (see top of file).
