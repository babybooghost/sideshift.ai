import { defineConfig } from "vite";

// Renderer only. Electron main/preload are plain CJS-free ESM, loaded directly.
// Fable compiles src/*.fs -> dist-fable/*.js, and index.html imports the entry.
export default defineConfig({
  root: ".",
  base: "./",
  build: {
    outDir: "dist",
    emptyOutDir: true,
    rollupOptions: {
      input: "index.html"
    }
  },
  server: {
    port: 5173,
    strictPort: true
  }
});
