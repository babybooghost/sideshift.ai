// Official Anthropic Messages API streaming. No reverse engineering:
// standard public endpoint, user-supplied API key, documented SSE format.
// Docs: https://docs.anthropic.com/en/api/messages-streaming

const API_URL = "https://api.anthropic.com/v1/messages";
const API_VERSION = "2023-06-01";
export const DEFAULT_MODEL = "claude-sonnet-5";

/**
 * Async generator yielding text deltas from a streamed completion.
 * @param {object} p
 * @param {string} p.apiKey        user's Anthropic API key
 * @param {string} [p.model]
 * @param {string} [p.system]      system prompt
 * @param {Array}  p.messages      [{role, content}] where content is string
 *                                 or content-block array (text + image blocks)
 * @param {number} [p.maxTokens]
 */
export async function* streamAnthropic({ apiKey, model, system, messages, maxTokens }) {
  if (!apiKey) throw new Error("Missing Anthropic API key");

  const res = await fetch(API_URL, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "x-api-key": apiKey,
      "anthropic-version": API_VERSION
    },
    body: JSON.stringify({
      model: model || DEFAULT_MODEL,
      max_tokens: maxTokens || 1024,
      stream: true,
      ...(system ? { system } : {}),
      messages
    })
  });

  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`Anthropic ${res.status}: ${body.slice(0, 500)}`);
  }

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });

    // SSE frames are separated by a blank line.
    let idx;
    while ((idx = buffer.indexOf("\n\n")) !== -1) {
      const frame = buffer.slice(0, idx);
      buffer = buffer.slice(idx + 2);
      for (const line of frame.split("\n")) {
        const trimmed = line.trim();
        if (!trimmed.startsWith("data:")) continue;
        const data = trimmed.slice(5).trim();
        if (!data || data === "[DONE]") continue;
        let evt;
        try { evt = JSON.parse(data); } catch { continue; }
        if (evt.type === "content_block_delta" && evt.delta?.type === "text_delta") {
          yield evt.delta.text;
        } else if (evt.type === "error") {
          throw new Error(evt.error?.message || "stream error");
        }
      }
    }
  }
}
