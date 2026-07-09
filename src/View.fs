module SideShift.View

open Feliz
open SideShift.Types
open SideShift.Interop

let private px (v: float) = sprintf "%gpx" v

// ---- theme tokens (warm gold/orange over warm-dark surfaces) ----------------
let private surface = "#17130E"
let private raised = "#1F1913"
let private chip = "#241C14"
let private inputBg = "#14100B"
let private border = "#33291D"
let private borderSoft = "#2A2016"
let private textPri = "#F3ECE0"
let private textMut = "#A89A86"
let private textSec = "#D9CDBB"
let private gold = "#F5B23B"
let private orange = "#E4571E"
let private grad = "linear-gradient(135deg, #F5B23B 0%, #E4571E 100%)"
let private shadowLg = "0 18px 50px rgba(24,14,6,0.55)"
let private shadowMd = "0 8px 28px rgba(24,14,6,0.5)"

let private hoverProps =
    [ prop.onMouseEnter (fun _ -> setIgnoreMouse false)
      prop.onMouseLeave (fun _ -> setIgnoreMouse true) ]

let private isCodey (s: string) =
    s.Contains("```") || s.Contains("@@ ") || s.Contains("\n    ") || s.StartsWith("---")

// Small brand mark echoing the app icon: gradient rounded square + "S".
let private brandBadge (size: int) =
    Html.div [
        prop.style [ style.width size; style.height size; style.borderRadius (size / 4)
                     style.custom ("background", grad); style.display.flex; style.alignItems.center
                     style.justifyContent.center; style.color "#FBF6EC"; style.fontWeight 800
                     style.fontSize (int (float size * 0.6)); style.lineHeight 1
                     style.custom ("boxShadow", "0 2px 10px rgba(228,87,30,0.45)") ]
        prop.children [ Html.span [ prop.text "S" ] ]
    ]

let private gradButton (label: string) (onClick: unit -> unit) =
    Html.button [
        prop.onClick (fun _ -> onClick ())
        prop.text label
        prop.style [ style.custom ("border", "none"); style.custom ("background", grad); style.color "#FFF7EC"
                     style.padding (10, 16); style.borderRadius 10; style.cursor.pointer; style.fontWeight 700
                     style.custom ("boxShadow", "0 6px 20px rgba(228,87,30,0.4)") ]
    ]

// ---- settings modal --------------------------------------------------------
let private field (label: string) (placeholder: string) (value: string) (onChange: string -> unit) =
    Html.div [
        prop.style [ style.marginTop 14 ]
        prop.children [
            Html.label [ prop.style [ style.fontSize 12; style.custom ("color", textMut); style.custom ("letterSpacing", "0.02em") ]
                         prop.text label ]
            Html.input [
                prop.type' "password"
                prop.placeholder placeholder
                prop.value value
                prop.onChange onChange
                prop.style [ style.width (length.percent 100); style.padding 10; style.borderRadius 9; style.marginTop 5
                             style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                             style.color textPri; style.boxSizing.borderBox; style.fontSize 13 ]
            ]
        ]
    ]

