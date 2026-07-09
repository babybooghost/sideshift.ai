module SideShift.View

open Feliz
open SideShift.Types
open SideShift.Interop

let private px (v: float) = sprintf "%gpx" v

// ---- theme tokens: all colors come from CSS vars set at the root per Theme, so
// flipping dark/light re-paints everything (warm off-white light, not pure white).
let private raised = "var(--ss-raised)"
let private chip = "var(--ss-chip)"
let private inputBg = "var(--ss-input)"
let private border = "var(--ss-border)"
let private borderSoft = "var(--ss-borderSoft)"
let private textPri = "var(--ss-textPri)"
let private textMut = "var(--ss-textMut)"
let private textSec = "var(--ss-textSec)"
let private track = "var(--ss-track)"
let private shadow = "var(--ss-shadow)"
let private accent = "var(--accent)"      // single accent, driven by --accent at the root
let private mono = "ui-monospace, SFMono-Regular, Menlo, monospace"

/// Full CSS-var set for a theme (name, value) — stamped on the root div.
let private themeVars =
    function
    | Dark ->
        [ "--ss-raised", "#1E1912"; "--ss-chip", "#231D15"; "--ss-input", "#120E0A"
          "--ss-border", "#2E271C"; "--ss-borderSoft", "#241E16"
          "--ss-textPri", "#F1EADD"; "--ss-textMut", "#9C8F7C"; "--ss-textSec", "#CDBFA9"
          "--ss-track", "#2A2016"; "--ss-shadow", "0 20px 52px rgba(0,0,0,0.5)" ]
    | Light ->
        [ "--ss-raised", "#FFFFFF"; "--ss-chip", "#F2ECE0"; "--ss-input", "#FFFFFF"
          "--ss-border", "#E6DCC9"; "--ss-borderSoft", "#EFE7D7"
          "--ss-textPri", "#2A2318"; "--ss-textMut", "#8A7E6A"; "--ss-textSec", "#5C513E"
          "--ss-track", "#E6DCC9"; "--ss-shadow", "0 16px 40px rgba(60,40,12,0.16)" ]

// Widget surface + backdrop-blur per opacity mode (Cluely-style glass), theme-aware.
let private surfaceVar theme opacity =
    let rgb = match theme with | Dark -> "22,18,13" | Light -> "251,247,239"
    match opacity with
    | Opaque -> (match theme with | Dark -> "#16120D" | Light -> "#FBF7EF")
    | Translucent -> sprintf "rgba(%s,0.74)" rgb
    | Transparent -> sprintf "rgba(%s,0.42)" rgb

let private blurVar =
    function
    | Opaque -> "0px"
    | Translucent -> "13px"
    | Transparent -> "8px"

let private accentSwatches =
    [ "#E4571E"; "#F5B23B"; "#4C86C6"; "#3FA35B"; "#8B5CF6"; "#E23B3B"; "#EC4899" ]

// Brand mark (S pierced by an arrow) — the one place gradient/gold appears.
let private markSvg =
    "<svg viewBox='0 0 1024 1024' width='100%' height='100%' xmlns='http://www.w3.org/2000/svg'>"
    + "<defs><linearGradient id='ssG' x1='330' y1='270' x2='700' y2='770' gradientUnits='userSpaceOnUse'>"
    + "<stop offset='0' stop-color='#F8C64C'/><stop offset='1' stop-color='#E68A26'/></linearGradient>"
    + "<linearGradient id='ssA' x1='200' y1='730' x2='850' y2='235' gradientUnits='userSpaceOnUse'>"
    + "<stop offset='0' stop-color='#D8481A'/><stop offset='1' stop-color='#F5732E'/></linearGradient></defs>"
    + "<path d='M 208 700 C 300 664 298 590 380 560 C 470 527 470 470 542 452 C 636 428 690 356 812 258' fill='none' stroke='url(#ssA)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/>"
    + "<path d='M 742 262 L 812 258 L 806 330' fill='none' stroke='url(#ssA)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/>"
    + "<g transform='translate(96 0) skewX(-11)'><path d='M 688 374 C 674 308 588 294 518 320 C 428 354 428 442 522 486 C 620 530 646 622 566 678 C 494 728 392 716 348 658' fill='none' stroke='url(#ssG)' stroke-width='100' stroke-linecap='round' stroke-linejoin='round'/></g>"
    + "<path d='M 560 442 C 636 414 690 356 812 258' fill='none' stroke='url(#ssA)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/>"
    + "<path d='M 742 262 L 812 258 L 806 330' fill='none' stroke='url(#ssA)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/></svg>"

// Inline SVG glyphs (currentColor), so no emoji-as-icon.
let private icoGear =
    "<svg viewBox='0 0 24 24' width='100%' height='100%' fill='none' stroke='currentColor' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='3'/><path d='M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z'/></svg>"

