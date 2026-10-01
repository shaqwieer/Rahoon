import "server-only";
import { redirectToHome, requireMe } from "./server";
import type { MeAuthenticated, OrgKind } from "./types";

/**
 * Workspace layout guard (UX only — the API authorizes every call): the team workspace requires an active session of
 * the Rahoon team; anyone else is sent to their own home (`me.home`).
 */
export async function requireOrgPortal(kind: OrgKind, portalPrefix: string): Promise<MeAuthenticated> {
  const me = await requireMe();
  if (me.organization?.kind !== kind) redirectToHome(me, portalPrefix);
  return me;
}
