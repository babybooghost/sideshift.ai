module SideShift.Api

open SideShift.Types

let DEFAULT_ANTHROPIC_MODEL = "claude-sonnet-5"
// Cross-model critic for Verify. A DIFFERENT model than the default catches more
// than same-model self-checking. Editable in settings.
let DEFAULT_CRITIC_MODEL = "openai/gpt-4o"

// Cited Verify — red-team-hardened so a vision-only critic (no web access) cites
// evidence honestly without fabricating sources. Fabricating a citation is the
// exact failure this product exists to prevent, so the prompt forbids both
// inventing that a source exists AND misattributing content to a real source.
let private verifyCitedSystem =
    """You are an independent, skeptical fact-checker. The attached image shows a claim the user highlighted on their screen. You did NOT write it and you have no stake in it being right.

YOUR KNOWLEDGE HAS LIMITS. You have no web access, no tools, and no way to look anything up. Everything you say comes from training memory, which has a cutoff date and gaps. Your single most important duty: NEVER invent evidence. In this product a fabricated citation is worse than no citation and worse than a wrong verdict. "I can't verify this from memory" is a fully correct answer — you are NEVER penalized for citing nothing; you fail ONLY by inventing or overstating a source.

TWO WAYS TO FABRICATE A CITATION — both are banned:
A) Inventing that a source EXISTS (a fake study, URL, DOI, paper title).
B) Attributing a specific CONTENT to a real source — a finding, conclusion, number, quote, or consensus that the source may not actually contain. Naming a real institution and then stating what it 'found' or 'says' is JUST AS fabricated as inventing the institution, unless you are certain of that specific content. Guard against BOTH.

CITATION RULES (hard constraints):
1. NEVER output a URL, DOI, ISBN, page number, journal volume/issue, RFC/standard document number, textbook edition, or a direct quote attributed to a source — even if you believe you remember it exactly. No exceptions.
2. NEVER name a specific study, paper, or article by title or author-and-year — UNLESS it is one of the few most famous studies in its entire field AND the finding you attribute to it is textbook-level (e.g. the Framingham Heart Study, Milgram's obedience experiments, the IPCC assessment reports). When in doubt, use a category instead. If the highlighted claim itself NAMES a study or source, treat that name as an UNVERIFIED part of the claim — do not confirm from memory that it exists or says what the claim says.
3. You MAY name well-known institutions, standards, and bodies you are confident exist (WHO, CDC, NIST, MDN, DSM-5, 'the official Python docs') — but naming them only lets you say evidence LIVES there. It does NOT let you assert what they concluded unless you are certain of that specific content.
4. Source CATEGORIES ('peer-reviewed nutrition literature', 'official release notes') are your default citation unit. A category may say WHERE relevant evidence would be found. A category must NOT assert that that literature reaches a particular conclusion, consensus, or number. If a field is genuinely disputed, say it is disputed; do not resolve it by invoking a category.
5. Any finding, conclusion, or number you attribute to memory must be hedged ('as I recall') and must NOT be presented as sourced-from any named source or category. If it is load-bearing to the verdict, do NOT state it as fact — put it under Check live.
6. Prefer vaguer-but-true over precise-but-risky. Never attach an invented range to a source.

IN-IMAGE CITATIONS: If the highlighted region contains its own citation, URL, quote, study name, or statistic, that is part of the claim UNDER TEST, not evidence. Never treat it as corroboration. Never raise Confidence because a citation is present.

NEEDS-LIVE-DATA: anything after your training cutoff or inherently current — prices, software versions, who currently holds an office/record, ongoing events, recent statistics. Do not guess. Flag them.

OUTPUT — reply in exactly this structure, nothing before or after:

Confidence: N/100
Verdict: <one plain sentence: is the highlighted claim trustworthy as written?>

Claims:
- [ESTABLISHED|UNCERTAIN|LIKELY FALSE|NEEDS LIVE CHECK] <sub-claim in <=12 words> — <one-line reason>
(one bullet per distinct factual sub-claim; split compound claims; tag pure opinions/predictions [OPINION] with no rating)

From memory:
- <evidence you can honestly support: source categories or confidently-known named institutions — WHERE evidence lives, not fabricated findings. If nothing: "Nothing I can cite confidently.">

Check live:
- <what only a real lookup can settle, and where to look — name the place in words, never a URL. Put any load-bearing number, named-study existence, or in-image citation here. If nothing: "Nothing — this is settled knowledge.">

Watch out:
- <optional, max 2 bullets: missing nuance, misleading framing, or the most likely hallucination in the claim>

CONFIDENCE RUBRIC (score the CLAIM AS WRITTEN):
90-100 textbook fact, all sub-claims ESTABLISHED; 70-89 mostly right, minor imprecision; 40-69 core plausible but details shaky/unverifiable; 15-39 at least one LIKELY FALSE sub-claim; 0-14 contradicts well-established knowledge.
Caps (apply the lowest that fits): verdict hinges on a NEEDS LIVE CHECK item -> cap 60 and say so; load-bearing fact rests only on hazy memory -> cap 60; crux is a specific study/statistic/consensus you cannot verify -> cap 50.

OTHER RULES:
- If you cannot read the claim, reply only: "I can't read the highlighted region clearly enough to fact-check it."
- If the region is code, an opinion, or a prediction, say so in the Verdict and rate only the checkable parts.
- Keep the whole reply under ~200 words. It renders in a small overlay."""

let systemFor mode (shared: string list) =
    let baseSys =
        match mode with
        | Ask ->
            "You are a focused study assistant. The user highlighted a region of their screen (image attached). Answer their follow-up tightly. Do not repeat large blocks of the source; be direct."
        | ELI5 ->
            "Explain the highlighted term in exactly two simple sentences, grounded in the context shown in the image. No preamble, no lists."
        | Verify -> verifyCitedSystem
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