let private icoScan =
    "<svg viewBox='0 0 24 24' width='100%' height='100%' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M3 7V5a2 2 0 0 1 2-2h2'/><path d='M17 3h2a2 2 0 0 1 2 2v2'/><path d='M21 17v2a2 2 0 0 1-2 2h-2'/><path d='M7 21H5a2 2 0 0 1-2-2v-2'/></svg>"

let private icoSend =
    "<svg viewBox='0 0 24 24' width='100%' height='100%' fill='none' stroke='currentColor' stroke-width='2.4' stroke-linecap='round' stroke-linejoin='round'><path d='M12 19V5'/><path d='M5 12l7-7 7 7'/></svg>"

let private svgIco (markup: string) (size: int) =
    Html.span [
        prop.style [ style.display.inlineFlex; style.width size; style.height size; style.custom ("lineHeight", "0") ]
        prop.dangerouslySetInnerHTML markup
    ]

let private hoverProps =
    [ prop.onMouseEnter (fun _ -> setIgnoreMouse false)
      prop.onMouseLeave (fun _ -> setIgnoreMouse true) ]

let private isCodey (s: string) =
    s.Contains("```") || s.Contains("@@ ") || s.Contains("\n    ") || s.StartsWith("---")

let private colorDot (c: string) =
    Html.span [ prop.style [ style.width 7; style.height 7; style.borderRadius 4; style.custom ("background", c)
                             style.display.inlineBlock; style.custom ("flexShrink", "0") ] ]

// ---- settings modal --------------------------------------------------------
let private field (label: string) (placeholder: string) (value: string) (onChange: string -> unit) =
    Html.div [
        prop.style [ style.marginTop 14 ]
        prop.children [
            Html.label [ prop.style [ style.fontSize 11; style.custom ("color", textMut); style.custom ("letterSpacing", "0.04em")
                                      style.custom ("textTransform", "uppercase") ]
                         prop.text label ]
            Html.input [
                prop.type' "password"
                prop.placeholder placeholder
                prop.value value
                prop.onChange onChange
                prop.style [ style.width (length.percent 100); style.padding 10; style.borderRadius 8; style.marginTop 6
                             style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                             style.color textPri; style.boxSizing.borderBox; style.fontSize 13; style.custom ("fontFamily", mono) ]
            ]
        ]
    ]

