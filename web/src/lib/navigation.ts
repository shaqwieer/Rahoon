import { safeNext } from "./api/types";

/**
 * Full document navigation (not the client router) — used after sign-in, MFA, organization switch and
 * sign-out so no router cache or in-memory state from the previous session/tenant survives.
 * Only same-origin paths are accepted.
 */
export function hardNavigate(path: string | null | undefined, fallback = "/"): void {
  const target = new URL(safeNext(path, fallback), window.location.origin);
  window.location.replace(target.toString());
}
