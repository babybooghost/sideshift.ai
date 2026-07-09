import { app, BrowserWindow, globalShortcut, ipcMain, desktopCapturer, screen, safeStorage, shell, Tray, Menu, nativeImage, systemPreferences, clipboard, Notification } from "electron";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";
import crypto from "node:crypto";
import http from "node:http";
import { execFile } from "node:child_process";
import { streamChat } from "./providers.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const isDev = process.env.NODE_ENV === "development";
// Named secrets: "anthropic", "openrouter", ...
const KEY_FILE = (name) => path.join(app.getPath("userData"), `${String(name).replace(/[^a-z0-9]/gi, "_")}.key.enc`);

let overlay = null;
let tray = null;
let settingsWin = null;

// Settings lives in a real, normal macOS window (traffic lights, Cmd+Tab, app
// activation) — the overlay stays chrome-less and non-intrusive.
function openSettingsWindow(errMsg) {
  if (settingsWin && !settingsWin.isDestroyed()) {
    settingsWin.show();
    settingsWin.focus();
    if (errMsg) settingsWin.webContents.send("settings-error", errMsg);
    return;
  }
  settingsWin = new BrowserWindow({
    width: 560,
    height: 800,
    minWidth: 500,
    minHeight: 540,
    title: "SideShift AI",
    backgroundColor: "#0B0908",
    show: false,
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });
  // Dock icon only while settings is open: as an agent (no-Dock) app the overlay
  // can join other apps' fullscreen Spaces; with a Dock icon macOS refuses.
  if (process.platform === "darwin") { try { app.dock.show(); } catch {} }
  settingsWin.once("ready-to-show", () => { settingsWin.show(); settingsWin.focus(); });
  settingsWin.on("closed", () => {
    settingsWin = null;
    if (process.platform === "darwin") { try { app.dock.hide(); } catch {} }
  });
  const query = errMsg ? { settings: "1", err: errMsg } : { settings: "1" };
  if (isDev) {
    settingsWin.loadURL("http://localhost:5173/?settings=1" + (errMsg ? "&err=" + encodeURIComponent(errMsg) : ""));
  } else {
    settingsWin.loadFile(path.join(__dirname, "..", "dist", "index.html"), { query });
  }
}

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
      // Preload MUST be .cjs — Electron decides a preload's module system by
      // file extension, not package.json "type", so an ESM ".js" fails to load.
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  // Surface renderer warnings/errors (e.g. a bad restore) to the main log.
  overlay.webContents.on("console-message", (_e, level, message) => {
    if (level >= 2) console.log(`[renderer:${level}] ${message}`);
  });

  // Startup healthcheck: the contextBridge API must be present, or capture/
  // streaming/keys are all silently dead.
  overlay.webContents.on("did-finish-load", async () => {
    try {
      const t = await overlay.webContents.executeJavaScript("typeof window.sideshift");
      console.log(`[sideshift] preload bridge: ${t}`);
    } catch {}
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
  // On macOS a denied Screen Recording permission yields either no sources or a
  // blank thumbnail. Detect it and return a structured error so the renderer can
  // point the user at System Settings instead of silently capturing black.
  if (process.platform === "darwin" &&
      typeof systemPreferences.getMediaAccessStatus === "function" &&
      systemPreferences.getMediaAccessStatus("screen") !== "granted") {
    return { ok: false, error: "screen-permission", message: "Screen Recording permission is off. Grant it in System Settings > Privacy & Security > Screen Recording, then reopen SideShift." };
  }
  try {
    const primary = screen.getPrimaryDisplay();
    const { width, height } = primary.size;
    const scale = primary.scaleFactor || 1;
    const sources = await desktopCapturer.getSources({
      types: ["screen"],
      thumbnailSize: { width: Math.round(width * scale), height: Math.round(height * scale) }
    });
    const src = sources.find((s) => s.display_id === String(primary.id)) || sources[0];
    if (!src || src.thumbnail.isEmpty()) {
      return { ok: false, error: "no-capture", message: "Could not read the screen. Check Screen Recording permission and try again." };
    }
    return {
      ok: true,
      dataUrl: src.thumbnail.toDataURL(),
      width: Math.round(width * scale),
      height: Math.round(height * scale),
      scale
    };
  } catch (e) {
    return { ok: false, error: "capture-failed", message: String(e?.message || e) };
  }
});

