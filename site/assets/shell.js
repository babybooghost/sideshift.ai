// Builds the shared nav / footer / chatbot / background for secondary pages.
// index.html has its own hardcoded shell. site.js wires the behaviors after.
const MARK = "<svg viewBox='0 0 1024 1024' width='100%' height='100%'><defs><linearGradient id='mg' x1='330' y1='270' x2='700' y2='770' gradientUnits='userSpaceOnUse'><stop offset='0' stop-color='#F8C64C'/><stop offset='1' stop-color='#E68A26'/></linearGradient><linearGradient id='ma' x1='200' y1='730' x2='850' y2='235' gradientUnits='userSpaceOnUse'><stop offset='0' stop-color='#D8481A'/><stop offset='1' stop-color='#F5732E'/></linearGradient></defs><path d='M 208 700 C 300 664 298 590 380 560 C 470 527 470 470 542 452 C 636 428 690 356 812 258' fill='none' stroke='url(#ma)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/><path d='M 742 262 L 812 258 L 806 330' fill='none' stroke='url(#ma)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/><g transform='translate(96 0) skewX(-11)'><path d='M 688 374 C 674 308 588 294 518 320 C 428 354 428 442 522 486 C 620 530 646 622 566 678 C 494 728 392 716 348 658' fill='none' stroke='url(#mg)' stroke-width='100' stroke-linecap='round' stroke-linejoin='round'/></g><path d='M 560 442 C 636 414 690 356 812 258' fill='none' stroke='url(#ma)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/><path d='M 742 262 L 812 258 L 806 330' fill='none' stroke='url(#ma)' stroke-width='46' stroke-linecap='round' stroke-linejoin='round'/></svg>";

const page = document.body.dataset.page || "";
const link = (href, label, key) => `<a href="${href}"${key === page ? ' class="on"' : ""}>${label}</a>`;

const nav = `<nav><div class="navin">
  <a class="brand" href="index.html"><span class="m">${MARK}</span> SIDESHIFT <span class="chip">v0.1.2</span></a>
  <button class="burger" id="burger" aria-label="Menu">☰</button>
  <div class="nav-links">
    ${link("index.html#features", "Features", "features")}
    ${link("pricing.html", "Pricing", "pricing")}
    ${link("marketplace.html", "Marketplace", "marketplace")}
    ${link("case-studies.html", "Case studies", "case")}
    ${link("contact.html", "Contact", "contact")}
    <a class="navcta" href="index.html#download">Download</a>
  </div></div></nav>`;

const footer = `<footer><div class="wrap footin">
  <div><b style="color:var(--ink)">SideShift AI</b>. The verify layer for AI. Your key, your machine.</div>
  <div class="footlinks">
    <a href="pricing.html">Pricing</a><a href="marketplace.html">Marketplace</a><a href="blog.html">Blog</a><a href="case-studies.html">Case studies</a><a href="contact.html">Contact</a><a href="https://github.com/babybooghost/sideshift.ai">GitHub</a>
  </div></div></footer>`;

const bot = `<div class="bot">
  <div class="bot-panel glass" id="botPanel">
    <div class="bot-head"><span class="aw-dot"></span> Ask SideShift</div>
    <div class="bot-msgs" id="botMsgs"><div class="bot-msg bot-ai">Hi. Ask me about Verify, your API key, pricing, or downloads.</div></div>
    <div class="bot-in"><input id="botIn" placeholder="Type a question..."><button id="botSend">→</button></div>
  </div>
  <button class="bot-btn" id="botBtn" aria-label="Chat"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 11.5a8.4 8.4 0 01-8.5 8.4 8.6 8.6 0 01-3.9-.9L3 21l1.9-5.6a8.4 8.4 0 01-.9-3.9A8.4 8.4 0 0112.5 3 8.4 8.4 0 0121 11.5z"/></svg></button>
</div>`;

document.body.insertAdjacentHTML("afterbegin", `<div class="bgfx"></div><div class="grain"></div>` + nav);
document.body.insertAdjacentHTML("beforeend", footer + bot);
