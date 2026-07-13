module SideShift.Update

open Fable.Core
open Fable.Core.JsInterop
open Elmish
open SideShift.Types

// true for both JS null and undefined (loose equality) — for optional persisted fields.
[<Emit("$0 == null")>]
let private isNil (x: obj) : bool = jsNative

// Warm brand-family palette so multiple widgets stay distinguishable but on-brand.
let private palette =
    [| "#F5B23B"; "#E4571E"; "#E8912B"; "#D96B3A"; "#B99433"; "#C0491C"; "#E0A050" |]

let private titleFor mode =
    match mode with
    | Ask -> "Ask"
    | ELI5 -> "ELI5"
    | Verify -> "Verify"
    | Diff -> "Diff"

let private effect (f: unit -> unit) : Cmd<Msg> = [ fun _ -> f () ]

let private mapWidget id f model =
    { model with Widgets = model.Widgets |> List.map (fun w -> if w.Id = id then f w else w) }

// Keep a widget reachable: never let its title bar leave the viewport (so drag/minimize/
// close stay clickable). Leaves a margin of on-screen widget on every edge.
let private clampPos (w: Widget) : Widget =
    let vw = Interop.innerWidth ()
    let vh = Interop.innerHeight ()
    let x = max 0.0 (min w.PosX (max 0.0 (vw - 140.0)))
    let y = max 0.0 (min w.PosY (max 0.0 (vh - 48.0)))
    { w with PosX = x; PosY = y }

let private keyOpt (r: obj) : string option =
    let k = r?key
    if isNull k then None else Some(string k)

let private classifyCmd apiKey (cap: SideShift.Types.Capture) : Cmd<Msg> =
    [ fun dispatch ->
        let req = SideShift.Api.buildClassifyReq apiKey cap
        let buf = System.Text.StringBuilder()
        Interop.streamChat req (fun ev ->
            match string (ev?("type")) with
            | "delta" -> buf.Append(string ev?text) |> ignore
            | "done" -> dispatch (PendingClassified((buf.ToString().ToUpper()).Contains "CODE"))
            | _ -> ())
        |> ignore ]

// Track the live stream's unsubscribe per widget so we can detach the IPC listener on
// completion (was leaked — one dead listener per message forever) and on widget close.
let private activeStreams = System.Collections.Generic.Dictionary<int, unit -> unit>()

let private stopStream (id: int) =
    match activeStreams.TryGetValue id with
    | true, unsub ->
        activeStreams.Remove id |> ignore
        (try unsub () with _ -> ())
    | _ -> ()

let private streamCmd req (id: int) : Cmd<Msg> =
    [ fun dispatch ->
        stopStream id
        let mutable unsub = ignore
        unsub <-
            Interop.streamChat req (fun ev ->
                match string (ev?("type")) with
                | "delta" -> dispatch (StreamDelta(id, string ev?text))
                | "done" -> stopStream id; dispatch (StreamDone id)
                | "error" -> stopStream id; dispatch (StreamError(id, string ev?message))
                | _ -> ())
        activeStreams.[id] <- unsub ]

let private modeStr =
    function
    | Ask -> "ask"
    | ELI5 -> "eli5"
    | Verify -> "verify"
    | Diff -> "diff"

let private strMode =
    function
    | "eli5" -> ELI5
    | "verify" -> Verify
    | "diff" -> Diff
    | _ -> Ask

let private surfaceStr =
    function
    | Opaque -> "opaque"
    | Transparent -> "transparent"
    | Translucent -> "translucent"

let private strSurface =
    function
    | "opaque" -> Opaque
    | "transparent" -> Transparent
    | _ -> Translucent

let private themeStr =
    function
    | Light -> "light"
    | Dark -> "dark"

let private strTheme =
    function
    | "light" -> Light
    | _ -> Dark

