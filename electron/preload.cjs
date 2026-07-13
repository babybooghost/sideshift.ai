// CommonJS preload. Electron picks a preload's module system by FILE EXTENSION,
// not package.json "type" — an ESM ".js" preload silently fails to load. ".cjs"
// is unambiguously CommonJS and works with contextIsolation + sandbox alike.
const { contextBridge, ipcRenderer } = require("electron");

let streamSeq = 0;

contextBridge.exposeInMainWorld("sideshift", {
  // Pass-through control so only widgets are clickable.
  setIgnoreMouse: (ignore) => ipcRenderer.send("set-ignore-mouse", ignore),

  // Screen capture (full display PNG; renderer crops the region).
  captureScreen: () => ipcRenderer.invoke("capture-screen"),

  // Named secure key storage ("anthropic", "openrouter").
  saveKey: (name, value) => ipcRenderer.invoke("save-key", { name, value }),
  loadKey: (name) => ipcRenderer.invoke("load-key", name),
  clearKey: (name) => ipcRenderer.invoke("clear-key", name),

  // Persist/restore the workspace (open widgets + merged context) across restarts.
  saveState: (state) => ipcRenderer.invoke("save-state", state),
  loadState: () => ipcRenderer.invoke("load-state"),

  // Global-hotkey events into the Elmish loop.
  onToggleCapture: (cb) => ipcRenderer.on("hotkey:toggle-capture", () => cb()),
  onSelection: (cb) => ipcRenderer.on("selection-captured", (_e, text) => cb(text)),
  onToast: (cb) => ipcRenderer.on("toast", (_e, msg) => cb(msg)),
  onNudge: (cb) => ipcRenderer.on("hotkey:nudge", (_e, dx, dy) => cb(dx, dy)),
  onOpenSettings: (cb) => ipcRenderer.on("menu:open-settings", () => cb()),
  openScreenPrivacy: () => ipcRenderer.invoke("open-screen-privacy"),

  // Settings lives in its own native window; the overlay syncs prefs/keys live.
  openSettingsWindow: (err) => ipcRenderer.send("open-settings-window", err || null),
  closeSettingsWindow: () => ipcRenderer.send("close-settings-window"),
  savePrefs: (prefs) => ipcRenderer.invoke("save-prefs", prefs),
  onPrefsChanged: (cb) => ipcRenderer.on("prefs-changed", (_e, p) => cb(p)),
  onKeysChanged: (cb) => ipcRenderer.on("keys-changed", () => cb()),
  onSettingsError: (cb) => ipcRenderer.on("settings-error", (_e, m) => cb(m)),

  // Validate an API key against the provider (real network check). -> {ok, valid, status}
  validateKey: (provider, key) => ipcRenderer.invoke("validate-key", { provider, key }),

  // Sign-in (PKCE loopback flow runs in main; built-in client, nothing to paste).
  googleSignIn: () => ipcRenderer.invoke("google-signin", {}),
  appleSignIn: () => ipcRenderer.invoke("apple-signin"),
  googleSignOut: () => ipcRenderer.invoke("google-signout"),

  // Provider-agnostic streaming. `req` = {provider, apiKey, model, system,
  // history:[{role,text}], userText, imageDataUrl, maxTokens}.
  // Returns an unsubscribe fn; onEvent gets {type, text|message}.
  streamChat: (req, onEvent) => {
    const id = `${Date.now()}-${streamSeq++}`;
    const channel = `stream:${id}`;
    const handler = (_e, payload) => onEvent(payload);
    ipcRenderer.on(channel, handler);
    ipcRenderer.send("chat-stream", { ...req, id });
    return () => ipcRenderer.removeListener(channel, handler);
  }
});
