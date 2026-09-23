import { execFileSync } from "node:child_process";
import path from "node:path";

/** Optionally reseeds the demo database (E2E_RESET=1) and checks both servers answer. */
export default async function globalSetup() {
  const root = path.resolve(__dirname, "..", "..");
  if (process.env.E2E_RESET === "1") {
    execFileSync("bash", [path.join(root, "scripts", "dev-api.sh"), "--reset"], { stdio: "inherit" });
  }
  const api = await fetch("http://localhost:5080/api/health").catch(() => null);
  if (!api?.ok) throw new Error("API is not running on :5080 — start it with `bash scripts/dev-api.sh`.");
  const web = await fetch(process.env.E2E_BASE_URL ?? "http://localhost:3000/login").catch(() => null);
  if (!web?.ok) throw new Error("Web is not running on :3000 — start it with `npm run dev` in web/.");
}
