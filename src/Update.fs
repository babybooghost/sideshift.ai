module SideShift.Update

open Fable.Core.JsInterop
open Elmish
open SideShift.Types

let private palette =
    [| "#6366f1"; "#ec4899"; "#10b981"; "#f59e0b"; "#06b6d4"; "#8b5cf6"; "#ef4444" |]

let private titleFor mode =
    match mode with
    | Ask -> "Ask"
    | ELI5 -> "ELI5"
    | Verify -> "Verify"
    | Diff -> "Diff"

let private effect (f: unit -> unit) : Cmd<Msg> = [ fun _ -> f () ]

let private mapWidget id f model =
    { model with Widgets = model.Widgets |> List.map (fun w -> if w.Id = id then f w else w) }

let private keyOpt (r: obj) : string option =
    let k = r?key
    if isNull k then None else Some(string k)

let private streamCmd req (id: int) : Cmd<Msg> =
    [ fun dispatch ->
        Interop.streamChat req (fun ev ->
            match string (ev?("type")) with
            | "delta" -> dispatch (StreamDelta(id, string ev?text))
            | "done" -> dispatch (StreamDone id)
            | "error" -> dispatch (StreamError(id, string ev?message))
            | _ -> ())
        |> ignore ]

let init () : Model * Cmd<Msg> =
    { AnthropicKey = None
      OpenRouterKey = None
      DefaultModel = SideShift.Api.DEFAULT_ANTHROPIC_MODEL
      CriticModel = SideShift.Api.DEFAULT_CRITIC_MODEL
      ShowSettings = false
      AnthropicDraft = ""
      OpenRouterDraft = ""
      CriticDraft = SideShift.Api.DEFAULT_CRITIC_MODEL
      Widgets = []
      NextId = 1
      TopZ = 10
      CaptureMode = false
      Screenshot = None
      Pending = None
      Drag = None
      Resize = None
      Closing = None
      SharedContext = [] },
    Cmd.batch
        [ Cmd.OfPromise.perform Interop.loadKey "anthropic" (fun r -> KeyLoaded("anthropic", keyOpt r))
          Cmd.OfPromise.perform Interop.loadKey "openrouter" (fun r -> KeyLoaded("openrouter", keyOpt r)) ]

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | Noop -> model, Cmd.none

    // --- settings ---
    | KeyLoaded("anthropic", k) ->
        { model with AnthropicKey = k; ShowSettings = model.ShowSettings || Option.isNone k }, Cmd.none
    | KeyLoaded("openrouter", k) -> { model with OpenRouterKey = k }, Cmd.none
    | KeyLoaded(_, _) -> model, Cmd.none
    | OpenSettings ->
        { model with
            ShowSettings = true
            AnthropicDraft = defaultArg model.AnthropicKey ""
            OpenRouterDraft = defaultArg model.OpenRouterKey ""
            CriticDraft = model.CriticModel }, effect (fun () -> Interop.setIgnoreMouse false)
    | CloseSettings -> { model with ShowSettings = false }, effect (fun () -> Interop.setIgnoreMouse true)
    | AnthropicDraftChanged s -> { model with AnthropicDraft = s }, Cmd.none
    | OpenRouterDraftChanged s -> { model with OpenRouterDraft = s }, Cmd.none
    | CriticDraftChanged s -> { model with CriticDraft = s }, Cmd.none
    | SaveSettings ->
        let ak = model.AnthropicDraft.Trim()
        let ork = model.OpenRouterDraft.Trim()
        let critic = let c = model.CriticDraft.Trim() in if c = "" then SideShift.Api.DEFAULT_CRITIC_MODEL else c
        let cmds =
            [ if ak <> "" then Cmd.OfPromise.perform (fun () -> Interop.saveKey "anthropic" ak) () (fun _ -> SettingsSaved)
              if ork <> "" then Cmd.OfPromise.perform (fun () -> Interop.saveKey "openrouter" ork) () (fun _ -> SettingsSaved) ]
        { model with
            AnthropicKey = (if ak = "" then model.AnthropicKey else Some ak)
            OpenRouterKey = (if ork = "" then None else Some ork)
            CriticModel = critic
            ShowSettings = false },
        Cmd.batch (effect (fun () -> Interop.setIgnoreMouse true) :: cmds)
    | SettingsSaved -> model, Cmd.none

    // --- capture flow ---
    | ToggleCapture ->
        if model.CaptureMode then
            { model with CaptureMode = false; Screenshot = None }, effect (fun () -> Interop.setIgnoreMouse true)
        else
            model,
            Cmd.OfPromise.either Interop.captureScreen ()
                (fun r -> ScreenshotReady(string r?dataUrl, float r?width, float r?height, float r?scale))
                (fun _ -> CaptureCancelled)
    | ScreenshotReady(d, w, h, s) ->
        { model with CaptureMode = true; Screenshot = Some(d, w, h, s) },
        effect (fun () -> Interop.setIgnoreMouse false)
    | CaptureCancelled ->
        { model with CaptureMode = false; Screenshot = None }, effect (fun () -> Interop.setIgnoreMouse true)
    | RegionDrawn(x, y, w, h) ->
        match model.Screenshot with
        | Some(dataUrl, _, _, scale) when w > 4.0 && h > 4.0 ->
            { model with CaptureMode = false },
            Cmd.OfPromise.either
                (fun () -> Interop.cropImage dataUrl (x * scale) (y * scale) (w * scale) (h * scale)) ()
                (fun png -> RegionReady { ImageDataUrl = png; X = x; Y = y; W = w; H = h })
                (fun _ -> CaptureCancelled)
        | _ ->
            { model with CaptureMode = false; Screenshot = None }, effect (fun () -> Interop.setIgnoreMouse true)
    | RegionReady cap ->
        { model with Screenshot = None; Pending = Some(cap, cap.X + cap.W, cap.Y) }, Cmd.none
    | DismissPending ->
        { model with Pending = None }, effect (fun () -> Interop.setIgnoreMouse true)
    | QuickAction mode ->
        match model.Pending with
        | None -> model, Cmd.none
        | Some(cap, _, _) ->
            let id = model.NextId
            let z = model.TopZ + 1
            let w =
                { Id = id
                  Mode = mode
                  Title = titleFor mode
                  Capture = cap
                  Messages = []
                  Input = ""
                  Streaming = false
                  StreamBuf = ""
                  Error = None
                  Via = ""
                  PosX = min cap.X 920.0
                  PosY = min (cap.Y + 10.0) 560.0
                  Width = 380.0
                  Height = 440.0
                  Z = z
                  Minimized = false
                  Color = palette.[id % palette.Length] }
            let model2 = { model with Widgets = w :: model.Widgets; NextId = id + 1; TopZ = z; Pending = None }
            match SideShift.Api.firstPrompt mode with
            | Some p -> (mapWidget id (fun x -> { x with Input = p }) model2), Cmd.ofMsg (Send id)
            | None -> model2, Cmd.none

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
        | Some(id, ox, oy), _ -> (mapWidget id (fun w -> { w with PosX = x - ox; PosY = y - oy }) model), Cmd.none
        | None, Some id ->
            (mapWidget id (fun w -> { w with Width = max 260.0 (x - w.PosX); Height = max 220.0 (y - w.PosY) }) model),
            Cmd.none
        | None, None -> model, Cmd.none
    | PointerUp -> { model with Drag = None; Resize = None }, Cmd.none
    | Minimize id -> (mapWidget id (fun w -> { w with Minimized = true }) model), Cmd.none
    | Restore id ->
        let z = model.TopZ + 1
        { (mapWidget id (fun w -> { w with Minimized = false; Z = z }) model) with TopZ = z }, Cmd.none
    | RequestClose id -> { model with Closing = Some id }, Cmd.none
    | CloseWith(id, policy) ->
        let shared =
            match policy with
            | Discard -> model.SharedContext
            | Merge ->
                match model.Widgets |> List.tryFind (fun w -> w.Id = id) with
                | Some w when not (List.isEmpty w.Messages) -> model.SharedContext @ [ SideShift.Api.mergeSummary w ]
                | _ -> model.SharedContext
        { model with
            Widgets = model.Widgets |> List.filter (fun w -> w.Id <> id)
            Closing = None
            SharedContext = shared }, Cmd.none
    | Merged _ -> model, Cmd.none

    // --- chat ---
    | InputChanged(id, s) -> (mapWidget id (fun w -> { w with Input = s }) model), Cmd.none
    | Send id ->
        match model.Widgets |> List.tryFind (fun w -> w.Id = id) with
        | Some w when w.Input.Trim() <> "" ->
            match SideShift.Api.routeFor model w.Mode with
            | Some(provider, apiKey, modelId, via) ->
                let text = w.Input.Trim()
                let system = SideShift.Api.systemFor w.Mode model.SharedContext
                let req = SideShift.Api.buildReq provider apiKey modelId system w text
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
        (mapWidget id
            (fun w ->
                { w with
                    Messages = w.Messages @ [ { Role = "assistant"; Text = w.StreamBuf } ]
                    StreamBuf = ""
                    Streaming = false })
            model), Cmd.none
    | StreamError(id, m) -> (mapWidget id (fun w -> { w with Streaming = false; Error = Some m }) model), Cmd.none