// --- IPC: secure API key storage (named) -----------------------------------
ipcMain.handle("save-key", (e, { name, value }) => {
  if (!safeStorage.isEncryptionAvailable()) return { ok: false, reason: "no-encryption" };
  try {
    fs.writeFileSync(KEY_FILE(name), safeStorage.encryptString(value));
    // keys saved from the settings window must reach the overlay immediately
    if (overlay && !overlay.isDestroyed() && e.sender !== overlay.webContents)
      overlay.webContents.send("keys-changed");
    return { ok: true };
  } catch (err) {
    // never throw across IPC (would reject unhandled in the renderer)
    return { ok: false, reason: "write-failed", error: String(err?.message || err) };
  }
});

ipcMain.handle("load-key", (_e, name) => {
  try {
    const f = KEY_FILE(name);
    if (!fs.existsSync(f)) return { ok: true, key: null };
    return { ok: true, key: safeStorage.decryptString(fs.readFileSync(f)) };
  } catch (e) {
    return { ok: false, reason: String(e) };
  }
});

ipcMain.handle("clear-key", (e, name) => {
  try { fs.existsSync(KEY_FILE(name)) && fs.unlinkSync(KEY_FILE(name)); } catch {}
  if (overlay && !overlay.isDestroyed() && e.sender !== overlay.webContents)
    overlay.webContents.send("keys-changed");
  return { ok: true };
});

// --- IPC: workspace persistence --------------------------------------------
const STATE_FILE = () => path.join(app.getPath("userData"), "state.json");

// Settings-window IPC: open/close, plus preference sync so the settings window
// never clobbers the overlay's live widget state (it merges prefs only).
ipcMain.on("open-settings-window", (_e, err) => openSettingsWindow(err || undefined));
ipcMain.on("close-settings-window", () => { if (settingsWin && !settingsWin.isDestroyed()) settingsWin.close(); });

ipcMain.handle("save-prefs", (_e, prefs) => {
  try {
    let cur = {};
    try { cur = JSON.parse(fs.readFileSync(STATE_FILE(), "utf8")) || {}; } catch {}
    const merged = { ...cur, ...prefs };
    fs.writeFileSync(STATE_FILE(), JSON.stringify(merged));
    if (overlay && !overlay.isDestroyed()) overlay.webContents.send("prefs-changed", prefs);
    return { ok: true };
  } catch (e) { return { ok: false, error: String(e?.message || e) }; }
});

ipcMain.handle("save-state", (_e, state) => {
  try { fs.writeFileSync(STATE_FILE(), JSON.stringify(state)); return { ok: true }; }
  catch (e) { return { ok: false, reason: String(e) }; }
});

ipcMain.handle("load-state", () => {
  try {
    if (!fs.existsSync(STATE_FILE())) return null;
    return JSON.parse(fs.readFileSync(STATE_FILE(), "utf8"));
  } catch { return null; }
});

// Open the macOS Screen Recording privacy pane (first-run onboarding).
ipcMain.handle("open-screen-privacy", () => {
  if (process.platform === "darwin") {
    shell.openExternal("x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");
  }
  return { ok: true };
});

// --- Google Sign-In (OAuth 2.0 Authorization Code + PKCE, RFC 8252) ---------
// Public "Desktop app" client: system browser + loopback redirect + PKCE. The
// client_secret is a non-secret app identifier (Google's token endpoint still
// wants it for Desktop clients); PKCE is what actually secures the flow.
const GOOG_FILE = () => path.join(app.getPath("userData"), "google.refresh.enc");
const b64url = (buf) => buf.toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

// Built-in OAuth client shipped with the app (a Desktop-app client id is a public
// identifier, not a secret; PKCE secures the flow). Users never paste anything.
let BUILTIN_OAUTH = { google: { clientId: "", clientSecret: "" } };
try {
  const cfg = JSON.parse(fs.readFileSync(path.join(__dirname, "oauth-config.json"), "utf8"));
  if (cfg && cfg.google) BUILTIN_OAUTH.google = { ...BUILTIN_OAUTH.google, ...cfg.google };
} catch {}

