import { app, BrowserWindow, globalShortcut, ipcMain, desktopCapturer, screen, safeStorage, shell } from "electron";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";
import crypto from "node:crypto";
import http from "node:http";
import { streamChat } from "./providers.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const isDev = process.env.NODE_ENV === "development";
// Named secrets: "anthropic", "openrouter", ...
const KEY_FILE = (name) => path.join(app.getPath("userData"), `${String(name).replace(/[^a-z0-9]/gi, "_")}.key.enc`);

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

// --- IPC: secure API key storage (named) -----------------------------------
ipcMain.handle("save-key", (_e, { name, value }) => {
  if (!safeStorage.isEncryptionAvailable()) return { ok: false, reason: "no-encryption" };
  fs.writeFileSync(KEY_FILE(name), safeStorage.encryptString(value));
  return { ok: true };
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

ipcMain.handle("clear-key", (_e, name) => {
  try { fs.existsSync(KEY_FILE(name)) && fs.unlinkSync(KEY_FILE(name)); } catch {}
  return { ok: true };
});

// --- IPC: workspace persistence --------------------------------------------
const STATE_FILE = () => path.join(app.getPath("userData"), "state.json");

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
      res.writeHead(200, { "content-type": "text/html" });
      res.end("<html><body style='font-family:-apple-system,sans-serif;background:#16120D;color:#F1EADD;padding:56px'><h2>SideShift AI</h2><p>Signed in — you can close this tab.</p></body></html>");
      clearTimeout(timer);
      server.close();
      const gotState = u.searchParams.get("state");
      const gotCode = u.searchParams.get("code");
      if (gotState !== state || !gotCode) reject(new Error("OAuth state/code mismatch"));
      else resolve({ code: gotCode, redirect });
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

ipcMain.handle("google-signin", async (_e, { clientId, clientSecret }) => {
  try { return { ok: true, profile: await googleSignIn(clientId, clientSecret) }; }
  catch (e) { return { ok: false, error: String(e?.message || e) }; }
});

ipcMain.handle("google-signout", () => {
  try { fs.existsSync(GOOG_FILE()) && fs.unlinkSync(GOOG_FILE()); } catch {}
  return { ok: true };
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

app.whenReady().then(() => {
  createOverlay();

  // Toggle region-capture mode in the renderer.
  globalShortcut.register("CommandOrControl+Shift+Space", () => {
    if (overlay) overlay.webContents.send("hotkey:toggle-capture");
  });
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

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createOverlay();
  });
});

app.on("will-quit", () => globalShortcut.unregisterAll());
// Overlay app has no dock/window lifecycle; keep running on macOS.
app.on("window-all-closed", () => { if (process.platform !== "darwin") app.quit(); });
