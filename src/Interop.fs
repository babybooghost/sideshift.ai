module SideShift.Interop

open Fable.Core
open Fable.Core.JsInterop

// Bridge exposed by electron/preload.js on window.sideshift.
[<Emit("window.sideshift")>]
let private bridge : obj = jsNative

let captureScreen () : JS.Promise<obj> = bridge?captureScreen ()
let saveKey (name: string) (value: string) : JS.Promise<obj> = bridge?saveKey (name, value)
let loadKey (name: string) : JS.Promise<obj> = bridge?loadKey (name)
let setIgnoreMouse (b: bool) : unit = bridge?setIgnoreMouse (b)
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
  var cur = null;
  function set(v){ if(v!==cur){ cur=v; try{ window.sideshift.setIgnoreMouse(v); }catch(e){} } }
  function hit(x,y){
    var el = document.elementFromPoint(x,y);
    set(!(el && el.closest && el.closest('.ss-interactive')));
  }
  window.addEventListener('pointermove', function(e){ hit(e.clientX, e.clientY); }, true);
  window.addEventListener('mousemove',   function(e){ hit(e.clientX, e.clientY); }, true);
  window.addEventListener('pointerdown', function(e){ hit(e.clientX, e.clientY); }, true);
  try{ window.sideshift.setIgnoreMouse(true); }catch(e){}
})()""")>]
let installHitTest () : unit = jsNative
