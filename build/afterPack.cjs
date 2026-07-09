// Ad-hoc code-sign the macOS app after packing. Apple Silicon refuses to run a
// fully-unsigned app (Gatekeeper "damaged"); an ad-hoc signature (codesign -s -)
// lets beta testers open it after clearing quarantine, with no paid cert.
const { execFileSync } = require("node:child_process");
const path = require("node:path");

module.exports = async function afterPack(context) {
  if (context.electronPlatformName !== "darwin") return;
  const appName = context.packager.appInfo.productFilename + ".app";
  const appPath = path.join(context.appOutDir, appName);
  execFileSync("codesign", ["--force", "--deep", "--sign", "-", appPath], { stdio: "inherit" });
  console.log(`[afterPack] ad-hoc signed ${appPath}`);
};