let private settingsModal (model: Model) dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.custom ("inset", "0"); style.display.flex
                     style.alignItems.center; style.justifyContent.center
                     style.custom ("background", "rgba(14,10,6,0.62)"); style.custom ("backdropFilter", "blur(4px)") ]
        yield! hoverProps
        prop.children [
            Html.div [
                prop.style [ style.width 440; style.custom ("background", raised); style.borderRadius 14; style.padding 24
                             style.color textPri; style.custom ("border", sprintf "1px solid %s" border)
                             style.custom ("boxShadow", shadow) ]
                prop.children [
                    Html.div [
                        prop.style [ style.display.flex; style.alignItems.center; style.custom ("gap", "11px") ]
                        prop.children [
                            Html.div [ prop.style [ style.width 34; style.height 34 ]; prop.children [ svgIco markSvg 34 ] ]
                            Html.div [
                                prop.children [
                                    Html.div [ prop.style [ style.fontWeight 700; style.fontSize 16; style.custom ("letterSpacing", "0.16em") ]
                                               prop.text "SIDESHIFT" ]
                                    Html.div [ prop.style [ style.fontSize 9; style.custom ("color", textMut); style.custom ("letterSpacing", "0.28em")
                                                            style.custom ("textTransform", "uppercase"); style.marginTop 2 ]
                                               prop.text "dynamic motion & transition" ]
                                ]
                            ]
                        ]
                    ]
                    Html.p [ prop.style [ style.custom ("color", textMut); style.fontSize 12.5; style.marginTop 16; style.lineHeight 1.55 ]
                             prop.text "Keys stored encrypted on this machine only. Anthropic runs capture and answers; OpenRouter is optional and powers Verify's independent cross-model critic." ]
                    field "Anthropic API key · required" "sk-ant-..." model.AnthropicDraft (fun v -> dispatch (AnthropicDraftChanged v))
                    field "OpenRouter API key · optional" "sk-or-..." model.OpenRouterDraft (fun v -> dispatch (OpenRouterDraftChanged v))
                    Html.div [
                        prop.style [ style.marginTop 14 ]
                        prop.children [
                            Html.label [ prop.style [ style.fontSize 11; style.custom ("color", textMut); style.custom ("letterSpacing", "0.04em")
                                                      style.custom ("textTransform", "uppercase") ]
                                         prop.text "Verify critic model" ]
                            Html.input [
                                prop.type' "text"
                                prop.placeholder "openai/gpt-4o"
                                prop.value model.CriticDraft
                                prop.onChange (fun (v: string) -> dispatch (CriticDraftChanged v))
                                prop.style [ style.width (length.percent 100); style.padding 10; style.borderRadius 8; style.marginTop 6
                                             style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                                             style.color textPri; style.boxSizing.borderBox; style.fontSize 13; style.custom ("fontFamily", mono) ]
                            ]
                        ]
                    ]
                    // Web-grounded Verify toggle
                    Html.div [
                        prop.style [ style.display.flex; style.alignItems.center; style.justifyContent.spaceBetween; style.marginTop 16 ]
                        prop.children [
                            Html.div [
                                prop.children [
                                    Html.div [ prop.style [ style.fontSize 12.5; style.color textPri; style.fontWeight 600 ]; prop.text "Web-grounded Verify" ]
                                    Html.div [ prop.style [ style.fontSize 11; style.color textMut; style.marginTop 2 ]
                                               prop.text "Anthropic web search → real citations · ~$10/1k searches" ]
                                ]
                            ]
                            Html.button [
                                prop.onClick (fun _ -> dispatch (SetWebVerify(not model.WebVerify)))
                                prop.style [ style.width 42; style.height 24; style.borderRadius 12; style.cursor.pointer; style.position.relative
                                             style.custom ("flexShrink", "0"); style.custom ("border", sprintf "1px solid %s" border)
                                             style.custom ("background", (if model.WebVerify then accent else "transparent")); style.custom ("transition", "background .15s") ]
                                prop.children [
                                    Html.span [ prop.style [ style.position.absolute; style.top 2; style.width 18; style.height 18; style.borderRadius 9
                                                             style.custom ("background", (if model.WebVerify then "#FFF" else textMut))
                                                             style.custom ("left", (if model.WebVerify then "21px" else "3px")); style.custom ("transition", "left .15s") ] ]
                                ]
                            ]
                        ]
                    ]
                    // Account (Google Sign-In)
                    Html.div [
                        prop.style [ style.marginTop 18; style.paddingTop 16; style.custom ("borderTop", sprintf "1px solid %s" borderSoft) ]
                        prop.children [
                            Html.label [ prop.style [ style.fontSize 11; style.color textMut; style.custom ("letterSpacing", "0.04em"); style.custom ("textTransform", "uppercase") ]
                                         prop.text "Account" ]
                            (match model.GoogleEmail with
                             | Some email ->
                                 Html.div [
                                     prop.style [ style.display.flex; style.alignItems.center; style.justifyContent.spaceBetween; style.marginTop 9 ]
                                     prop.children [
                                         Html.span [ prop.style [ style.fontSize 13; style.color textPri ]; prop.text ("Signed in · " + email) ]
                                         Html.button [
                                             prop.text "Sign out"
                                             prop.onClick (fun _ -> dispatch GoogleSignOut)
                                             prop.style [ style.padding (7, 12); style.borderRadius 8; style.cursor.pointer; style.fontSize 12
                                                          style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent"); style.color textSec ]
                                         ]
                                     ]
                                 ]
                             | None ->
                                 Html.div [
                                     prop.children [
                                         Html.input [
                                             prop.type' "text"; prop.placeholder "Google OAuth Client ID"
                                             prop.value model.GoogleIdDraft
                                             prop.onChange (fun (v: string) -> dispatch (GoogleIdDraftChanged v))
                                             prop.style [ style.width (length.percent 100); style.padding 9; style.borderRadius 8; style.marginTop 8
                                                          style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                                                          style.color textPri; style.boxSizing.borderBox; style.fontSize 12.5; style.custom ("fontFamily", mono) ]
                                         ]
                                         Html.input [
                                             prop.type' "password"; prop.placeholder "Client secret"
                                             prop.value model.GoogleSecretDraft
                                             prop.onChange (fun (v: string) -> dispatch (GoogleSecretDraftChanged v))
                                             prop.style [ style.width (length.percent 100); style.padding 9; style.borderRadius 8; style.marginTop 7
                                                          style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                                                          style.color textPri; style.boxSizing.borderBox; style.fontSize 12.5; style.custom ("fontFamily", mono) ]
                                         ]
                                         Html.div [
                                             prop.style [ style.display.flex; style.custom ("gap", "8px"); style.marginTop 9 ]
                                             prop.children [
                                                 Html.button [
                                                     prop.text "Save keys"
                                                     prop.onClick (fun _ -> dispatch SaveGoogleKeys)
                                                     prop.style [ style.padding (8, 12); style.borderRadius 8; style.cursor.pointer; style.fontSize 12
                                                                  style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent"); style.color textSec ]
                                                 ]
                                                 Html.button [
                                                     prop.text (if model.GoogleBusy then "Opening browser…" else "Sign in with Google")
                                                     prop.disabled model.GoogleBusy
                                                     prop.onClick (fun _ -> dispatch DoGoogleSignIn)
                                                     prop.style [ style.custom ("flex", "1"); style.padding (8, 12); style.borderRadius 8; style.cursor.pointer; style.fontSize 12; style.fontWeight 600
                                                                  style.custom ("border", "none"); style.custom ("background", accent); style.color "#FFF" ]
                                                 ]
                                             ]
                                         ]
                                     ]
                                 ])
                            (match model.GoogleErr with
                             | Some e -> Html.div [ prop.style [ style.fontSize 12; style.color "#F0865A"; style.marginTop 8 ]; prop.text e ]
                             | None -> Html.none)
                            Html.div [ prop.style [ style.fontSize 11; style.color textMut; style.marginTop 8; style.lineHeight 1.5 ]
                                       prop.text "Identity only for now — accounts/sync arrive with the managed backend. Needs a 'Desktop app' OAuth client from Google Cloud Console." ]
                        ]
                    ]
                    // Appearance: accent + surface opacity
                    Html.div [
                        prop.style [ style.marginTop 18 ]
                        prop.children [
                            Html.label [ prop.style [ style.fontSize 11; style.color textMut; style.custom ("letterSpacing", "0.04em"); style.custom ("textTransform", "uppercase") ]
                                         prop.text "Accent" ]
                            Html.div [
                                prop.style [ style.display.flex; style.custom ("gap", "8px"); style.marginTop 7 ]
                                prop.children [
                                    for c in accentSwatches ->
                                        Html.button [
                                            prop.onClick (fun _ -> dispatch (SetAccent c))
                                            prop.style [ style.width 24; style.height 24; style.borderRadius 6; style.cursor.pointer
                                                         style.custom ("background", c)
                                                         style.custom ("border", (if model.AccentColor = c then "2px solid #FFF" else "2px solid transparent")) ]
                                        ]
                                ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.style [ style.marginTop 14 ]
                        prop.children [
                            Html.label [ prop.style [ style.fontSize 11; style.color textMut; style.custom ("letterSpacing", "0.04em"); style.custom ("textTransform", "uppercase") ]
                                         prop.text "Surface" ]
                            Html.div [
                                prop.style [ style.display.flex; style.custom ("gap", "6px"); style.marginTop 7 ]
                                prop.children [
                                    for (label, s) in [ "Opaque", Opaque; "Translucent", Translucent; "Transparent", Transparent ] ->
                                        Html.button [
                                            prop.text label
                                            prop.onClick (fun _ -> dispatch (SetOpacity s))
                                            prop.style [ style.custom ("flex", "1"); style.padding (7, 8); style.borderRadius 7; style.cursor.pointer; style.fontSize 12
                                                         style.custom ("border", sprintf "1px solid %s" border)
                                                         style.custom ("background", (if model.Opacity = s then accent else "transparent"))
                                                         style.color (if model.Opacity = s then "#FFF" else textSec) ]
                                        ]
                                ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.style [ style.marginTop 14 ]
                        prop.children [
                            Html.label [ prop.style [ style.fontSize 11; style.color textMut; style.custom ("letterSpacing", "0.04em"); style.custom ("textTransform", "uppercase") ]
                                         prop.text "Theme" ]
                            Html.div [
                                prop.style [ style.display.flex; style.custom ("gap", "6px"); style.marginTop 7 ]
                                prop.children [
                                    for (label, t) in [ "Dark", Dark; "Light", Light ] ->
                                        Html.button [
                                            prop.text label
                                            prop.onClick (fun _ -> dispatch (SetTheme t))
                                            prop.style [ style.custom ("flex", "1"); style.padding (7, 8); style.borderRadius 7; style.cursor.pointer; style.fontSize 12
                                                         style.custom ("border", sprintf "1px solid %s" border)
                                                         style.custom ("background", (if model.Theme = t then accent else "transparent"))
                                                         style.color (if model.Theme = t then "#FFF" else textSec) ]
                                        ]
                                ]
                            ]
                        ]
                    ]
                    Html.button [
                        prop.onClick (fun _ -> dispatch OpenScreenPrivacy)
                        prop.text "macOS: grant Screen Recording…"
                        prop.style [ style.marginTop 14; style.width (length.percent 100); style.padding (9, 12); style.borderRadius 8; style.cursor.pointer
                                     style.fontSize 12; style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent"); style.color textSec ]
                    ]
                    Html.div [
                        prop.style [ style.display.flex; style.custom ("gap", "9px"); style.marginTop 20 ]
                        prop.children [
                            Html.button [
                                prop.onClick (fun _ -> dispatch SaveSettings)
                                prop.text "Save"
                                prop.style [ style.custom ("flex", "1"); style.padding 10; style.borderRadius 8; style.custom ("border", "none")
                                             style.custom ("background", accent); style.color "#FFF"; style.cursor.pointer; style.fontWeight 600
                                             style.custom ("boxShadow", "0 4px 18px color-mix(in srgb, var(--accent) 40%, transparent)") ]
                            ]
                            Html.button [
                                prop.onClick (fun _ -> dispatch CloseSettings)
                                prop.text "Cancel"
                                prop.style [ style.padding (10, 16); style.borderRadius 8; style.custom ("border", sprintf "1px solid %s" border)
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
                         style.custom ("background", "rgba(14,10,6,0.24)") ]
            yield! hoverProps
            prop.onMouseDown (fun e -> setStart (Some(e.clientX, e.clientY)); setRect None)
            prop.onMouseMove (fun e ->
                match start with
                | Some(sx, sy) -> setRect (Some(min sx e.clientX, min sy e.clientY, abs (e.clientX - sx), abs (e.clientY - sy)))
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
                                 style.custom ("background", raised); style.color textSec; style.padding (8, 15)
                                 style.borderRadius 8; style.fontSize 12.5; style.custom ("border", sprintf "1px solid %s" border) ]
                    prop.text "Drag a box over any text or code · Esc to cancel"
                ]
                match rect with
                | Some(x, y, w, h) ->
                    Html.div [
                        prop.style [ style.position.absolute; style.custom ("left", px x); style.custom ("top", px y)
                                     style.custom ("width", px w); style.custom ("height", px h)
                                     style.custom ("border", sprintf "2px solid %s" accent)
                                     style.custom ("background", "rgba(228,87,30,0.10)"); style.borderRadius 3 ]
                    ]
                | None -> Html.none
            ]
        ])

// ---- action bar after a region is drawn -----------------------------------
let private pendingBar (isCode: bool) (ax: float) (ay: float) dispatch =
    let btn (label: string) (mode: WidgetMode) (primary: bool) =
        Html.button [
            prop.text label
            prop.onClick (fun _ -> dispatch (QuickAction mode))
            prop.style [ style.custom ("border", "none")
                         style.custom ("background", (if primary then accent else "transparent"))
                         style.color (if primary then "#FFF" else textPri)
                         style.padding (7, 11); style.cursor.pointer; style.borderRadius 6; style.fontSize 12.5
                         style.fontWeight (if primary then 600 else 500) ]
        ]
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.custom ("left", px (min ax 1100.0))
                     style.custom ("top", px (max (ay - 46.0) 8.0)); style.display.flex; style.alignItems.center
                     style.custom ("gap", "1px"); style.custom ("background", raised); style.custom ("border", sprintf "1px solid %s" border)
                     style.borderRadius 10; style.padding 4; style.custom ("boxShadow", shadow); style.custom ("zIndex", "2000000") ]
        yield! hoverProps
        prop.children [
            Html.span [ prop.style [ style.width 14; style.height 14; style.color accent; style.marginLeft 5; style.marginRight 3 ]
                        prop.children [ svgIco icoScan 14 ] ]
            if isCode then
                Html.span [ prop.style [ style.fontSize 10; style.custom ("fontFamily", mono); style.color textMut
                                         style.custom ("background", chip); style.padding (1, 6); style.borderRadius 5; style.marginRight 2 ]
                            prop.text "code" ]
            btn "Ask" Ask false
            btn "ELI5" ELI5 false
            btn "Verify" Verify false
            btn "Diff" Diff isCode
            Html.button [
                prop.text "✕"
                prop.onClick (fun _ -> dispatch DismissPending)
                prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color textMut
                             style.cursor.pointer; style.padding (6, 9) ]
            ]
        ]
    ]

// ---- structured Verify rendering (confidence bar + colored claim badges) ---
let private tagColor =
    function
    | "ESTABLISHED" -> "#3FA35B"
    | "UNCERTAIN" -> "#E8912B"
    | "LIKELY FALSE" -> "#E23B3B"
    | "NEEDS LIVE CHECK" -> "#4C86C6"
    | "OPINION" -> "#8A7C68"
    | _ -> textMut

let private confColor n = if n >= 70 then "#3FA35B" elif n >= 40 then "#E8912B" else "#E23B3B"

let private confBar (n: int) =
    Html.div [
        prop.style [ style.marginBottom 9 ]
        prop.children [
            Html.div [
                prop.style [ style.display.flex; style.justifyContent.spaceBetween; style.marginBottom 4 ]
                prop.children [
                    Html.span [ prop.style [ style.fontSize 10; style.color textMut; style.custom ("letterSpacing", "0.06em"); style.custom ("textTransform", "uppercase") ]
                                prop.text "Confidence" ]
                    Html.span [ prop.style [ style.fontSize 12; style.fontWeight 700; style.color (confColor n); style.custom ("fontFamily", mono) ]
                                prop.text (sprintf "%d/100" n) ]
                ]
            ]
            Html.div [
                prop.style [ style.height 6; style.borderRadius 3; style.custom ("background", track); style.overflow.hidden ]
                prop.children [ Html.div [ prop.style [ style.height 6; style.custom ("width", sprintf "%d%%" n); style.custom ("background", confColor n); style.custom ("boxShadow", "0 0 10px " + confColor n) ] ] ]
            ]
        ]
    ]

let private verifyBody (text: string) =
    let lines = text.Replace("\r", "").Split('\n')
    let headers = set [ "Claims:"; "From memory:"; "Check live:"; "Watch out:" ]
    Html.div [
        prop.children [
            for raw in lines do
                let line = raw.Trim()
                if line = "" then Html.none
                elif line.StartsWith("Confidence:") then
                    match System.Int32.TryParse((line.Substring(line.IndexOf(':') + 1).Split('/')).[0].Trim()) with
                    | true, n -> confBar (max 0 (min 100 n))
                    | _ -> Html.div [ prop.style [ style.fontSize 12.5; style.color textPri ]; prop.text line ]
                elif line.StartsWith("Verdict:") then
                    Html.div [ prop.style [ style.fontSize 13; style.color textPri; style.fontWeight 600; style.marginBottom 9; style.lineHeight 1.45 ]
                               prop.text (line.Substring(8).Trim()) ]
                elif line.StartsWith("- [") && line.Contains("]") then
                    let inside = line.Substring(line.IndexOf('[') + 1)
                    let tag = inside.Substring(0, inside.IndexOf(']')).Trim()
                    let rest = inside.Substring(inside.IndexOf(']') + 1).Trim()
                    Html.div [
                        prop.style [ style.display.flex; style.custom ("gap", "7px"); style.alignItems.flexStart; style.marginBottom 5 ]
                        prop.children [
                            Html.span [ prop.style [ style.fontSize 8.5; style.fontWeight 700; style.custom ("letterSpacing", "0.03em"); style.color "#FFF"
                                                     style.custom ("background", tagColor tag); style.padding (2, 6); style.borderRadius 4
                                                     style.custom ("whiteSpace", "nowrap"); style.custom ("flexShrink", "0"); style.marginTop 2 ]
                                        prop.text tag ]
                            Html.span [ prop.style [ style.fontSize 12.5; style.color textPri; style.lineHeight 1.45 ]; prop.text rest ]
                        ]
                    ]
                elif headers.Contains line then
                    Html.div [ prop.style [ style.fontSize 10; style.color textMut; style.fontWeight 700; style.custom ("letterSpacing", "0.07em")
                                            style.custom ("textTransform", "uppercase"); style.marginTop 9; style.marginBottom 4 ]
                               prop.text (line.TrimEnd(':')) ]
                elif line.StartsWith("- ") then
                    Html.div [
                        prop.style [ style.display.flex; style.custom ("gap", "6px"); style.marginBottom 4 ]
                        prop.children [
                            Html.span [ prop.style [ style.color textMut; style.fontSize 12.5 ]; prop.text "•" ]
                            Html.span [ prop.style [ style.fontSize 12.5; style.color textSec; style.lineHeight 1.45 ]; prop.text (line.Substring(2).Trim()) ]
                        ]
                    ]
                else
                    Html.div [ prop.style [ style.fontSize 12.5; style.color textPri; style.lineHeight 1.45; style.marginBottom 4 ]; prop.text line ]
        ]
    ]

// ---- message bubble --------------------------------------------------------
let private bubble (accentColor: string) (isVerify: bool) (m: ChatMsg) =
    let mine = m.Role = "user"
    let structured = isVerify && not mine
    let code = (not mine) && (not structured) && isCodey m.Text
    let pv, ph = if structured then 11, 12 else 9, 11
    Html.div [
        prop.style [ style.display.flex; style.custom ("justifyContent", (if mine then "flex-end" else "flex-start")); style.marginBottom 8 ]
        prop.children [
            Html.div [
                prop.style [ style.maxWidth (length.percent (if structured then 97 else 88))
                             style.padding (pv, ph); style.borderRadius 10
                             style.fontSize 13; style.lineHeight 1.5
                             style.custom ("whiteSpace", (if structured then "normal" else "pre-wrap")); style.custom ("wordBreak", "break-word")
                             style.custom ("fontFamily", (if code then mono else "inherit"))
                             style.custom ("background", (if mine then accentColor else chip))
                             style.color (if mine then "#FFF" else textPri) ]
                prop.children [
                    if structured then verifyBody m.Text else Html.text m.Text
                    if code then
                        Html.button [
                            prop.text "copy"
                            prop.onClick (fun _ -> copy m.Text)
                            prop.style [ style.display.block; style.marginTop 7; style.fontSize 11; style.cursor.pointer
                                         style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent")
                                         style.color textMut; style.borderRadius 5; style.padding (2, 7); style.custom ("fontFamily", mono) ]
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
                     style.custom ("width", px w.Width); style.custom ("height", px w.Height); style.custom ("zIndex", string w.Z)
                     style.display.flex; style.flexDirection.column; style.borderRadius 12
                     style.custom ("background", "var(--surface)"); style.custom ("backdropFilter", "blur(var(--sblur))")
                     style.custom ("WebkitBackdropFilter", "blur(var(--sblur))")
                     style.custom ("border", sprintf "1px solid %s" border)
                     style.custom ("boxShadow", shadow); style.overflow.hidden ]
        yield! hoverProps
        prop.onMouseDown (fun _ -> dispatch (Focus w.Id))
        prop.children [
            // header: dark, hairline, color DOT (no gradient fill, no left-border-accent)
            Html.div [
                prop.style [ style.display.flex; style.alignItems.center; style.custom ("gap", "8px"); style.padding (10, 12)
                             style.custom ("background", raised); style.custom ("borderBottom", sprintf "1px solid %s" border)
                             style.cursor "grab"; style.custom ("userSelect", "none") ]
                prop.onMouseDown (fun e -> dispatch (StartDrag(w.Id, e.clientX - w.PosX, e.clientY - w.PosY)))
                prop.children [
                    colorDot w.Color
                    Html.span [ prop.style [ style.fontWeight 600; style.fontSize 13; style.color textPri ]; prop.text w.Title ]
                    if w.Via <> "" then
                        Html.span [ prop.style [ style.fontSize 10.5; style.custom ("fontFamily", mono); style.color textMut
                                                 style.custom ("background", chip); style.padding (1, 6); style.borderRadius 5 ]
                                    prop.text w.Via ]
                    Html.div [ prop.style [ style.custom ("flex", "1") ] ]
                    Html.button [
                        prop.text "–"
                        prop.onClick (fun e -> e.stopPropagation (); dispatch (Minimize w.Id))
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color textMut
                                     style.cursor.pointer; style.fontSize 15; style.custom ("lineHeight", "1") ]
                    ]
                    Html.button [
                        prop.text "✕"
                        prop.onClick (fun e -> e.stopPropagation (); dispatch (RequestClose w.Id))
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.style [ style.custom ("border", "none"); style.custom ("background", "transparent"); style.color textMut
                                     style.cursor.pointer; style.fontSize 13 ]
                    ]
                ]
            ]
            // context thumbnail
            Html.div [
                prop.style [ style.padding (8, 12); style.custom ("borderBottom", sprintf "1px solid %s" borderSoft) ]
                prop.children [
                    Html.img [
                        prop.src w.Capture.ImageDataUrl
                        prop.style [ style.maxWidth (length.percent 100); style.maxHeight 88; style.borderRadius 6
                                     style.custom ("border", sprintf "1px solid %s" border); style.display.block ]
                    ]
                ]
            ]
            // conversation
            Html.div [
                prop.style [ style.custom ("flex", "1"); style.overflowY.auto; style.padding 12 ]
                prop.children [
                    yield! (w.Messages |> List.map (bubble w.Color (w.Mode = Verify)))
                    if w.Streaming then bubble w.Color (w.Mode = Verify) { Role = "assistant"; Text = (if w.StreamBuf = "" then "…" else w.StreamBuf) }
                    match w.Error with
                    | Some e -> Html.div [ prop.style [ style.color "#F0865A"; style.fontSize 12 ]; prop.text ("Error: " + e) ]
                    | None -> Html.none
                ]
            ]
            // input row
            Html.div [
                prop.style [ style.display.flex; style.custom ("gap", "8px"); style.padding 10
                             style.custom ("borderTop", sprintf "1px solid %s" borderSoft); style.alignItems.center ]
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
                        prop.style [ style.custom ("flex", "1"); style.resize.none; style.padding 9; style.borderRadius 8
                                     style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", inputBg)
                                     style.color textPri; style.fontSize 13; style.boxSizing.borderBox ]
                    ]
                    Html.button [
                        prop.disabled w.Streaming
                        prop.onClick (fun _ -> dispatch (Send w.Id))
                        prop.style [ style.custom ("border", "none"); style.custom ("background", w.Color); style.color "#FFF"
                                     style.borderRadius 8; style.width 40; style.height 36; style.cursor.pointer
                                     style.custom ("boxShadow", "0 3px 14px " + w.Color + "66")
                                     style.display.flex; style.alignItems.center; style.justifyContent.center ]
                        prop.children [ svgIco icoSend 16 ]
                    ]
                ]
            ]
            // resize handle
            Html.div [
                prop.onMouseDown (fun e -> e.stopPropagation (); dispatch (StartResize w.Id))
                prop.style [ style.position.absolute; style.right 0; style.bottom 0; style.width 15; style.height 15
                             style.cursor "nwse-resize"
                             style.custom ("background", "linear-gradient(135deg, transparent 55%, " + border + " 55%)") ]
            ]
            // close menu
            match model.Closing with
            | Some cid when cid = w.Id ->
                Html.div [
                    prop.style [ style.position.absolute; style.custom ("inset", "0"); style.display.flex; style.flexDirection.column
                                 style.alignItems.center; style.justifyContent.center; style.custom ("gap", "12px")
                                 style.custom ("background", "rgba(14,10,6,0.88)") ]
                    prop.children [
                        Html.div [ prop.style [ style.color textSec; style.fontSize 13 ]; prop.text "Close this side-quest?" ]
                        Html.div [
                            prop.style [ style.display.flex; style.custom ("gap", "9px") ]
                            prop.children [
                                Html.button [
                                    prop.text "Merge to context"
                                    prop.onClick (fun _ -> dispatch (CloseWith(w.Id, Merge)))
                                    prop.style [ style.custom ("border", "none"); style.custom ("background", accent); style.color "#FFF"
                                                 style.padding (9, 13); style.borderRadius 8; style.cursor.pointer; style.fontWeight 600; style.fontSize 12.5 ]
                                ]
                                Html.button [
                                    prop.text "Discard"
                                    prop.onClick (fun _ -> dispatch (CloseWith(w.Id, Discard)))
                                    prop.style [ style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", "transparent")
                                                 style.color textSec; style.padding (9, 13); style.borderRadius 8; style.cursor.pointer; style.fontSize 12.5 ]
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
                        prop.style [ style.width 14; style.height 14; style.borderRadius 7; style.cursor.pointer
                                     style.custom ("background", w.Color); style.custom ("boxShadow", "0 0 0 3px rgba(0,0,0,0.35)") ]
                    ]))
            ]
        ]

// ---- control dock ----------------------------------------------------------
let private dock dispatch =
    Html.div [
        prop.className "ss-interactive"
        prop.style [ style.position.fixedRelativeToWindow; style.right 18; style.bottom 18; style.display.flex
                     style.custom ("gap", "8px"); style.alignItems.center ]
        yield! hoverProps
        prop.children [
            Html.button [
                prop.title "Highlight a region (⌘⇧Space)"
                prop.onClick (fun _ -> dispatch ToggleCapture)
                prop.style [ style.custom ("border", "none"); style.custom ("background", accent); style.color "#FFF"
                             style.padding (10, 15); style.borderRadius 9; style.cursor.pointer; style.fontWeight 600; style.fontSize 13
                             style.display.flex; style.alignItems.center; style.custom ("gap", "8px")
                             style.custom ("boxShadow", "0 8px 24px rgba(228,87,30,0.35)") ]
                prop.children [
                    Html.span [ prop.style [ style.width 15; style.height 15 ]; prop.children [ svgIco icoScan 15 ] ]
                    Html.span [ prop.text "Capture" ]
                ]
            ]
            Html.button [
                prop.title "Settings / API keys"
                prop.onClick (fun _ -> dispatch OpenSettings)
                prop.style [ style.custom ("border", sprintf "1px solid %s" border); style.custom ("background", raised); style.color textSec
                             style.width 40; style.height 40; style.borderRadius 9; style.cursor.pointer
                             style.display.flex; style.alignItems.center; style.justifyContent.center ]
                prop.children [ Html.span [ prop.style [ style.width 17; style.height 17 ]; prop.children [ svgIco icoGear 17 ] ] ]
            ]
        ]
    ]

// ---- root ------------------------------------------------------------------
let view (model: Model) dispatch =
    Html.div [
        prop.style ([ style.custom ("--accent", model.AccentColor)
                      style.custom ("--surface", surfaceVar model.Theme model.Opacity)
                      style.custom ("--sblur", blurVar model.Opacity) ]
                    @ (themeVars model.Theme |> List.map (fun (k, v) -> style.custom (k, v))))
        prop.children [
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
            | Some(_, ax, ay) -> pendingBar model.PendingCode ax ay dispatch
            | None -> Html.none

            if model.CaptureMode then CaptureSelector {| dispatch = dispatch |} else Html.none

            dock dispatch

            if model.ShowSettings then settingsModal model dispatch else Html.none
        ]
    ]
