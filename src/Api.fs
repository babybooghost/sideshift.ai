module SideShift.Api

open SideShift.Types

let DEFAULT_ANTHROPIC_MODEL = "claude-sonnet-5"
// Cross-model critic for Verify. A DIFFERENT model than the default catches more
// than same-model self-checking. Editable in settings.
let DEFAULT_CRITIC_MODEL = "openai/gpt-4o"

let systemFor mode (shared: string list) =
    let baseSys =
        match mode with
        | Ask ->
            "You are a focused study assistant. The user highlighted a region of their screen (image attached). Answer their follow-up tightly. Do not repeat large blocks of the source; be direct."
        | ELI5 ->
            "Explain the highlighted term in exactly two simple sentences, grounded in the context shown in the image. No preamble, no lists."
        | Verify ->
            "You are an independent, skeptical fact-checker looking at a claim in the attached image. You did NOT write it. Reply as: a line 'Confidence: N/100', then a short bullet list of missing nuance or likely hallucinations. If you cannot read the claim, say so."
        | Diff ->
            "The highlighted region is code. Answer the user's request as a MINIMAL git-style unified diff (--- / +++ / @@ / lines prefixed + or -). Show only changed lines. Never reprint unchanged code or whole files."
    if List.isEmpty shared then baseSys
    else baseSys + "\n\nEarlier side-notes the user carried over:\n- " + String.concat "\n- " shared

let firstPrompt mode =
    match mode with
    | Ask -> None
    | ELI5 -> Some "Define the highlighted term simply."
    | Verify -> Some "Fact-check the highlighted claim and give a confidence score."
    | Diff -> Some "Review the highlighted code and suggest the fix as a diff."

/// Pick backend for a widget: Verify routes to the OpenRouter cross-model critic
/// when a key is present; everything else uses Anthropic-direct.
/// Returns (provider, apiKey, modelId, viaLabel).
let routeFor (m: Model) (mode: WidgetMode) : (string * string * string * string) option =
    match mode, m.OpenRouterKey, m.AnthropicKey with
    | Verify, Some ork, _ -> Some("openrouter", ork, m.CriticModel, "critic:" + m.CriticModel)
    | _, _, Some ak -> Some("anthropic", ak, m.DefaultModel, m.DefaultModel)
    | _ -> None

/// Neutral request the Electron provider layer shapes per-provider.
let buildReq provider apiKey (modelId: string) system (w: Widget) (userText: string) : obj =
    let history =
        w.Messages
        |> List.map (fun mm -> box {| role = mm.Role; text = mm.Text |})
        |> List.toArray
    box
        {| provider = provider
           apiKey = apiKey
           model = modelId
           system = system
           maxTokens = 1500
           history = history
           userText = userText
           imageDataUrl = w.Capture.ImageDataUrl |}

/// One-shot classifier: is the captured region primarily code?
let buildClassifyReq (apiKey: string) (cap: Capture) : obj =
    box
        {| provider = "anthropic"
           apiKey = apiKey
           model = DEFAULT_ANTHROPIC_MODEL
           system = "You are a classifier. Reply with exactly one word — CODE if the attached image is primarily source code or a code block, otherwise TEXT. No other words."
           maxTokens = 5
           history = ([||]: obj [])
           userText = "Classify the attached region."
           imageDataUrl = cap.ImageDataUrl |}

let mergeSummary (w: Widget) : string =
    let lastA =
        w.Messages
        |> List.rev
        |> List.tryFind (fun m -> m.Role = "assistant")
        |> Option.map (fun m -> m.Text)
        |> Option.defaultValue ""
    let head = if lastA.Length > 240 then lastA.Substring(0, 240) + "…" else lastA
    sprintf "[%s] %s" w.Title (head.Replace("\n", " "))