let private settingsModal (model: Model) dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.custom ("inset", "0"); style.display.flex
                     style.alignItems.center; style.justifyContent.center
                     style.custom ("background", "rgba(20,14,8,0.6)"); style.custom ("backdropFilter", "blur(4px)") ]
        yield! hoverProps
        prop.children [
            Html.div [
                prop.style [ style.width 440; style.custom ("background", raised); style.borderRadius 16; style.padding 26
                             style.color textPri; style.custom ("border", sprintf "1px solid %s" border)
                             style.custom ("boxShadow", shadowLg) ]
                prop.children [
                    Html.div [
                        prop.style [ style.display.flex; style.alignItems.center; style.custom ("gap", "12px") ]
                        prop.children [
                            brandBadge 34
                            Html.div [
                                prop.children [
                                    Html.div [ prop.style [ style.fontWeight 800; style.fontSize 17; style.custom ("letterSpacing", "0.14em") ]
                                               prop.text "SIDESHIFT" ]
                                    Html.div [ prop.style [ style.fontSize 10; style.custom ("color", gold); style.custom ("letterSpacing", "0.22em") ]
                                               prop.text "DYNAMIC MOTION & TRANSITION" ]
                                ]
                            ]
                        ]
                    ]
                    Html.p [ prop.style [ style.custom ("color", textMut); style.fontSize 13; style.marginTop 14; style.lineHeight 1.5 ]
                             prop.text "Keys stored encrypted on this machine only. Anthropic runs capture/answers; OpenRouter is optional and powers Verify's independent cross-model critic." ]
                    field "Anthropic API key (required)" "sk-ant-..." model.AnthropicDraft (fun v -> dispatch (AnthropicDraftChanged v))
                    field "OpenRouter API key (optional)" "sk-or-..." model.OpenRouterDraft (fun v -> dispatch (OpenRouterDraftChanged v))
                    Html.div [
                        prop.style [ style.marginTop 14 ]
                        prop.children [
                            Html.label [ prop.style [ style.fontSize 12; style.custom ("color", textMut) ]
                                         prop.text "Verify critic model (OpenRouter id)" ]
                            Html.input [
                                prop.type' "text"
                                prop.placeholder "openai/gpt-4o"
                                prop.value model.CriticDraft
                                prop.onChange (fun (v: string) -> dispatch (CriticDraftChanged v))
                                prop.style [ style.width (length.percent 100); style.padding 10; style.borderRadius 9; style.marginTop 5
                                             style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                                             style.color textPri; style.boxSizing.borderBox; style.fontSize 13 ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.style [ style.display.flex; style.custom ("gap", "10px"); style.marginTop 20 ]
                        prop.children [
                            Html.div [ prop.style [ style.custom ("flex", "1") ]; prop.children [ gradButton "Save" (fun () -> dispatch SaveSettings) ] ]
                            Html.button [
                                prop.onClick (fun _ -> dispatch CloseSettings)
                                prop.text "Cancel"
                                prop.style [ style.padding (10, 16); style.borderRadius 10; style.custom ("border", sprintf "1px solid %s" border)
                                             style.custom ("background", "transparent"); style.color textSec; style.cursor.pointer ]
                            ]
                        ]
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
                         style.custom ("background", "rgba(20,14,8,0.22)") ]
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
                    prop.style [ style.position.absolute; style.top 16; style.left (length.percent 50)
                                 style.transform.translate (length.percent -50, length.px 0)
                                 style.custom ("background", raised); style.color textSec; style.padding (9, 16)
                                 style.borderRadius 22; style.fontSize 13; style.custom ("border", sprintf "1px solid %s" border)
                                 style.custom ("boxShadow", shadowMd) ]
                    prop.text "Drag a box over any text or code · Esc to cancel"
                ]
                match rect with
                | Some(x, y, w, h) ->
                    Html.div [
                        prop.style [ style.position.absolute; style.custom ("left", px x); style.custom ("top", px y)
                                     style.custom ("width", px w); style.custom ("height", px h)
                                     style.custom ("border", sprintf "2px solid %s" gold)
                                     style.custom ("background", "rgba(245,178,59,0.12)"); style.borderRadius 4 ]
                    ]
                | None -> Html.none
            ]
        ])

// ---- action bar after a region is drawn -----------------------------------
let private pendingBar (ax: float) (ay: float) dispatch =
    let btn (label: string) (mode: WidgetMode) =
        Html.button [
            prop.text label
            prop.onClick (fun _ -> dispatch (QuickAction mode))
            prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color textPri
                         style.padding (7, 11); style.cursor.pointer; style.borderRadius 7; style.fontSize 13; style.fontWeight 600 ]
        ]
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.custom ("left", px (min ax 1100.0))
                     style.custom ("top", px (max (ay - 46.0) 8.0)); style.display.flex; style.alignItems.center
                     style.custom ("gap", "2px"); style.custom ("background", raised); style.custom ("border", sprintf "1px solid %s" border)
                     style.borderRadius 12; style.padding 5; style.custom ("boxShadow", shadowMd); style.custom ("zIndex", "2000000") ]
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
                prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#8A7C68"
                             style.cursor.pointer; style.padding (6, 8) ]
            ]
        ]
    ]

// ---- message bubble --------------------------------------------------------
let private bubble (m: ChatMsg) =
    let mine = m.Role = "user"
    let code = (not mine) && isCodey m.Text
    Html.div [
        prop.style [ style.display.flex; style.custom ("justifyContent", (if mine then "flex-end" else "flex-start"))
                     style.marginBottom 8 ]
        prop.children [
            Html.div [
                prop.style [ style.maxWidth (length.percent 88); style.padding (9, 11); style.borderRadius 11
                             style.fontSize 13; style.lineHeight 1.5; style.custom ("whiteSpace", "pre-wrap")
                             style.custom ("wordBreak", "break-word")
                             style.custom ("fontFamily", (if code then "ui-monospace, SFMono-Regular, Menlo, monospace" else "inherit"))
                             style.custom ("background", (if mine then grad else chip))
                             style.color (if mine then "#FFF7EC" else textPri) ]
                prop.children [
                    Html.text m.Text
                    if code then
                        Html.button [
                            prop.text "copy"
                            prop.onClick (fun _ -> copy m.Text)
                            prop.style [ style.display.block; style.marginTop 7; style.fontSize 11; style.cursor.pointer
                                         style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent")
                                         style.color "#B7A88F"; style.borderRadius 6; style.padding (2, 7) ]
                        ]
                ]
            ]
        ]
    ]

