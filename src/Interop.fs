module SideShift.Interop

open Fable.Core
open Fable.Core.JsInterop

// Bridge exposed by electron/preload.js on window.sideshift.
[<Emit("window.sideshift")>]
let private bridge : obj = jsNative

let captureScreen () : JS.Promise<obj> = bridge?captureScreen ()
let saveKey (name: string) (value: string) : JS.Promise<obj> = bridge?saveKey (name, value)
let loadKey (name: string) : JS.Promise<obj> = bridge?loadKey (name)
let clearKey (name: string) : JS.Promise<obj> = bridge?clearKey (name)

/// Set overlay pass-through. Routed through installHitTest's dedup cache (window.__ssSetIgnore)
/// when available so direct callers (capture on/off, dismiss) stay in sync with the global
/// hit-test — otherwise the cache and the real OS state desync and the overlay can strand
/// click-through over visible UI (or capture over empty screen).
[<Emit("(window.__ssSetIgnore ? window.__ssSetIgnore($0) : window.sideshift.setIgnoreMouse($0))")>]
let setIgnoreMouse (b: bool) : unit = jsNative
/// True when this renderer runs inside the dedicated Settings window
/// (real macOS window with traffic lights) rather than the overlay.
[<Emit("new URLSearchParams(window.location.search).has('settings')")>]
let isSettingsWindow : bool = jsNative

/// Error message passed to the Settings window on open (e.g. capture failure), or "".
[<Emit("(new URLSearchParams(window.location.search).get('err') || '')")>]
let settingsErrParam : string = jsNative

let openSettingsWindow (err: string) : unit = bridge?openSettingsWindow (err)
let closeSettingsWindow () : unit = bridge?closeSettingsWindow ()
let savePrefs (prefs: obj) : JS.Promise<obj> = bridge?savePrefs (prefs)
let onPrefsChanged (cb: obj -> unit) : unit = bridge?onPrefsChanged (cb)
let onKeysChanged (cb: unit -> unit) : unit = bridge?onKeysChanged (cb)
let onSettingsError (cb: string -> unit) : unit = bridge?onSettingsError (cb)

[<Emit("document.title = $0")>]
let setTitle (t: string) : unit = jsNative

let onSelection (cb: string -> unit) : unit = bridge?onSelection (cb)
let onToast (cb: string -> unit) : unit = bridge?onToast (cb)
let onToggleCapture (cb: unit -> unit) : unit = bridge?onToggleCapture (cb)
let onNudge (cb: float -> float -> unit) : unit = bridge?onNudge (System.Func<float, float, unit>(cb))
let onOpenSettings (cb: unit -> unit) : unit = bridge?onOpenSettings (cb)
let openScreenPrivacy () : unit = bridge?openScreenPrivacy ()
let validateKey (provider: string) (key: string) : JS.Promise<obj> = bridge?validateKey (provider, key)
let googleSignIn () : JS.Promise<obj> = bridge?googleSignIn ()
let appleSignIn () : JS.Promise<obj> = bridge?appleSignIn ()
let googleSignOut () : JS.Promise<obj> = bridge?googleSignOut ()
let saveState (state: obj) : JS.Promise<obj> = bridge?saveState (state)
let loadState () : JS.Promise<obj> = bridge?loadState ()

[<Emit("window.innerWidth")>]
let innerWidth () : float = jsNative

[<Emit("setTimeout($1, $0)")>]
let setTimeoutMs (ms: int) (cb: unit -> unit) : unit = jsNative

[<Emit("window.innerHeight")>]
let innerHeight () : float = jsNative

/// streamChat(req, onEvent) -> unsubscribe fn. onEvent gets {type, text|message}.
let streamChat (req: obj) (onEvent: obj -> unit) : (unit -> unit) =
    bridge?streamChat (req, onEvent)

/// Crop a region out of a full-screen PNG data URL; returns a PNG data URL.
[<Emit("""(function(dataUrl, sx, sy, sw, sh){
  return new Promise(function(resolve){
    var img = new Image();
    img.onload = function(){
      var w = Math.max(1, Math.round(sw)), h = Math.max(1, Math.round(sh));
      var c = document.createElement('canvas');
      c.width = w; c.height = h;
      var ctx = c.getContext('2d');
      ctx.drawImage(img, sx, sy, sw, sh, 0, 0, w, h);
      resolve(c.toDataURL('image/png'));
    };
    img.src = dataUrl;
  });
})($0,$1,$2,$3,$4)""")>]
let cropImage (dataUrl: string) (sx: float) (sy: float) (sw: float) (sh: float) : JS.Promise<string> = jsNative

[<Emit("navigator.clipboard.writeText($0)")>]
let copy (s: string) : unit = jsNative

/// Robust click-through. One document-level hit-test drives setIgnoreMouse: when the
/// topmost element under the cursor sits inside a `.ss-interactive` region the overlay
/// captures the mouse, otherwise events pass through to the app below. Runs on every
/// pointer move (Electron forwards move events even while ignoring), so it re-evaluates
/// the instant the drag catch-layer appears and can never get stuck click-through the
/// way per-element mouseenter/mouseleave did.
[<Emit("""(function(){
  var cur = null, raf = 0, lx = 0, ly = 0;
  function set(v){ if(v!==cur){ cur=v; try{ window.sideshift.setIgnoreMouse(v); }catch(e){} } }
  function run(){ raf = 0;
    var el = document.elementFromPoint(lx, ly);
    set(!(el && el.closest && el.closest('.ss-interactive')));
  }
  // Coalesce to one hit-test per frame: Electron forwards EVERY screen-wide mouse move
  // while click-through, so probing the DOM per event burns CPU at 120Hz and lags the UI.
  function queue(e){ lx = e.clientX; ly = e.clientY; if(!raf) raf = requestAnimationFrame(run); }
  // Single source of truth: direct callers (Interop.setIgnoreMouse) go through the same
  // dedup so they can never desync the cache from the real OS pass-through state.
  window.__ssSetIgnore = set;
  window.addEventListener('pointermove', queue, true);
  window.addEventListener('mousemove',   queue, true);
  // pointerdown resolves synchronously so the very first click on fresh UI still lands
  window.addEventListener('pointerdown', function(e){ lx=e.clientX; ly=e.clientY; if(raf){ cancelAnimationFrame(raf); raf=0; } run(); }, true);
  set(true);
})()""")>]
let installHitTest () : unit = jsNative
