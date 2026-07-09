// Provider abstraction. Both paths are official, documented, public APIs called
// with the user's own key — no reverse engineering.
//   anthropic  -> https://docs.anthropic.com/en/api/messages-streaming
//   openrouter -> https://openrouter.ai/docs (OpenAI-style chat/completions)
//
// The renderer sends a NEUTRAL request; each provider shapes it to its own
// wire format and normalizes the SSE stream down to plain text deltas.

const ANTHROPIC_URL = "https://api.anthropic.com/v1/messages";
const ANTHROPIC_VERSION = "2023-06-01";
const OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions";

const b64 = (dataUrl) => {
  const i = dataUrl.indexOf(",");
  return i >= 0 ? dataUrl.slice(i + 1) : dataUrl;
};
const mediaTypeOf = (dataUrl) => {
  const m = /^data:([^;]+);/.exec(dataUrl || "");
  return m ? m[1] : "image/png";
};

// Shared SSE reader. `extract(evt)` returns a text chunk or null; throwing aborts.
async function* parseSSE(res, extract) {
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let idx;
    while ((idx = buffer.indexOf("\n\n")) !== -1) {
      const frame = buffer.slice(0, idx);
      buffer = buffer.slice(idx + 2);
      for (const line of frame.split("\n")) {
        const t = line.trim();
        if (!t.startsWith("data:")) continue;
        const data = t.slice(5).trim();
        if (!data || data === "[DONE]") continue;
        let evt;
        try { evt = JSON.parse(data); } catch { continue; }
        const out = extract(evt);
        if (out) yield out;
      }
    }
  }
}

async function* anthropicStream({ apiKey, model, system, history, userText, imageDataUrl, maxTokens, webGrounded }) {
  const messages = (history || []).map((h) => ({ role: h.role, content: h.text }));
  const content = [{ type: "text", text: userText }];
  if (imageDataUrl) {
    content.push({
      type: "image",
      source: { type: "base64", media_type: mediaTypeOf(imageDataUrl), data: b64(imageDataUrl) }
    });
  }
  messages.push({ role: "user", content });

  const body = { model, max_tokens: maxTokens || 1500, stream: true, ...(system ? { system } : {}), messages };
  if (webGrounded) {
    // GA server-side web search; citations are always on. $10/1k searches on top of tokens.
    body.tools = [{ type: "web_search_20250305", name: "web_search", max_uses: 5 }];
  }

  const res = await fetch(ANTHROPIC_URL, {
    method: "POST",
    headers: { "content-type": "application/json", "x-api-key": apiKey, "anthropic-version": ANTHROPIC_VERSION },
    body: JSON.stringify(body)
  });
  if (!res.ok) {
    const t = await res.text().catch(() => "");
    throw new Error(`Anthropic ${res.status}: ${t.slice(0, 500)}`);
  }

  // Manual SSE loop: on web search, collect real sources and suppress the model's
  // pre-search narration so the structured Verify output still renders cleanly.
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  const sources = [];
  let held = "";
  let flushed = !webGrounded; // stream immediately when not web-grounded

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let idx;
    while ((idx = buffer.indexOf("\n\n")) !== -1) {
      const frame = buffer.slice(0, idx);
      buffer = buffer.slice(idx + 2);
      for (const line of frame.split("\n")) {
        const t = line.trim();
        if (!t.startsWith("data:")) continue;
        const data = t.slice(5).trim();
        if (!data || data === "[DONE]") continue;
        let evt;
        try { evt = JSON.parse(data); } catch { continue; }

        if (evt.type === "content_block_delta" && evt.delta?.type === "text_delta") {
          if (flushed) yield evt.delta.text;
          else held += evt.delta.text;
        } else if (evt.type === "content_block_start" && evt.content_block?.type === "web_search_tool_result") {
          const c = evt.content_block.content;
          if (Array.isArray(c)) {
            for (const r of c) if (r.type === "web_search_result" && r.url) sources.push({ url: r.url, title: r.title || r.url });
          }
          if (!flushed) { flushed = true; held = ""; } // drop pre-search narration
        } else if (evt.type === "error") {
          throw new Error(evt.error?.message || "stream error");
        }
      }
    }
  }
  if (!flushed && held) yield held; // no search actually happened — show the answer anyway
  if (sources.length) {
    yield "\n\nSources:\n";
    for (const s of sources.slice(0, 6)) yield `- ${s.title} — ${s.url}\n`;
  }
}

async function* openrouterStream({ apiKey, model, system, history, userText, imageDataUrl, maxTokens }) {
  const messages = [];
  if (system) messages.push({ role: "system", content: system });
  for (const h of history || []) messages.push({ role: h.role, content: h.text });
  const content = [{ type: "text", text: userText }];
  if (imageDataUrl) content.push({ type: "image_url", image_url: { url: imageDataUrl } });
  messages.push({ role: "user", content });

  const res = await fetch(OPENROUTER_URL, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      authorization: `Bearer ${apiKey}`,
      // OpenRouter attribution headers (optional but recommended).
      "HTTP-Referer": "https://github.com/babybooghost/sideshift.ai",
      "X-Title": "SideShift AI"
    },
    body: JSON.stringify({ model, max_tokens: maxTokens || 1500, stream: true, messages })
  });
  if (!res.ok) {
    const t = await res.text().catch(() => "");
    throw new Error(`OpenRouter ${res.status}: ${t.slice(0, 500)}`);
  }
  yield* parseSSE(res, (evt) => {
    if (evt.error) throw new Error(evt.error?.message || "stream error");
    const d = evt.choices?.[0]?.delta?.content;
    return typeof d === "string" && d.length ? d : null;
  });
}

export function streamChat(req) {
  if (!req.apiKey) throw new Error(`Missing ${req.provider || "anthropic"} API key`);
  return req.provider === "openrouter" ? openrouterStream(req) : anthropicStream(req);
}
