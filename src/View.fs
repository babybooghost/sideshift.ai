module SideShift.View

open Fable.Core.JsInterop
open Feliz
open SideShift.Types
open SideShift.Interop

let private px (v: float) = sprintf "%gpx" v

// Toggle Electron mouse pass-through so only widgets are clickable.
let private hoverProps =
    [ prop.onMouseEnter (fun _ -> setIgnoreMouse false)
      prop.onMouseLeave (fun _ -> setIgnoreMouse true) ]

let private isCodey (s: string) =
    s.Contains("```") || s.Contains("@@ ") || s.Contains("\n    ") || s.StartsWith("---")

// ---- API key modal ---------------------------------------------------------
let private keyModal (model: Model) dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.custom ("inset", "0"); style.display.flex
                     style.alignItems.center; style.justifyContent.center
                     style.custom ("background", "rgba(10,10,15,0.55)"); style.custom ("backdropFilter", "blur(3px)") ]
        yield! hoverProps
        prop.children [
            Html.div [
                prop.style [ style.width 380; style.custom ("background", "#16161d"); style.borderRadius 14
                             style.padding 22; style.color "#e6e6ef"
                             style.custom ("boxShadow", "0 20px 60px rgba(0,0,0,0.5)")
                             style.custom ("border", "1px solid #2a2a37") ]
                prop.children [
                    Html.h3 [ prop.style [ style.margin 0; style.marginBottom 6 ]; prop.text "SideShift AI" ]
                    Html.p [ prop.style [ style.custom ("color", "#9a9aad"); style.fontSize 13; style.marginTop 0 ]
                             prop.text "Paste your Anthropic API key. Stored encrypted on this machine only." ]
                    Html.input [
                        prop.type' "password"
                        prop.placeholder "sk-ant-..."
                        prop.value model.KeyDraft
                        prop.onChange (fun (v: string) -> dispatch (KeyDraftChanged v))
                        prop.style [ style.width (length.percent 100); style.padding 10; style.borderRadius 8
                                     style.custom ("border", "1px solid #33333f"); style.custom ("background", "#0e0e14")
                                     style.color "#fff"; style.boxSizing.borderBox ]
                    ]
                    Html.button [
                        prop.onClick (fun _ -> dispatch SaveKey)
                        prop.text "Save key"
                        prop.style [ style.marginTop 12; style.width (length.percent 100); style.padding 10
                                     style.borderRadius 8; style.custom ("border", "none"); style.custom ("background", "#6366f1")
                                     style.color "#fff"; style.cursor.pointer; style.fontWeight 600 ]
                    ]
                ]
            ]
        ]
    ]

// ---- capture region selector ----------------------------------------------
let private CaptureSelector =
    React.functionComponent (fun (props: {| dispatch: Msg -> unit |}) ->
        let start, setStart = React.useState (None: (float * float) option)
        let rect, setRect = React.useState (None: (float * float * float * float) option)
        Html.div [
            prop.className "ss-interactive"
            prop.style [ style.position.fixedRelativeToWindow; style.custom ("inset", "0"); style.cursor "crosshair"
                         style.custom ("background", "rgba(10,12,20,0.20)") ]
            yield! hoverProps
            prop.onMouseDown (fun e -> setStart (Some(e.clientX, e.clientY)); setRect None)
            prop.onMouseMove (fun e ->
                match start with
                | Some(sx, sy) ->
                    setRect (Some(min sx e.clientX, min sy e.clientY, abs (e.clientX - sx), abs (e.clientY - sy)))
                | None -> ())
            prop.onMouseUp (fun _ ->
                match rect with
                | Some(x, y, w, h) -> props.dispatch (RegionDrawn(x, y, w, h))
                | None -> props.dispatch CaptureCancelled
                setStart None
                setRect None)
            prop.children [
                Html.div [
                    prop.style [ style.position.absolute; style.top 14; style.left (length.percent 50)
                                 style.transform.translate (length.percent -50, length.px 0)
                                 style.custom ("background", "#16161d"); style.color "#cfcfe0"; style.padding (8, 14)
                                 style.borderRadius 20; style.fontSize 13; style.custom ("border", "1px solid #2a2a37") ]
                    prop.text "Drag a box over any text or code · Esc to cancel"
                ]
                match rect with
                | Some(x, y, w, h) ->
                    Html.div [
                        prop.style [ style.position.absolute; style.custom ("left", px x); style.custom ("top", px y)
                                     style.custom ("width", px w); style.custom ("height", px h)
                                     style.custom ("border", "2px solid #6366f1")
                                     style.custom ("background", "rgba(99,102,241,0.12)"); style.borderRadius 4 ]
                    ]
                | None -> Html.none
            ]
        ])