// ---- widget panel ----------------------------------------------------------
let private widgetView (model: Model) (w: Widget) dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.absolute; style.custom ("left", px w.PosX); style.custom ("top", px w.PosY)
                     style.custom ("width", px w.Width); style.custom ("height", px w.Height)
                     style.custom ("zIndex", string w.Z); style.display.flex; style.flexDirection.column
                     style.custom ("background", surface); style.borderRadius 14
                     style.custom ("border", sprintf "1px solid %s" border)
                     style.custom ("boxShadow", shadowLg); style.overflow.hidden ]
        yield! hoverProps
        prop.onMouseDown (fun _ -> dispatch (Focus w.Id))
        prop.children [
            // header / drag handle
            Html.div [
                prop.style [ style.display.flex; style.alignItems.center; style.custom ("gap", "7px")
                             style.padding (9, 11)
                             style.custom ("background", sprintf "linear-gradient(135deg, %s 0%%, %s 130%%)" w.Color "#7A3A12")
                             style.color "#FFF7EC"; style.cursor "grab"; style.custom ("userSelect", "none") ]
                prop.onMouseDown (fun e -> dispatch (StartDrag(w.Id, e.clientX - w.PosX, e.clientY - w.PosY)))
                prop.children [
                    Html.span [ prop.style [ style.fontWeight 700; style.fontSize 13; style.custom ("letterSpacing", "0.02em") ]; prop.text w.Title ]
                    if w.Via <> "" then
                        Html.span [ prop.style [ style.fontSize 10; style.custom ("opacity", "0.85")
                                                 style.custom ("background", "rgba(0,0,0,0.28)"); style.padding (1, 7); style.borderRadius 8 ]
                                    prop.text w.Via ]
                    Html.div [ prop.style [ style.custom ("flex", "1") ] ]
                    Html.button [
                        prop.text "—"
                        prop.onClick (fun e -> e.stopPropagation (); dispatch (Minimize w.Id))
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#FFF7EC"
                                     style.cursor.pointer; style.fontWeight 700 ]
                    ]
                    Html.button [
                        prop.text "✕"
                        prop.onClick (fun e -> e.stopPropagation (); dispatch (RequestClose w.Id))
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color "#FFF7EC"
                                     style.cursor.pointer; style.fontWeight 700 ]
                    ]
                ]
            ]
            // context thumbnail
            Html.div [
                prop.style [ style.padding (7, 11); style.custom ("borderBottom", sprintf "1px solid %s" borderSoft) ]
                prop.children [
                    Html.img [
                        prop.src w.Capture.ImageDataUrl
                        prop.style [ style.maxWidth (length.percent 100); style.maxHeight 90; style.borderRadius 7
                                     style.custom ("border", sprintf "1px solid %s" border); style.display.block ]
                    ]
                ]
            ]
            // conversation
            Html.div [
                prop.style [ style.custom ("flex", "1"); style.overflowY.auto; style.padding 11 ]
                prop.children [
                    yield! (w.Messages |> List.map bubble)
                    if w.Streaming then bubble { Role = "assistant"; Text = (if w.StreamBuf = "" then "…" else w.StreamBuf) }
                    match w.Error with
                    | Some e -> Html.div [ prop.style [ style.color "#F08A5D"; style.fontSize 12 ]; prop.text ("Error: " + e) ]
                    | None -> Html.none
                ]
            ]
            // input row
            Html.div [
                prop.style [ style.display.flex; style.custom ("gap", "7px"); style.padding 9
                             style.custom ("borderTop", sprintf "1px solid %s" borderSoft) ]
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
                        prop.style [ style.custom ("flex", "1"); style.resize.none; style.padding 9; style.borderRadius 9
                                     style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                                     style.color textPri; style.fontSize 13; style.boxSizing.borderBox ]
                    ]
                    Html.button [
                        prop.text "↑"
                        prop.disabled w.Streaming
                        prop.onClick (fun _ -> dispatch (Send w.Id))
                        prop.style [ style.custom ("border", "none"); style.custom ("background", grad); style.color "#FFF7EC"
                                     style.borderRadius 9; style.width 42; style.cursor.pointer; style.fontWeight 700 ]
                    ]
                ]
            ]
            // resize handle (bottom-right)
            Html.div [
                prop.onMouseDown (fun e -> e.stopPropagation (); dispatch (StartResize w.Id))
                prop.style [ style.position.absolute; style.right 0; style.bottom 0; style.width 16; style.height 16
                             style.cursor "nwse-resize"
                             style.custom ("background", "linear-gradient(135deg, transparent 50%, " + w.Color + " 50%)") ]
            ]
            // close menu overlay
            match model.Closing with
            | Some cid when cid = w.Id ->
                Html.div [
                    prop.style [ style.position.absolute; style.custom ("inset", "0"); style.display.flex
                                 style.flexDirection.column; style.alignItems.center; style.justifyContent.center
                                 style.custom ("gap", "12px"); style.custom ("background", "rgba(20,14,8,0.86)") ]
                    prop.children [
                        Html.div [ prop.style [ style.color textSec; style.fontSize 13 ]; prop.text "Close this side-quest?" ]
                        Html.div [
                            prop.style [ style.display.flex; style.custom ("gap", "9px") ]
                            prop.children [
                                Html.button [
                                    prop.text "Merge to context"
                                    prop.onClick (fun _ -> dispatch (CloseWith(w.Id, Merge)))
                                    prop.style [ style.custom ("border", "none"); style.custom ("background", "#2FA36B")
                                                 style.color "#FFF"; style.padding (9, 13); style.borderRadius 9; style.cursor.pointer; style.fontWeight 600 ]
                                ]
                                Html.button [
                                    prop.text "Discard"
                                    prop.onClick (fun _ -> dispatch (CloseWith(w.Id, Discard)))
                                    prop.style [ style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent")
                                                 style.color textPri; style.padding (9, 13); style.borderRadius 9; style.cursor.pointer ]
                                ]
                            ]
                        ]
                    ]
                ]
            | _ -> Html.none
        ]
    ]

