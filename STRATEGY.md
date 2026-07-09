# SideShift — Strategy

Cofounder-honest take. Grounded in real AI pain points, not startup fluff. Read the "Hard truths" section first if you only read one.

## Positioning: a trust layer, not another chatbot

SideShift is a **meta-layer over any AI**, not an AI. Category to own: **the verify / understand / retain layer for AI output.**

The Cluely comparison is a double-edged sword. Cluely's wedge was "cheat on everything" — invisible help in interviews/exams, deliberately controversial. SideShift is the **honest inverse**: the overlay that makes you *trust less and understand more*. That's a defensible brand because **trust is the #1 unsolved problem in consumer AI**, and nobody owns "the anti-hallucination layer."

Tagline candidates:
- "Cluely helps you fake it. SideShift helps you actually get it."
- "A second opinion for everything AI tells you."
- "Verify, don't vibe."

Two structural differentiators competitors can't easily copy without changing their model:
- **App-agnostic** — reads the *screen*, so it works over Claude, ChatGPT, Gemini, an IDE, a PDF. Not locked to one vendor.
- **Bring-your-own-key / local** — no server sees your data or your prompts. This is the privacy wedge.

## The real AI problems → which features, and which are actually a moat

| Human/AI problem | SideShift answer | Moat strength |
|---|---|---|
| **Hallucination / can't tell what's true** | Verify with a **cross-model critic** (different model grades the claim) | ★★★ Strongest. Cross-*provider* verification is genuinely hard for a single-vendor tool to match. |
| **Context pollution** (tangents derail the main thread) | Merge / Discard | ★★ Real workflow value, easy to demo |
| **Cognitive overload** (scroll, jargon, full-file reprints) | ELI5, Diff-docking, Margin Minimap | ★★ Sticky once learned |
| **App-switching friction** (copy-paste between tools) | Overlay works over anything | ★★ Structural |
| **Privacy anxiety** (sensitive screen → cloud AI) | BYOK, local storage, no server | ★★★ Underrated; the enterprise/regulated wedge |
| **Over-reliance / deskilling** | Verify breeds skepticism; ELI5 teaches | ★ Ethics + marketing story, not a moat but a *narrative* |

## Monetization (ranked by leverage)

The core tension: **BYOK is honest and cheap for you, but most users won't get an API key.** That ceiling is the whole revenue problem. Solve it in this order:

1. **Managed-key / usage tier (the real engine).** For non-technical users, sell tokens at margin — one-click, no API key. This is where the money is; BYOK users are your evangelists, managed-key users are your revenue.
2. **Pro subscription** ($8–12/mo). Premium: cross-model Verify, sync/persistence across devices, unlimited widgets, team shared context, priority models. BYOK users pay for *convenience + power*, not tokens.
3. **One-time license** ($40–60, indie-Mac-app model). Captures the privacy crowd that hates subscriptions. Offer alongside Pro.
4. **Team / EDU seats.** Students are the *perfect* fit for a "study/verify layer" — fact-check essays, ELI5 lectures. Cheap seats, huge volume, word-of-mouth.

Free tier = BYOK + core features. Convert on the two things BYOK can't give: **no-key convenience** and **cross-device/team**.

## Go-to-market

- **The demo is the product.** A 20-second screen recording of Verify **catching a real hallucination** is the single viral asset. Lead with it everywhere.
- **Channels, in order:** Show HN (F#/Fable + privacy + BYOK is HN-native catnip) → Product Hunt → X build-in-public → r/ChatGPT, r/artificial → student/researcher communities.
- **Audience wedges:** (1) skeptics/researchers → Verify; (2) students → ELI5 + study layer; (3) developers → Diff-docking.
- **Contrast marketing vs Cluely** — ride the attention, but differentiate hard on ethics/trust or you look derivative.
- **Content SEO:** "how to fact-check ChatGPT / verify AI answers / catch AI hallucinations."

## Product roadmap (high-leverage, problem-grounded)

Ordered by trust-impact × differentiation:

1. **Cited Verify** — critic pulls *sources*, not just a confidence number. Biggest trust lever; turns a nice feature into a category-definer.
2. **Cross-model consensus** — run N models on the same claim, show agree/disagree. "Second opinion" as a headline feature.
3. **Local/offline model option (Ollama)** — fully private inference. *Kills the privacy objection entirely* and is a story no cloud-native competitor can tell.
4. **PII / redaction guard** — detect + blur secrets before pixels leave the machine. Turns "reads your screen" fear into a selling point.
5. **Managed-key onboarding** — the no-API-key path (see monetization #1).
6. **History + search of past side-chats** — solves "lost in the scroll" across sessions.
7. **Team "verified facts" library** — shared, cited, reusable. B2B expansion.

## Hard truths (the cofounder part)

- **Thin tech moat.** Anyone can build a screen overlay. Your moat is *brand (trust) + UX polish + workflow lock-in (persistence/merged context) + the managed-key billing relationship.* Invest there, not in the overlay plumbing.
- **BYOK caps your TAM** to technical users until managed keys ship. Prioritize that if you want revenue, not just stars.
- **"Reads your screen" triggers real fear.** Counter with radical transparency: local-only by default, visible capture indicator, redaction, and consider open-sourcing the capture/privacy core so people can audit it.
- **Distribution costs money:** Apple notarization ($99/yr) + Windows code-signing cert (~$100–400/yr) are required before non-beta distribution; today's builds are unsigned/ad-hoc.
- **Cluely's baggage cuts both ways** — the "AI cheating" association can taint you. Lean explicitly *ethical* to avoid it.
- **Focus.** You have 5 strong features. Ship Verify (cited) exceptionally before broadening — one category-defining feature beats five good ones.

## The one-line bet

The AI market is flooded with tools that *generate*. Almost none help you *trust and understand* what was generated. SideShift can own "the trust layer for AI" — if it nails cited, cross-model Verify and makes the privacy story unbeatable.