async function googleSignIn(clientId, clientSecret) {
  const verifier = b64url(crypto.randomBytes(48));
  const challenge = b64url(crypto.createHash("sha256").update(verifier).digest());
  const state = b64url(crypto.randomBytes(16));

  const { code, redirect } = await new Promise((resolve, reject) => {
    let redirect = null;
    let server;
    const timer = setTimeout(() => { try { server.close(); } catch {} reject(new Error("Sign-in timed out")); }, 180000);
    server = http.createServer((req, res) => {
      const u = new URL(req.url, redirect);
      if (u.pathname !== "/callback") { res.writeHead(404); res.end(); return; }
      clearTimeout(timer);
      const gotState = u.searchParams.get("state");
      const gotCode = u.searchParams.get("code");
      const gotErr = u.searchParams.get("error");
      const ok = !gotErr && !!gotCode && gotState === state;
      // Show the page that matches what actually happened (was always "Signed in",
      // even when the user denied consent). charset is required: without it the
      // browser guesses latin-1 and punctuation renders as mojibake.
      const title = ok ? "You're signed in." : "Sign-in cancelled.";
      const sub = ok ? "Head back to SideShift. This tab can be closed." : "Nothing was changed. This tab can be closed.";
      res.writeHead(200, { "content-type": "text/html; charset=utf-8" });
      res.end(`<!doctype html><html><head><meta charset="utf-8"><title>SideShift AI</title></head>
<body style="margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#0B0908;font-family:-apple-system,BlinkMacSystemFont,sans-serif">
<div style="text-align:center;padding:56px 44px;background:#16120D;border:1px solid #2E271C;border-radius:18px;box-shadow:0 40px 90px rgba(0,0,0,.55)">
<svg viewBox="0 0 1024 1024" width="64" height="64" style="filter:drop-shadow(0 0 18px rgba(228,87,30,.45))"><defs><linearGradient id="g" x1="330" y1="270" x2="700" y2="770" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#F8C64C"/><stop offset="1" stop-color="#E68A26"/></linearGradient><linearGradient id="a" x1="200" y1="730" x2="850" y2="235" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#D8481A"/><stop offset="1" stop-color="#F5732E"/></linearGradient></defs><path d="M 208 700 C 300 664 298 590 380 560 C 470 527 470 470 542 452 C 636 428 690 356 812 258" fill="none" stroke="url(#a)" stroke-width="46" stroke-linecap="round" stroke-linejoin="round"/><path d="M 742 262 L 812 258 L 806 330" fill="none" stroke="url(#a)" stroke-width="46" stroke-linecap="round" stroke-linejoin="round"/><g transform="translate(96 0) skewX(-11)"><path d="M 688 374 C 674 308 588 294 518 320 C 428 354 428 442 522 486 C 620 530 646 622 566 678 C 494 728 392 716 348 658" fill="none" stroke="url(#g)" stroke-width="100" stroke-linecap="round" stroke-linejoin="round"/></g><path d="M 560 442 C 636 414 690 356 812 258" fill="none" stroke="url(#a)" stroke-width="46" stroke-linecap="round" stroke-linejoin="round"/></svg>
<h1 style="color:#F1EADD;font-size:22px;letter-spacing:-.01em;margin:18px 0 6px">${title}</h1>
<p style="color:#9C8F7C;font-size:14.5px;margin:0">${sub}</p>
</div></body></html>`);
      server.close();
      if (ok) resolve({ code: gotCode, redirect });
      else reject(new Error(
        gotErr === "access_denied" ? "Sign-in was cancelled."
        : gotErr ? "Google returned an error: " + gotErr
        : "OAuth state mismatch — please try again."));
    });
    server.on("error", reject);
    server.listen(0, "127.0.0.1", () => {
      redirect = `http://127.0.0.1:${server.address().port}/callback`;
      const auth = new URL("https://accounts.google.com/o/oauth2/v2/auth");
      auth.searchParams.set("client_id", clientId);
      auth.searchParams.set("redirect_uri", redirect);
      auth.searchParams.set("response_type", "code");
      auth.searchParams.set("scope", "openid email profile");
      auth.searchParams.set("code_challenge", challenge);
      auth.searchParams.set("code_challenge_method", "S256");
      auth.searchParams.set("state", state);
      auth.searchParams.set("access_type", "offline");
      shell.openExternal(auth.toString());
    });
  });

  const tokRes = await fetch("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "content-type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      code, client_id: clientId, client_secret: clientSecret, code_verifier: verifier,
      grant_type: "authorization_code", redirect_uri: redirect
    })
  });
  const tok = await tokRes.json();
  if (!tokRes.ok) throw new Error("Token exchange failed: " + (tok.error_description || tok.error || tokRes.status));

  const uiRes = await fetch("https://openidconnect.googleapis.com/v1/userinfo", {
    headers: { authorization: `Bearer ${tok.access_token}` }
  });
  const profile = await uiRes.json();
  if (tok.refresh_token) {
    try { fs.writeFileSync(GOOG_FILE(), safeStorage.encryptString(tok.refresh_token)); } catch {}
  }
  return { email: profile.email, name: profile.name, picture: profile.picture };
}

