import { app, BrowserWindow, globalShortcut, ipcMain, desktopCapturer, screen, safeStorage } from "electron";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";
import { streamAnthropic } from "./anthropic.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const isDev = process.env.NODE_ENV === "development";
const KEY_FILE = () => path.join(app.getPath("userData"), "anthropic.key.enc");

let overlay = null;

function createOverlay() {
  const primary = screen.getPrimaryDisplay();
  const { width, height } = primary.workAreaSize;

  overlay = new BrowserWindow({
    width,
    height,
    x: 0,
    y: 0,
    transparent: true,
    frame: false,
    hasShadow: false,
    resizable: false,
    movable: false,
    skipTaskbar: true,
    alwaysOnTop: true,
    fullscreenable: false,
    // Overlay must float above normal windows without stealing focus from target app.
    focusable: true,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  overlay.setAlwaysOnTop(true, "screen-saver");
  overlay.setVisibleOnAllWorkspaces(true, { visibleOnFullScreen: true });
  // Click-through by default; renderer re-enables hit-testing over widgets.
  overlay.setIgnoreMouseEvents(true, { forward: true });

  if (isDev) {
    overlay.loadURL("http://localhost:5173");
  } else {
    overlay.loadFile(path.join(__dirname, "..", "dist", "index.html"));
  }
}

// --- IPC: overlay mouse pass-through toggling -------------------------------
// Renderer calls this when the pointer enters/leaves an interactive widget so
// the rest of the screen stays clickable in the underlying app.
ipcMain.on("set-ignore-mouse", (_e, ignore) => {
  if (overlay) overlay.setIgnoreMouseEvents(ignore, { forward: true });
});

// --- IPC: full-screen capture ----------------------------------------------
// Returns a PNG data URL of the primary display at native resolution.
// The renderer crops the user-selected region locally, so we never ship more
// pixels than needed and nothing leaves the machine except the final crop.
ipcMain.handle("capture-screen", async () => {
  const primary = screen.getPrimaryDisplay();
  const { width, height } = primary.size;
  const scale = primary.scaleFactor || 1;
  const sources = await desktopCapturer.getSources({
    types: ["screen"],
    thumbnailSize: { width: Math.round(width * scale), height: Math.round(height * scale) }
  });
  const src = sources.find((s) => s.display_id === String(primary.id)) || sources[0];
  return {
    dataUrl: src.thumbnail.toDataURL(),
    width: Math.round(width * scale),
    height: Math.round(height * scale),
    scale
  };
});

// --- IPC: secure API key storage -------------------------------------------
ipcMain.handle("save-key", (_e, plain) => {
  if (!safeStorage.isEncryptionAvailable()) return { ok: false, reason: "no-encryption" };
  const enc = safeStorage.encryptString(plain);
  fs.writeFileSync(KEY_FILE(), enc);
  return { ok: true };
});

ipcMain.handle("load-key", () => {
  try {
    if (!fs.existsSync(KEY_FILE())) return { ok: true, key: null };
    const enc = fs.readFileSync(KEY_FILE());
    return { ok: true, key: safeStorage.decryptString(enc) };
  } catch (e) {
    return { ok: false, reason: String(e) };
  }
});

ipcMain.handle("clear-key", () => {
  try { fs.existsSync(KEY_FILE()) && fs.unlinkSync(KEY_FILE()); } catch {}
  return { ok: true };
});

// --- IPC: Anthropic streaming ----------------------------------------------
// Each request gets an id; chunks flow back as "stream:<id>" events.
ipcMain.on("anthropic-stream", async (event, { id, apiKey, model, system, messages, maxTokens }) => {
  const send = (payload) => {
    if (!event.sender.isDestroyed()) event.sender.send(`stream:${id}`, payload);
  };
  try {
    for await (const delta of streamAnthropic({ apiKey, model, system, messages, maxTokens })) {
      send({ type: "delta", text: delta });
    }
    send({ type: "done" });
  } catch (e) {
    send({ type: "error", message: String(e?.message || e) });
  }
});

app.whenReady().then(() => {
  createOverlay();

  // Toggle region-capture mode in the renderer.
  globalShortcut.register("CommandOrControl+Shift+Space", () => {
    if (overlay) overlay.webContents.send("hotkey:toggle-capture");
  });
  // Hide/show the whole overlay.
  globalShortcut.register("CommandOrControl+Shift+H", () => {
    if (!overlay) return;
    overlay.isVisible() ? overlay.hide() : overlay.show();
  });

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createOverlay();
  });
});

app.on("will-quit", () => globalShortcut.unregisterAll());
// Overlay app has no dock/window lifecycle; keep running on macOS.
app.on("window-all-closed", () => { if (process.platform !== "darwin") app.quit(); });
