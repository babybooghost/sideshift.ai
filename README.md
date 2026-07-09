# SideShift AI

Screen-analysis AI overlay. Highlight **any** text or code on your screen — in Claude, ChatGPT, an IDE, a PDF, anything — and open focused, draggable side-chats about it without touching the underlying app.

- **No reverse engineering.** No session cookies, no private endpoints, no header spoofing.
- **Screen analysis only.** Captures the region you draw, sends the pixels to the official Anthropic API with **your own** key.
- **App-agnostic.** It reads the screen, so it works over any app or website.

## Stack

- Electron — transparent, always-on-top, click-through overlay + global hotkey + `desktopCapturer`.
- F# → JS via **Fable 5**, UI in **Elmish / Feliz**.
- Vite bundles the renderer.
- API key stored encrypted locally via Electron `safeStorage`.

## Features

| Feature | What it does |
|---|---|
| **Capture** | `⌘⇧Space` (or the Capture button) → drag a box over any text/code. |
| **Ask / ELI5 / Verify / Diff** | Action bar on the selection. ELI5 = 2-sentence definition; Verify = skeptical fact-check + confidence score; Diff = answers code edits as a minimal git-style diff. |
| **Multi-widget** | Many side-chats at once; click one to bring it to the front. |
| **Merge or Discard** | Closing a widget lets you discard the tangent or merge a summary into shared context that later side-chats inherit. |
| **Margin Minimap** | Minimized widgets dock as colored nodes on the right edge; click to reopen. |
| **Quick-copy** | Copy button on code/diff replies. |

## Run

```bash
npm install
npm run build     # dotnet fable src -> dist-fable, then vite build
npm start         # launch the Electron overlay
```

Dev (hot reload):

```bash
npm run dev
```

Hotkeys: `⌘⇧Space` capture · `⌘⇧H` show/hide overlay · `Esc` cancel capture.

## Packaging & distribution

Installers are built with **electron-builder** (config in [electron-builder.yml](electron-builder.yml)). Targets: macOS `.dmg` (arm64 + x64), Windows `.exe` NSIS installer (x64). No Linux yet.

Local builds:

```bash
npm run dist:mac    # -> release/SideShift AI-<ver>-arm64.dmg  (+ x64)
npm run dist:win    # -> release/SideShift AI-<ver>-setup.exe   (needs a Windows host)
npm run dist        # both (each target needs its native OS)
```

Windows installers can't be built on macOS, so releases go through CI. Push a tag and GitHub Actions ([release.yml](.github/workflows/release.yml)) builds both on native runners and attaches them to a GitHub Release:

```bash
git tag v0.1.0 && git push origin v0.1.0
```

The app icon is the SideShift "S" mark ([build/icon.svg](build/icon.svg)), rendered to `build/icon.png` with `npm run icon` (Chromium rasterizer, no image deps); electron-builder converts that to `.icns`/`.ico`.

### Beta testers — opening an unsigned build

These betas are **unsigned** (no paid Apple/Windows cert yet), so the OS will warn:

- **macOS:** first launch is blocked ("unidentified developer"). On macOS 15 Sequoia the right-click→Open shortcut is gone — open the app once, then go to **System Settings → Privacy & Security** and click **Open Anyway**. If it still reports the app is "damaged" (quarantine on a downloaded dmg), run `xattr -cr "/Applications/SideShift AI.app"`. Then grant **Screen Recording** under Privacy & Security → *Screen & System Audio Recording* — required for capture. Because ad-hoc signatures change on every build, you must **re-grant Screen Recording after each beta update**.
- **Windows:** SmartScreen shows "Windows protected your PC" → **More info** → **Run anyway**.

## Notes / honest limits

- The API version calls `api.anthropic.com` with the user's key — it does **not** post into the AI app you're looking at, so "Merge" keeps context inside SideShift rather than injecting it into that app's server-side thread.
- Region OCR is done by the vision model itself (the cropped image is sent directly), so accuracy tracks the model.
