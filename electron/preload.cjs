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
