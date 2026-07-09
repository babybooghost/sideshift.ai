import { app, BrowserWindow } from "electron";
import fs from "node:fs";

// Usage: electron render-svg.mjs <svg-in> <png-out> [size]
const svgPath = process.argv[2];
const outPath = process.argv[3];
const size = parseInt(process.argv[4] || "1024", 10);

app.disableHardwareAcceleration();

app.whenReady().then(async () => {
  const svg = fs.readFileSync(svgPath, "utf8");
  const win = new BrowserWindow({
    width: size,
    height: size,
    show: false,
    frame: false,
    transparent: true,
    useContentSize: true,
    webPreferences: { offscreen: false }
  });
  const html =
    `<!doctype html><html><head><meta charset="utf-8">` +
    `<style>html,body{margin:0;padding:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>` +
    `</head><body>${svg}</body></html>`;
  await win.loadURL("data:text/html;charset=utf-8," + encodeURIComponent(html));
  await new Promise((r) => setTimeout(r, 400));
  const img = await win.webContents.capturePage();
  const sz = img.getSize();
  const png = sz.width !== size ? img.resize({ width: size, height: size }).toPNG() : img.toPNG();
  fs.writeFileSync(outPath, png);
  console.log("RENDERED", outPath, JSON.stringify(sz));
  app.quit();
});