// ---- action bar after a region is drawn -----------------------------------
let private pendingBar (cap: Capture) (ax: float) (ay: float) dispatch =
    let btn (label: string) (mode: WidgetMode) =
        Html.button [
            prop.text label
            prop.onClick (fun _ -> dispatch (QuickAction mode))
            prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#e6e6ef"
                         style.padding (6, 10); style.cursor.pointer; style.borderRadius 6; style.fontSize 13 ]
        ]
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.custom ("left", px (min ax 1100.0))
                     style.custom ("top", px (max (ay - 44.0) 8.0)); style.display.flex; style.custom ("gap", "2px")
                     style.custom ("background", "#16161d"); style.custom ("border", "1px solid #2a2a37")
                     style.borderRadius 10; style.padding 4; style.custom ("boxShadow", "0 8px 30px rgba(0,0,0,0.45)")
                     style.custom ("zIndex", "2000000") ]
        yield! hoverProps
        prop.children [
            Html.span [ prop.style [ style.fontSize 15; style.padding (6, 6) ]; prop.text "⚡" ]
            btn "Ask" Ask
            btn "ELI5" ELI5
            btn "Verify" Verify
            btn "Diff" Diff
            Html.button [
                prop.text "✕"
                prop.onClick (fun _ -> dispatch DismissPending)
                prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#7a7a8d"
                             style.cursor.pointer; style.padding (6, 8) ]
            ]
        ]
    ]

// ---- a single message bubble ----------------------------------------------
let private bubble (m: ChatMsg) dispatch =
    let mine = m.Role = "user"
    let code = (not mine) && isCodey m.Text
    Html.div [
        prop.style [ style.display.flex; style.custom ("justifyContent", (if mine then "flex-end" else "flex-start"))
                     style.marginBottom 8 ]
        prop.children [
            Html.div [
                prop.style [ style.maxWidth (length.percent 88); style.padding (8, 10); style.borderRadius 10
                             style.fontSize 13; style.custom ("whiteSpace", "pre-wrap"); style.custom ("wordBreak", "break-word")
                             style.custom ("fontFamily", (if code then "ui-monospace, SFMono-Regular, Menlo, monospace" else "inherit"))
                             style.custom ("background", (if mine then "#6366f1" else "#20202b"))
                             style.color (if mine then "#fff" else "#e6e6ef") ]
                prop.children [
                    Html.text m.Text
                    if code then
                        Html.button [
                            prop.text "copy"
                            prop.onClick (fun _ -> copy m.Text)
                            prop.style [ style.display.block; style.marginTop 6; style.fontSize 11; style.cursor.pointer
                                         style.custom ("border", "1px solid #3a3a48"); style.custom ("background", "transparent")
                                         style.color "#a5a5ba"; style.borderRadius 5; style.padding (2, 6) ]
                        ]
                ]
            ]
        ]
    ]

