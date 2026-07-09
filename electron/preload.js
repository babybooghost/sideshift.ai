import { contextBridge, ipcRenderer } from "electron";

let streamSeq = 0;

contextBridge.exposeInMainWorld("sideshift", {
  // Pass-through control so only widgets are clickable.
  setIgnoreMouse: (ignore) => ipcRenderer.send("set-ignore-mouse", ignore),

  // Screen capture (full display PNG; renderer crops the region).
  captureScreen: () => ipcRenderer.invoke("capture-screen"),

  // Secure key storage.
  saveKey: (k) => ipcRenderer.invoke("save-key", k),
  loadKey: () => ipcRenderer.invoke("load-key"),
  clearKey: () => ipcRenderer.invoke("clear-key"),

  // Global-hotkey events into the Elmish loop.
  onToggleCapture: (cb) => ipcRenderer.on("hotkey:toggle-capture", () => cb()),

  // Streaming: returns an unsubscribe fn. onEvent gets {type,text|message}.
  streamAnthropic: (req, onEvent) => {
    const id = `${Date.now()}-${streamSeq++}`;
    const channel = `stream:${id}`;
    const handler = (_e, payload) => onEvent(payload);
    ipcRenderer.on(channel, handler);
    ipcRenderer.send("anthropic-stream", { ...req, id });
    return () => ipcRenderer.removeListener(channel, handler);
  }
});
