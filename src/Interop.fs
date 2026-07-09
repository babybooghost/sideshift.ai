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
