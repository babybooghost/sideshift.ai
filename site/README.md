# SideShift AI marketing site

Static, zero-build. Pages: `index.html`, `pricing.html`, `marketplace.html`, `case-studies.html`, `blog.html`, `contact.html`. Shared assets in `assets/` (CSS, Three.js scenes, shared shell). Three.js loads from a pinned CDN.

## Deploy to Vercel

1. Push this repo to GitHub (already done).
2. In Vercel: **New Project** → import `babybooghost/sideshift.ai`.
3. Set **Root Directory** to `site`, **Framework Preset** = Other, no build command, output = `site` itself.
4. Deploy. Then add your custom domain under **Settings → Domains** and point DNS at Vercel.

Also works as-is on **Cloudflare Pages**, **Netlify**, or **GitHub Pages** (serve the `site/` folder as static).

## Notes

- Download buttons link to the GitHub release. While the repo is private, only collaborators can download; make the repo public for open downloads.
- Marketplace / Blog / Case-studies are honest "coming soon" pages. No fabricated tools, articles, or metrics.
