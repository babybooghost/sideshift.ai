module SideShift.Api

open Fable.Core.JsInterop
open SideShift.Types

let DEFAULT_MODEL = "claude-sonnet-5"

let private base64Of (dataUrl: string) =
    let i = dataUrl.IndexOf(',')
    if i >= 0 then dataUrl.Substring(i + 1) else dataUrl

let systemFor mode (shared: string list) =
    let baseSys =
        match mode with
        | Ask ->
            "You are a focused study assistant. The user highlighted a region of their screen (image attached). Answer their follow-up tightly. Do not repeat large blocks of the source; be direct."
        | ELI5 ->
            "Explain the highlighted term in exactly two simple sentences, grounded in the context shown in the image. No preamble, no lists."
        | Verify ->
            "You are an independent, skeptical fact-checker. Assess the highlighted claim in the image. Reply as: a line 'Confidence: N/100', then a short bullet list of missing nuance or likely hallucinations. If you cannot read the claim, say so."
        | Diff ->
            "The highlighted region is code. Answer the user's request as a MINIMAL git-style unified diff (--- / +++ / @@ / lines prefixed + or -). Show only changed lines. Never reprint unchanged code or whole files."
    if List.isEmpty shared then baseSys
    else
        baseSys
        + "\n\nEarlier side-notes the user carried over:\n- "
        + String.concat "\n- " shared

let firstPrompt mode =
    match mode with
    | Ask -> None
    | ELI5 -> Some "Define the highlighted term simply."
    | Verify -> Some "Fact-check the highlighted claim and give a confidence score."
    | Diff -> Some "Review the highlighted code and suggest the fix as a diff."

/// Anthropic messages array: history as text turns + current user turn with image attached.
let private buildMessages (w: Widget) (userText: string) : obj array =
    let history =
        w.Messages
        |> List.map (fun m -> box {| role = m.Role; content = m.Text |})
        |> List.toArray

    let imgBlock =
        box
            {| ``type`` = "image"
               source =
                {| ``type`` = "base64"
                   media_type = "image/png"
                   data = base64Of w.Capture.ImageDataUrl |} |}

    let textBlock = box {| ``type`` = "text"; text = userText |}
    let current = box {| role = "user"; content = [| textBlock; imgBlock |] |}
    Array.append history [| current |]

/// Build the request object consumed by Interop.streamAnthropic.
let buildReq (apiKey: string) (shared: string list) (w: Widget) (userText: string) : obj =
    box
        {| apiKey = apiKey
           model = DEFAULT_MODEL
           system = systemFor w.Mode shared
           maxTokens = 1500
           messages = buildMessages w userText |}

/// Summarize a finished side-quest for the "Merge" flow.
let mergeSummary (w: Widget) : string =
    let lastA =
        w.Messages
        |> List.rev
        |> List.tryFind (fun m -> m.Role = "assistant")
        |> Option.map (fun m -> m.Text)
        |> Option.defaultValue ""
    let head = if lastA.Length > 240 then lastA.Substring(0, 240) + "…" else lastA
    sprintf "[%s] %s" w.Title (head.Replace("\n", " "))