/// Serialize the restorable slice of the model to a plain JS object.
let private serialize (m: Model) : obj =
    box
        {| nextId = m.NextId
           topZ = m.TopZ
           accent = m.AccentColor
           opacity = surfaceStr m.Opacity
           theme = themeStr m.Theme
           webVerify = m.WebVerify
           critic = m.CriticModel
           googleEmail = Option.toObj m.GoogleEmail
           shared = m.SharedContext |> List.toArray
           widgets =
            m.Widgets
            |> List.map (fun w ->
                box
                    {| id = w.Id
                       mode = modeStr w.Mode
                       title = w.Title
                       img = w.Capture.ImageDataUrl
                       ctext = w.Capture.Text
                       cx = w.Capture.X
                       cy = w.Capture.Y
                       cw = w.Capture.W
                       ch = w.Capture.H
                       messages =
                        w.Messages
                        |> List.map (fun mm -> box {| role = mm.Role; text = mm.Text |})
                        |> List.toArray
                       input = w.Input
                       posX = w.PosX
                       posY = w.PosY
                       width = w.Width
                       height = w.Height
                       z = w.Z
                       minimized = w.Minimized
                       color = w.Color
                       via = w.Via |})
            |> List.toArray |}

let private parseWidget (o: obj) : Widget =
    let msgs: obj [] = unbox o?messages
    { Id = unbox o?id
      Mode = strMode (unbox o?mode)
      Title = unbox o?title
      Capture = { ImageDataUrl = unbox o?img
                  Text = (if isNil o?ctext then "" else unbox o?ctext)
                  X = unbox o?cx; Y = unbox o?cy; W = unbox o?cw; H = unbox o?ch }
      CtxImages = [] // live-context frames are RAM-only, never restored
      Messages = msgs |> Array.map (fun m -> { Role = unbox m?role; Text = unbox m?text }) |> Array.toList
      Input = unbox o?input
      Streaming = false
      StreamBuf = ""
      Error = None
      Via = unbox o?via
      PosX = unbox o?posX
      PosY = unbox o?posY
      Width = unbox o?width
      Height = unbox o?height
      Z = unbox o?z
      Minimized = unbox o?minimized
      Color = unbox o?color }

let private saveCmd (m: Model) : Cmd<Msg> =
    effect (fun () -> Interop.saveState (serialize m) |> ignore)

let private prefsObj (m: Model) : obj =
    box
        {| accent = m.AccentColor
           opacity = surfaceStr m.Opacity
           theme = themeStr m.Theme
           webVerify = m.WebVerify
           critic = m.CriticModel
           googleEmail = Option.toObj m.GoogleEmail |}

// The settings window persists preferences through a merge-only channel so it can
// never overwrite the overlay's live widgets; the overlay saves the full state.
let private persist (m: Model) : Cmd<Msg> =
    if Interop.isSettingsWindow then effect (fun () -> Interop.savePrefs (prefsObj m) |> ignore)
    else saveCmd m