// ---- margin minimap --------------------------------------------------------
let private minimap (model: Model) dispatch =
    let mins = model.Widgets |> List.filter (fun w -> w.Minimized)
    if List.isEmpty mins then Html.none
    else
        Html.div [
            prop.className "ss-interactive"
            prop.style [ style.position.fixedRelativeToWindow; style.right 7; style.top (length.percent 30)
                         style.display.flex; style.flexDirection.column; style.custom ("gap", "9px") ]
            yield! hoverProps
            prop.children [
                yield! (mins |> List.map (fun w ->
                    Html.div [
                        prop.title (sprintf "%s — click to reopen" w.Title)
                        prop.onClick (fun _ -> dispatch (Restore w.Id))
                        prop.style [ style.width 16; style.height 16; style.borderRadius 8; style.cursor.pointer
                                     style.custom ("background", w.Color)
                                     style.custom ("boxShadow", "0 0 0 2px rgba(255,247,236,0.15)") ]
                    ]))
            ]
        ]

// ---- control dock ----------------------------------------------------------
let private dock dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.right 18; style.bottom 18; style.display.flex
                     style.custom ("gap", "9px"); style.alignItems.center ]
        yield! hoverProps
        prop.children [
            Html.button [
                prop.title "Highlight a region (⌘⇧Space)"
                prop.onClick (fun _ -> dispatch ToggleCapture)
                prop.style [ style.custom ("border", "none"); style.custom ("background", grad); style.color "#FFF7EC"
                             style.padding (11, 16); style.borderRadius 26; style.cursor.pointer; style.fontWeight 700
                             style.display.flex; style.alignItems.center; style.custom ("gap", "8px")
                             style.custom ("boxShadow", "0 8px 26px rgba(228,87,30,0.45)") ]
                prop.children [
                    Html.span [ prop.text "⚡" ]
                    Html.span [ prop.text "Capture" ]
                ]
            ]
            Html.button [
                prop.title "Settings / API keys"
                prop.text "⚙"
                prop.onClick (fun _ -> dispatch OpenSettings)
                prop.style [ style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", raised); style.color textSec
                             style.width 42; style.height 42; style.borderRadius 21; style.cursor.pointer ]
            ]
        ]
    ]

// ---- root ------------------------------------------------------------------
let view (model: Model) dispatch =
    Html.div [
        prop.children [
            // pointer catch layer (drag OR resize)
            match model.Drag, model.Resize with
            | None, None -> Html.none
            | _ ->
                Html.div [
                    prop.className "ss-interactive"
                    prop.style [ style.position.fixedRelativeToWindow; style.custom ("inset", "0"); style.custom ("zIndex", "3000000") ]
                    prop.onMouseMove (fun e -> dispatch (PointerMove(e.clientX, e.clientY)))
                    prop.onMouseUp (fun _ -> dispatch PointerUp)
                ]

            yield! (model.Widgets |> List.filter (fun w -> not w.Minimized) |> List.map (fun w -> widgetView model w dispatch))

            minimap model dispatch

            match model.Pending with
            | Some(_, ax, ay) -> pendingBar ax ay dispatch
            | None -> Html.none

            if model.CaptureMode then CaptureSelector {| dispatch = dispatch |} else Html.none

            dock dispatch

            if model.ShowSettings then settingsModal model dispatch else Html.none
        ]
    ]