ipcMain.handle("google-signin", async (_e, args) => {
  const clientId = (args && args.clientId) || BUILTIN_OAUTH.google.clientId;
  const clientSecret = (args && args.clientSecret) || BUILTIN_OAUTH.google.clientSecret;
  if (!clientId)
    return { ok: false, error: "Google sign-in isn't provisioned in this build yet. It arrives with the next release." };
  try { return { ok: true, profile: await googleSignIn(clientId, clientSecret) }; }
  catch (e) { return { ok: false, error: String(e?.message || e) }; }
});

// Sign in with Apple needs a Services ID + key from Apple's paid developer
// program, which also unlocks notarization. Honest stub until that lands.
ipcMain.handle("apple-signin", async () => (
  { ok: false, error: "Apple sign-in arrives with the notarized build (it needs Apple's developer program)." }
));

ipcMain.handle("google-signout", () => {
  try { fs.existsSync(GOOG_FILE()) && fs.unlinkSync(GOOG_FILE()); } catch {}
  return { ok: true };
});

// Validate an API key by hitting a cheap authenticated endpoint. ok=false means
// we could not reach the network (offline); valid reflects the auth result.
ipcMain.handle("validate-key", async (_e, { provider, key }) => {
  try {
    const res = provider === "openrouter"
      ? await fetch("https://openrouter.ai/api/v1/key", { headers: { authorization: `Bearer ${key}` } })
      : await fetch("https://api.anthropic.com/v1/models", { headers: { "x-api-key": key, "anthropic-version": "2023-06-01" } });
    // Only a real auth failure (401/403) means the key is bad. A 429 rate-limit or a
    // 5xx must NOT be reported as "rejected" — treat those (and 2xx) as acceptable so a
    // valid key is never thrown away over a transient provider hiccup.
    return { ok: true, valid: !(res.status === 401 || res.status === 403), status: res.status };
  } catch (e) {
    return { ok: false, valid: false, status: 0, error: String(e?.message || e) };
  }
});

// --- IPC: chat streaming (provider-agnostic) -------------------------------
// Each request gets an id; chunks flow back as "stream:<id>" events.
ipcMain.on("chat-stream", async (event, req) => {
  const send = (payload) => {
    if (!event.sender.isDestroyed()) event.sender.send(`stream:${req.id}`, payload);
  };
  try {
    for await (const delta of streamChat(req)) send({ type: "delta", text: delta });
    send({ type: "done" });
  } catch (e) {
    send({ type: "error", message: String(e?.message || e) });
  }
});

// Grab the highlighted text from the frontmost app: simulate Cmd+C via System
// Events (needs Accessibility), read the clipboard, then restore it. This is the
// Cluely-style "read what I selected" flow — no reverse engineering, plain OS APIs.
function grabSelection() {
  if (process.platform === "darwin" && !systemPreferences.isTrustedAccessibilityClient(true)) {
    openSettingsWindow("To read highlighted text, allow SideShift under System Settings > Privacy & Security > Accessibility, then press Cmd+Shift+S again.");
    return;
  }
  const prev = clipboard.readText();
  clipboard.clear();
  execFile("osascript", ["-e", 'tell application "System Events" to keystroke "c" using {command down}'], (err) => {
    setTimeout(() => {
      const sel = clipboard.readText();
      if (prev) clipboard.writeText(prev); else clipboard.clear();
      if (err) {
        openSettingsWindow("Could not read the selection: " + String(err.message || err));
      } else if (sel && sel.trim()) {
        if (overlay) { overlay.show(); overlay.webContents.send("selection-captured", sel); }
      } else {
        new Notification({ title: "SideShift AI", body: "No highlighted text found. Select some text, then press Cmd+Shift+S." }).show();
      }
    }, 320);
  });
}