let init () : Model * Cmd<Msg> =
    { AnthropicKey = None
      OpenRouterKey = None
      DefaultModel = SideShift.Api.DEFAULT_ANTHROPIC_MODEL
      CriticModel = SideShift.Api.DEFAULT_CRITIC_MODEL
      // the dedicated settings window boots straight into the settings view
      ShowSettings = Interop.isSettingsWindow
      AnthropicDraft = ""
      OpenRouterDraft = ""
      CriticDraft = SideShift.Api.DEFAULT_CRITIC_MODEL
      Validating = false
      KeyError = (let e = Interop.settingsErrParam in if e = "" then None else Some e)
      AccentColor = "#E4571E"
      Opacity = Translucent
      Theme = Dark
      WebVerify = false
      GoogleEmail = None
      GoogleBusy = false
      GoogleErr = None
      Widgets = []
      NextId = 1
      TopZ = 10
      CaptureMode = false
      Screenshot = None
      Pending = None
      PendingCode = false
      Drag = None
      Resize = None
      Closing = None
      SharedContext = []
      Toast = None },
    Cmd.batch
        [ Cmd.OfPromise.perform Interop.loadKey "anthropic" (fun r -> KeyLoaded("anthropic", keyOpt r))
          Cmd.OfPromise.perform Interop.loadKey "openrouter" (fun r -> KeyLoaded("openrouter", keyOpt r))
          Cmd.OfPromise.perform Interop.loadState () StateLoaded ]

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | Noop -> model, Cmd.none

    // --- settings ---
    | KeyLoaded("anthropic", k) ->
        if Interop.isSettingsWindow then
            // settings window: show the stored key in the (masked) draft field
            { model with AnthropicKey = k; AnthropicDraft = defaultArg k "" }, Cmd.none
        elif Option.isNone k && not model.ShowSettings then
            // overlay first run without a key: pop the native settings window once
            { model with AnthropicKey = k; ShowSettings = true },
            effect (fun () -> Interop.openSettingsWindow "")
        else
            { model with AnthropicKey = k }, Cmd.none
    | KeyLoaded("openrouter", k) ->
        if Interop.isSettingsWindow then { model with OpenRouterKey = k; OpenRouterDraft = defaultArg k "" }, Cmd.none
        else { model with OpenRouterKey = k }, Cmd.none
    | KeyLoaded(_, _) -> model, Cmd.none
    | KeysReload ->
        model,
        Cmd.batch
            [ Cmd.OfPromise.perform Interop.loadKey "anthropic" (fun r -> KeyLoaded("anthropic", keyOpt r))
              Cmd.OfPromise.perform Interop.loadKey "openrouter" (fun r -> KeyLoaded("openrouter", keyOpt r)) ]
    | PrefsChanged o ->
        let critic = if isNil o?critic then model.CriticModel else (unbox o?critic: string)
        { model with
            AccentColor = (if isNil o?accent then model.AccentColor else (unbox o?accent: string))
            Opacity = (if isNil o?opacity then model.Opacity else strSurface (unbox o?opacity: string))
            Theme = (if isNil o?theme then model.Theme else strTheme (unbox o?theme: string))
            WebVerify = (if isNil o?webVerify then model.WebVerify else (unbox o?webVerify: bool))
            CriticModel = critic
            CriticDraft = critic
            GoogleEmail = (if isNil o?googleEmail then None else Some(unbox o?googleEmail: string)) },
        Cmd.none
    | SettingsError e -> { model with KeyError = Some e; Validating = false }, Cmd.none
    | StateLoaded o ->
        if isNull o then
            model, Cmd.none
        else
            let ws: obj [] = unbox o?widgets
            let sh: obj [] = unbox o?shared
            let critic = if isNil o?critic then model.CriticModel else (unbox o?critic: string)
            { model with
                // clamp on restore so a window saved off-screen (resolution change) is reachable
                Widgets = ws |> Array.map (parseWidget >> clampPos) |> Array.toList
                NextId = max model.NextId (unbox o?nextId: int)
                TopZ = max model.TopZ (unbox o?topZ: int)
                AccentColor = (if isNil o?accent then model.AccentColor else (unbox o?accent: string))
                Opacity = (if isNil o?opacity then model.Opacity else strSurface (unbox o?opacity: string))
                Theme = (if isNil o?theme then model.Theme else strTheme (unbox o?theme: string))
                WebVerify = (if isNil o?webVerify then model.WebVerify else (unbox o?webVerify: bool))
                CriticModel = critic
                CriticDraft = critic
                GoogleEmail = (if isNil o?googleEmail then model.GoogleEmail else Some(unbox o?googleEmail: string))
                SharedContext = sh |> Array.map (fun s -> (unbox s: string)) |> Array.toList },
            Cmd.none
    | OpenSettings ->
        if Interop.isSettingsWindow then
            model, Cmd.none
        else
            // settings is its own native window (traffic lights, real activation)
            { model with ShowSettings = true }, effect (fun () -> Interop.openSettingsWindow "")
    | CloseSettings ->
        if Interop.isSettingsWindow then
            model, effect Interop.closeSettingsWindow
        else
            { model with ShowSettings = false; Validating = false; KeyError = None }, Cmd.none
    | AnthropicDraftChanged s -> { model with AnthropicDraft = s }, Cmd.none
    | OpenRouterDraftChanged s -> { model with OpenRouterDraft = s }, Cmd.none
    | CriticDraftChanged s -> { model with CriticDraft = s }, Cmd.none
    | SaveSettings ->
        let ak = model.AnthropicDraft.Trim()
        let effective = if ak <> "" then ak else defaultArg model.AnthropicKey ""
        if effective = "" then
            { model with KeyError = Some "An Anthropic API key is required." }, Cmd.none
        elif not (effective.StartsWith "sk-") then
            { model with KeyError = Some "That does not look like an API key. Anthropic keys start with sk-ant-." }, Cmd.none
        else
            { model with Validating = true; KeyError = None },
            Cmd.OfPromise.either
                (fun () -> Interop.validateKey "anthropic" effective) ()
                (fun r -> KeyValidated(unbox r?ok, unbox r?valid))
                (fun _ -> KeyValidated(false, false))
    | KeyValidated(reachable, valid) ->
        if reachable && not valid then
            { model with Validating = false; KeyError = Some "That API key was rejected (401). Check it and try again." }, Cmd.none
        else
            // valid, or offline (could not reach the provider) -> accept and persist.
            let ak = model.AnthropicDraft.Trim()
            let ork = model.OpenRouterDraft.Trim()
            let critic = let c = model.CriticDraft.Trim() in if c = "" then SideShift.Api.DEFAULT_CRITIC_MODEL else c
            let m2 =
                { model with
                    AnthropicKey = (if ak = "" then model.AnthropicKey else Some ak)
                    OpenRouterKey = (if ork = "" then None else Some ork)
                    CriticModel = critic
                    Validating = false
                    KeyError = None
                    ShowSettings = false }
            let cmds =
                [ if ak <> "" then Cmd.OfPromise.perform (fun () -> Interop.saveKey "anthropic" ak) () (fun _ -> SettingsSaved)
                  // persist a set OpenRouter key, and actually erase it from disk when cleared
                  // (otherwise the old key resurrects on restart)
                  if ork <> "" then Cmd.OfPromise.perform (fun () -> Interop.saveKey "openrouter" ork) () (fun _ -> SettingsSaved)
                  elif Option.isSome model.OpenRouterKey then Cmd.OfPromise.perform (fun () -> Interop.clearKey "openrouter") () (fun _ -> SettingsSaved)
                  // persist critic model + prefs (merge-only channel in the settings window)
                  persist m2 ]
            if Interop.isSettingsWindow then
                // saved from the native settings window: close it when done
                m2, Cmd.batch (cmds @ [ effect Interop.closeSettingsWindow ])
            else
                m2, Cmd.batch cmds
    | SettingsSaved -> model, Cmd.none
    | SetAccent c ->
        let m2 = { model with AccentColor = c }
        m2, persist m2
    | SetOpacity o ->
        let m2 = { model with Opacity = o }
        m2, persist m2
    | SetTheme t ->
        let m2 = { model with Theme = t }
        m2, persist m2
    | SetWebVerify b ->
        let m2 = { model with WebVerify = b }
        m2, persist m2
    | DoGoogleSignIn ->
        { model with GoogleBusy = true; GoogleErr = None },
        Cmd.OfPromise.either
            (fun () -> Interop.googleSignIn ()) ()
            (fun r ->
                if unbox r?ok then
                    let p = r?profile
                    let email = if isNil p then None else (let e = p?email in if isNil e then None else Some(string e))
                    GoogleSignedIn(email, None)
                else
                    GoogleSignedIn(None, Some(string r?error)))
            (fun ex -> GoogleSignedIn(None, Some ex.Message))
    | DoAppleSignIn ->
        { model with GoogleErr = None },
        Cmd.OfPromise.either
            (fun () -> Interop.appleSignIn ()) ()
            (fun r ->
                if unbox r?ok then
                    let p = r?profile
                    let email = if isNil p then None else (let e = p?email in if isNil e then None else Some(string e))
                    GoogleSignedIn(email, None)
                else
                    GoogleSignedIn(None, Some(string r?error)))
            (fun ex -> GoogleSignedIn(None, Some ex.Message))
    | GoogleSignedIn(email, err) ->
        let m2 = { model with GoogleBusy = false; GoogleEmail = email; GoogleErr = err }
        m2, (if Option.isSome email then persist m2 else Cmd.none)
    | GoogleSignOut ->
        let m2 = { model with GoogleEmail = None }
        m2, Cmd.batch [ effect (fun () -> Interop.googleSignOut () |> ignore); persist m2 ]
    | OpenScreenPrivacy -> model, effect (fun () -> Interop.openScreenPrivacy ())
    | NudgeFocused(dx, dy) ->
        match model.Widgets |> List.filter (fun w -> not w.Minimized) with
        | [] -> model, Cmd.none
        | vis ->
            let top = vis |> List.maxBy (fun w -> w.Z)
            let m2 = mapWidget top.Id (fun w -> clampPos { w with PosX = w.PosX + dx; PosY = w.PosY + dy }) model
            m2, saveCmd m2

    // --- capture flow ---
    | ToggleCapture ->
        if model.CaptureMode then
            { model with CaptureMode = false; Screenshot = None }, effect (fun () -> Interop.setIgnoreMouse true)
        else
            model,
            Cmd.OfPromise.either Interop.captureScreen ()
                (fun r ->
                    if isNil r?ok || unbox r?ok then ScreenshotReady(string r?dataUrl, float r?width, float r?height, float r?scale)
                    else CaptureFailed(string r?message))
                (fun ex -> CaptureFailed ex.Message)
    | ScreenshotReady(d, w, h, s) ->
        { model with CaptureMode = true; Screenshot = Some(d, w, h, s) },
        effect (fun () -> Interop.setIgnoreMouse false)
    | CaptureFailed msg ->
        // surface the reason (usually macOS Screen Recording permission) in the settings window
        { model with CaptureMode = false; Screenshot = None },
        effect (fun () -> Interop.openSettingsWindow msg)
    | SelectionCaptured txt ->
        // Highlighted text grabbed from the frontmost app (Cmd+Shift+S): same action
        // bar as a region capture, centered near the top, no pixels involved.
        // Bound the size so a huge selection can't bloat every request + state.json.
        let t = (truncateSafe 12000 txt).Trim()
        if t = "" then model, Cmd.none
        else
            let looksCode =
                t.Contains "{" || t.Contains ";" || t.Contains "=>" || t.Contains "</"
                || t.Contains "def " || t.Contains "fun " || t.Contains "()"
            let cap = { ImageDataUrl = ""; Text = t; X = 0.0; Y = 0.0; W = 0.0; H = 0.0 }
            let ax = max 8.0 (Interop.innerWidth () / 2.0 - 150.0)
            // clear any half-open capture crosshair so the pending bar isn't buried
            // under the full-screen selector scrim; make the overlay clickable.
            { model with CaptureMode = false; Screenshot = None; Pending = Some(cap, ax, 96.0); PendingCode = looksCode },
            effect (fun () -> Interop.setIgnoreMouse false)
    | ShowToast m ->
        { model with Toast = Some m }, [ fun dispatch -> Interop.setTimeoutMs 3400 (fun () -> dispatch ClearToast) ]
    | ClearToast -> { model with Toast = None }, Cmd.none
    | InstantAsk payload ->
        // Whole-screen shot + recent live-context frames: open a chat that already
        // sees the screen. No pending bar — this is the zero-friction primary flow.
        let shot: string = unbox payload?dataUrl
        let ctx: string list =
            if isNil payload?ctx then []
            else (unbox payload?ctx: obj []) |> Array.map (fun c -> (unbox c: string)) |> Array.toList
        let id = model.NextId
        let z = model.TopZ + 1
        let vw = Interop.innerWidth ()
        let vh = Interop.innerHeight ()
        let w =
            { Id = id
              Mode = Ask
              Title = "Screen"
              Capture = { ImageDataUrl = shot; Text = ""; X = 0.0; Y = 0.0; W = 0.0; H = 0.0 }
              CtxImages = ctx
              Messages = []
              Input = ""
              Streaming = false
              StreamBuf = ""
              Error = None
              Via = ""
              PosX = max 8.0 (vw - 420.0)
              PosY = min 84.0 (max 8.0 (vh - 460.0))
              Width = 400.0
              Height = 440.0
              Z = z
              Minimized = false
              Color = palette.[id % palette.Length] }
        let m2 =
            { model with
                Widgets = w :: model.Widgets; NextId = id + 1; TopZ = z
                CaptureMode = false; Screenshot = None; Pending = None; PendingCode = false }
        m2, Cmd.batch [ effect (fun () -> Interop.setIgnoreMouse false); saveCmd m2 ]
    | CaptureCancelled ->
        { model with CaptureMode = false; Screenshot = None }, effect (fun () -> Interop.setIgnoreMouse true)
    | RegionDrawn(x, y, w, h) ->
        match model.Screenshot with
        | Some(dataUrl, _, _, scale) when w > 4.0 && h > 4.0 ->
            { model with CaptureMode = false },
            Cmd.OfPromise.either
                (fun () -> Interop.cropImage dataUrl (x * scale) (y * scale) (w * scale) (h * scale)) ()
                (fun png -> RegionReady { ImageDataUrl = png; Text = ""; X = x; Y = y; W = w; H = h })
                (fun _ -> CaptureCancelled)
        | _ ->
            { model with CaptureMode = false; Screenshot = None }, effect (fun () -> Interop.setIgnoreMouse true)
    | RegionReady cap ->
        let m2 = { model with Screenshot = None; Pending = Some(cap, cap.X + cap.W, cap.Y); PendingCode = false }
        match model.AnthropicKey with
        | Some k -> m2, classifyCmd k cap
        | None -> m2, Cmd.none
    | PendingClassified isCode ->
        match model.Pending with
        // only apply an image classify to an image pending; a late result must never
        // relabel a text selection that replaced the region while the classify was in flight
        | Some(cap, _, _) when cap.Text = "" -> { model with PendingCode = isCode }, Cmd.none
        | _ -> model, Cmd.none
    | DismissPending ->
        { model with Pending = None; PendingCode = false }, effect (fun () -> Interop.setIgnoreMouse true)
    | QuickAction mode ->
        match model.Pending with
        | None -> model, Cmd.none
        | Some(cap, ax, ay) ->
            let id = model.NextId
            let z = model.TopZ + 1
            // text selections have no origin rect; spawn the widget where the action
            // bar was (centered top) instead of the top-left corner (0,0)
            let baseX = if cap.Text <> "" then ax else cap.X
            let baseY = if cap.Text <> "" then ay + 8.0 else cap.Y + 10.0
            let w =
                { Id = id
                  Mode = mode
                  Title = titleFor mode
                  Capture = cap
                  CtxImages = []
                  Messages = []
                  Input = ""
                  Streaming = false
                  StreamBuf = ""
                  Error = None
                  Via = ""
                  PosX = baseX
                  PosY = baseY
                  Width = 380.0
                  Height = 440.0
                  Z = z
                  Minimized = false
                  Color = palette.[id % palette.Length] }
                |> fun ww ->
                    // clamp so the whole panel (not just the title bar) stays on-screen
                    let vw = Interop.innerWidth ()
                    let vh = Interop.innerHeight ()
                    { ww with
                        PosX = max 8.0 (min ww.PosX (max 8.0 (vw - ww.Width - 8.0)))
                        PosY = max 8.0 (min ww.PosY (max 8.0 (vh - ww.Height - 8.0))) }
            let model2 = { model with Widgets = w :: model.Widgets; NextId = id + 1; TopZ = z; Pending = None; PendingCode = false }
            match SideShift.Api.firstPrompt mode with
            | Some p ->
                let model3 = mapWidget id (fun x -> { x with Input = p }) model2
                model3, Cmd.batch [ Cmd.ofMsg (Send id); saveCmd model3 ]
            | None -> model2, saveCmd model2

    // --- widget lifecycle ---
    | Focus id ->
        let z = model.TopZ + 1
        { (mapWidget id (fun w -> { w with Z = z }) model) with TopZ = z }, Cmd.none
    | StartDrag(id, ox, oy) ->
        let z = model.TopZ + 1
        { (mapWidget id (fun w -> { w with Z = z }) model) with TopZ = z; Drag = Some(id, ox, oy) }, Cmd.none
    | StartResize id ->
        let z = model.TopZ + 1
        { (mapWidget id (fun w -> { w with Z = z }) model) with TopZ = z; Resize = Some id }, Cmd.none
    | PointerMove(x, y) ->
        match model.Drag, model.Resize with
        | Some(id, ox, oy), _ -> (mapWidget id (fun w -> clampPos { w with PosX = x - ox; PosY = y - oy }) model), Cmd.none
        | None, Some id ->
            (mapWidget id (fun w -> { w with Width = max 260.0 (x - w.PosX); Height = max 220.0 (y - w.PosY) }) model),
            Cmd.none
        | None, None -> model, Cmd.none
    | PointerUp ->
        let m2 = { model with Drag = None; Resize = None }
        m2, (if Option.isSome model.Drag || Option.isSome model.Resize then saveCmd m2 else Cmd.none)
    | Minimize id ->
        let m2 = mapWidget id (fun w -> { w with Minimized = true }) model
        m2, saveCmd m2
    | Restore id ->
        let z = model.TopZ + 1
        let m2 = { (mapWidget id (fun w -> clampPos { w with Minimized = false; Z = z }) model) with TopZ = z }
        m2, saveCmd m2
    | RequestClose id -> { model with Closing = Some id }, Cmd.none
    | CancelClose -> { model with Closing = None }, Cmd.none
    | CloseWith(id, policy) ->
        let shared =
            match policy with
            | Discard -> model.SharedContext
            | Merge ->
                match model.Widgets |> List.tryFind (fun w -> w.Id = id) with
                | Some w when not (List.isEmpty w.Messages) -> model.SharedContext @ [ SideShift.Api.mergeSummary w ]
                | _ -> model.SharedContext
        stopStream id // detach any in-flight stream listener for the widget being closed
        let m2 =
            { model with
                Widgets = model.Widgets |> List.filter (fun w -> w.Id <> id)
                Closing = None
                SharedContext = shared }
        m2, saveCmd m2
    | Merged _ -> model, Cmd.none

    // --- chat ---
    | InputChanged(id, s) -> (mapWidget id (fun w -> { w with Input = s }) model), Cmd.none
    | Send id ->
        match model.Widgets |> List.tryFind (fun w -> w.Id = id) with
        // ignore a second send while a reply is still streaming — concurrent streams
        // would interleave into one corrupted assistant message
        | Some w when w.Streaming -> model, Cmd.none
        | Some w when w.Input.Trim() <> "" ->
            match SideShift.Api.routeFor model w.Mode with
            | Some(provider, apiKey, modelId, via, webGrounded) ->
                let text = w.Input.Trim()
                let system =
                    if w.Mode = Verify && webGrounded then SideShift.Api.verifyWebSystem model.SharedContext
                    else SideShift.Api.systemFor w.Mode model.SharedContext
                let req = SideShift.Api.buildReq provider apiKey modelId system webGrounded w text
                let model2 =
                    mapWidget id
                        (fun x ->
                            { x with
                                Messages = x.Messages @ [ { Role = "user"; Text = text } ]
                                Input = ""
                                Streaming = true
                                StreamBuf = ""
                                Error = None
                                Via = via })
                        model
                model2, streamCmd req id
            | None -> { model with ShowSettings = true }, effect (fun () -> Interop.setIgnoreMouse false)
        | _ -> model, Cmd.none
    | StreamDelta(id, t) -> (mapWidget id (fun w -> { w with StreamBuf = w.StreamBuf + t }) model), Cmd.none
    | StreamDone id ->
        let m2 =
            mapWidget id
                (fun w ->
                    { w with
                        Messages = w.Messages @ [ { Role = "assistant"; Text = w.StreamBuf } ]
                        StreamBuf = ""
                        Streaming = false })
                model
        m2, saveCmd m2
    | StreamError(id, m) ->
        // keep any partial reply the user was already reading instead of discarding it
        (mapWidget id
            (fun w ->
                { w with
                    Streaming = false
                    Error = Some m
                    Messages = (if w.StreamBuf <> "" then w.Messages @ [ { Role = "assistant"; Text = w.StreamBuf } ] else w.Messages)
                    StreamBuf = "" })
            model),
        Cmd.none