// ---- a widget panel --------------------------------------------------------
let private widgetView (model: Model) (w: Widget) dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.absolute; style.custom ("left", px w.PosX); style.custom ("top", px w.PosY)
                     style.custom ("width", px w.Width); style.custom ("height", px w.Height)
                     style.custom ("zIndex", string w.Z); style.display.flex; style.flexDirection.column
                     style.custom ("background", "#121218"); style.borderRadius 12
                     style.custom ("border", sprintf "1px solid %s" w.Color)
                     style.custom ("boxShadow", "0 16px 50px rgba(0,0,0,0.5)"); style.overflow.hidden ]
        yield! hoverProps
        prop.onMouseDown (fun _ -> dispatch (Focus w.Id))
        prop.children [
            // header / drag handle
            Html.div [
                prop.style [ style.display.flex; style.alignItems.center; style.custom ("gap", "6px")
                             style.padding (8, 10); style.custom ("background", w.Color); style.color "#fff"
                             style.cursor "grab"; style.custom ("userSelect", "none") ]
                prop.onMouseDown (fun e -> dispatch (StartDrag(w.Id, e.clientX - w.PosX, e.clientY - w.PosY)))
                prop.children [
                    Html.span [ prop.style [ style.fontWeight 700; style.fontSize 13 ]; prop.text w.Title ]
                    Html.div [ prop.style [ style.custom ("flex", "1") ] ]
                    Html.button [
                        prop.text "—"
                        prop.onClick (fun e -> e.stopPropagation (); dispatch (Minimize w.Id))
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#fff"
                                     style.cursor.pointer; style.fontWeight 700 ]
                    ]
                    Html.button [
                        prop.text "✕"
                        prop.onClick (fun e -> e.stopPropagation (); dispatch (RequestClose w.Id))
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#fff"
                                     style.cursor.pointer; style.fontWeight 700 ]
                    ]
                ]
            ]
            // context thumbnail
            Html.div [
                prop.style [ style.padding (6, 10); style.custom ("borderBottom", "1px solid #22222c") ]
                prop.children [
                    Html.img [
                        prop.src w.Capture.ImageDataUrl
                        prop.style [ style.maxWidth (length.percent 100); style.maxHeight 90; style.borderRadius 6
                                     style.custom ("border", "1px solid #2a2a37"); style.display.block ]
                    ]
                ]
            ]
            // conversation
            Html.div [
                prop.style [ style.custom ("flex", "1"); style.overflowY.auto; style.padding 10 ]
                prop.children [
                    yield! (w.Messages |> List.map (fun m -> bubble m dispatch))
                    if w.Streaming then
                        bubble { Role = "assistant"; Text = (if w.StreamBuf = "" then "…" else w.StreamBuf) } dispatch
                    match w.Error with
                    | Some e ->
                        Html.div [ prop.style [ style.color "#ff6b6b"; style.fontSize 12 ]; prop.text ("Error: " + e) ]
                    | None -> Html.none
                ]
            ]
            // input row
            Html.div [
                prop.style [ style.display.flex; style.custom ("gap", "6px"); style.padding 8
                             style.custom ("borderTop", "1px solid #22222c") ]
                prop.children [
                    Html.textarea [
                        prop.value w.Input
                        prop.placeholder "Ask a follow-up…"
                        prop.rows 1
                        prop.onChange (fun (v: string) -> dispatch (InputChanged(w.Id, v)))
                        prop.onKeyDown (fun e ->
                            if e.key = "Enter" && not e.shiftKey then
                                e.preventDefault ()
                                dispatch (Send w.Id))
                        prop.style [ style.custom ("flex", "1"); style.resize.none; style.padding 8; style.borderRadius 8
                                     style.custom ("border", "1px solid #2a2a37"); style.custom ("background", "#0e0e14")
                                     style.color "#fff"; style.fontSize 13; style.boxSizing.borderBox ]
                    ]
                    Html.button [
                        prop.text "↑"
                        prop.disabled w.Streaming
                        prop.onClick (fun _ -> dispatch (Send w.Id))
                        prop.style [ style.custom ("border", "none"); style.custom ("background", w.Color); style.color "#fff"
                                     style.borderRadius 8; style.width 40; style.cursor.pointer; style.fontWeight 700 ]
                    ]
                ]
            ]
            // close menu overlay
            match model.Closing with
            | Some cid when cid = w.Id ->
                Html.div [
                    prop.style [ style.position.absolute; style.custom ("inset", "0"); style.display.flex
                                 style.flexDirection.column; style.alignItems.center; style.justifyContent.center
                                 style.custom ("gap", "10px"); style.custom ("background", "rgba(10,10,15,0.82)") ]
                    prop.children [
                        Html.div [ prop.style [ style.color "#cfcfe0"; style.fontSize 13 ]; prop.text "Close this side-quest?" ]
                        Html.div [
                            prop.style [ style.display.flex; style.custom ("gap", "8px") ]
                            prop.children [
                                Html.button [
                                    prop.text "Merge to context"
                                    prop.onClick (fun _ -> dispatch (CloseWith(w.Id, Merge)))
                                    prop.style [ style.custom ("border", "none"); style.custom ("background", "#10b981")
                                                 style.color "#fff"; style.padding (8, 12); style.borderRadius 8; style.cursor.pointer ]
                                ]
                                Html.button [
                                    prop.text "Discard"
                                    prop.onClick (fun _ -> dispatch (CloseWith(w.Id, Discard)))
                                    prop.style [ style.custom ("border", "1px solid #3a3a48"); style.custom ("background", "transparent")
                                                 style.color "#e6e6ef"; style.padding (8, 12); style.borderRadius 8; style.cursor.pointer ]
                                ]
                            ]
                        ]
                    ]
                ]
            | _ -> Html.none
        ]
    ]