app.whenReady().then(() => {
  // Agent app (no Dock icon): required for the overlay to float over OTHER apps'
  // fullscreen Spaces on macOS. The tray is the permanent home; the Dock icon
  // reappears while the settings window is open.
  if (process.platform === "darwin") { try { app.dock.hide(); } catch {} }

  createOverlay();

  // Toggle region-capture mode in the renderer.
  globalShortcut.register("CommandOrControl+Shift+Space", () => {
    if (overlay) overlay.webContents.send("hotkey:toggle-capture");
  });
  // Ask about the current selection (any app).
  globalShortcut.register("CommandOrControl+Shift+S", grabSelection);
  // Hide/show the whole overlay (Cluely-style quick hide).
  const toggleHide = () => {
    if (!overlay) return;
    overlay.isVisible() ? overlay.hide() : overlay.show();
  };
  globalShortcut.register("CommandOrControl+\\", toggleHide);
  globalShortcut.register("CommandOrControl+Shift+H", toggleHide);

  // Move the focused widget with the keyboard. Cmd+Alt+Arrow (not bare Cmd+Arrow,
  // which the OS/browsers use for navigation) so we never hijack common shortcuts.
  const STEP = 48;
  const nudge = (dx, dy) => {
    if (!overlay) return;
    if (!overlay.isVisible()) overlay.show();
    overlay.webContents.send("hotkey:nudge", dx, dy);
  };
  globalShortcut.register("CommandOrControl+Alt+Left", () => nudge(-STEP, 0));
  globalShortcut.register("CommandOrControl+Alt+Right", () => nudge(STEP, 0));
  globalShortcut.register("CommandOrControl+Alt+Up", () => nudge(0, -STEP));
  globalShortcut.register("CommandOrControl+Alt+Down", () => nudge(0, STEP));

  const showOverlay = () => { if (overlay) { overlay.show(); overlay.setAlwaysOnTop(true, "screen-saver"); } };
  const openSettings = () => openSettingsWindow();
  const capture = () => { showOverlay(); if (overlay) overlay.webContents.send("hotkey:toggle-capture"); };

  // Tray (menu bar / system tray) — the always-visible entry point so the app
  // is never an invisible ghost. Works on macOS and Windows.
  try {
    const timg = nativeImage.createFromPath(path.join(__dirname, "trayTemplate.png"));
    timg.setTemplateImage(true);
    tray = new Tray(timg);
    tray.setToolTip("SideShift AI");
    tray.setContextMenu(Menu.buildFromTemplate([
      { label: "Capture region", accelerator: "CommandOrControl+Shift+Space", click: capture },
      { label: "Ask about highlighted text", accelerator: "CommandOrControl+Shift+S", click: grabSelection },
      { label: "Show / hide overlay", accelerator: "CommandOrControl+\\", click: toggleHide },
      { label: "Settings…", click: openSettings },
      { type: "separator" },
      { label: "About SideShift AI", click: () => shell.openExternal("https://github.com/babybooghost/sideshift.ai") },
      { label: "Quit SideShift", accelerator: "CommandOrControl+Q", click: () => app.quit() }
    ]));
    tray.on("click", showOverlay);
  } catch (e) { console.log("[sideshift] tray init failed:", String(e)); }

  // Application menu — gives a real menu bar and, crucially, Edit roles so
  // copy/paste/select-all work inside the settings inputs.
  Menu.setApplicationMenu(Menu.buildFromTemplate([
    { label: app.name, submenu: [
      { role: "about" }, { type: "separator" },
      { label: "Settings…", accelerator: "CommandOrControl+,", click: openSettings },
      { label: "Capture", accelerator: "CommandOrControl+Shift+Space", click: capture },
      { type: "separator" },
      { role: "hide" }, { role: "quit" }
    ]},
    { label: "Edit", submenu: [
      { role: "undo" }, { role: "redo" }, { type: "separator" },
      { role: "cut" }, { role: "copy" }, { role: "paste" }, { role: "selectAll" }
    ]}
  ]));

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createOverlay();
    else showOverlay();
  });
});

app.on("will-quit", () => globalShortcut.unregisterAll());
// Overlay app has no dock/window lifecycle; keep running on macOS.
app.on("window-all-closed", () => { if (process.platform !== "darwin") app.quit(); });