// ---- margin minimap (minimized widgets docked on the right edge) -----------
let private minimap (model: Model) dispatch =
    let mins = model.Widgets |> List.filter (fun w -> w.Minimized)
    if List.isEmpty mins then Html.none
    else
        Html.div [
            prop.className "ss-interactive"
            prop.style [ style.position.fixedRelativeToWindow; style.right 6; style.top (length.percent 30)
                         style.display.flex; style.flexDirection.column; style.custom ("gap", "8px") ]
            yield! hoverProps
            prop.children [
                yield! (mins |> List.map (fun w ->
                    Html.div [
                        prop.title (sprintf "%s — click to reopen" w.Title)
                        prop.onClick (fun _ -> dispatch (Restore w.Id))
                        prop.style [ style.width 16; style.height 16; style.borderRadius 8; style.cursor.pointer
                                     style.custom ("background", w.Color)
                                     style.custom ("boxShadow", sprintf "0 0 0 2px rgba(255,255,255,0.15)") ]
                    ]))
            ]
        ]

// ---- control dock (visible entry point besides the hotkey) -----------------
let private dock (model: Model) dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.right 16; style.bottom 16; style.display.flex
                     style.custom ("gap", "8px"); style.alignItems.center ]
        yield! hoverProps
        prop.children [
            Html.button [
                prop.title "Highlight a region (⌘⇧Space)"
                prop.text "⚡ Capture"
                prop.onClick (fun _ -> dispatch ToggleCapture)
                prop.style [ style.custom ("border", "none"); style.custom ("background", "#6366f1"); style.color "#fff"
                             style.padding (10, 14); style.borderRadius 24; style.cursor.pointer; style.fontWeight 600
                             style.custom ("boxShadow", "0 8px 30px rgba(99,102,241,0.5)") ]
            ]
            Html.button [
                prop.title "API key"
                prop.text "⚙"
                prop.onClick (fun _ -> dispatch ShowKeyPrompt)
                prop.style [ style.custom ("border", "none"); style.custom ("background", "#20202b"); style.color "#cfcfe0"
                             style.width 40; style.height 40; style.borderRadius 20; style.cursor.pointer ]
            ]
        ]
    ]

// ---- root ------------------------------------------------------------------
let view (model: Model) dispatch =
    Html.div [
        prop.children [
            // drag catch layer
            match model.Drag with
            | Some _ ->
                Html.div [
                    prop.className "ss-interactive"
                    prop.style [ style.position.fixedRelativeToWindow; style.custom ("inset", "0"); style.custom ("zIndex", "3000000") ]
                    prop.onMouseMove (fun e -> dispatch (DragMove(e.clientX, e.clientY)))
                    prop.onMouseUp (fun _ -> dispatch EndDrag)
                ]
            | None -> Html.none

            yield! (model.Widgets |> List.filter (fun w -> not w.Minimized) |> List.map (fun w -> widgetView model w dispatch))

            minimap model dispatch

            match model.Pending with
            | Some(cap, ax, ay) -> pendingBar cap ax ay dispatch
            | None -> Html.none

            if model.CaptureMode then CaptureSelector {| dispatch = dispatch |} else Html.none

            dock model dispatch

            if model.ShowKeyPrompt then keyModal model dispatch else Html.none
        ]
    ]
