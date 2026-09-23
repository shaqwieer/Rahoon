import "server-only";
import { redirect } from "next/navigation";
import { getMe, redirectToHome, requireMe } from "./server";
import type { MeAuthenticated, OrgKind } from "./types";

/**
 * Portal layout guard (UX only — the API authorizes every call). Institutional portals require an active
 * organization session of the right kind; anything else is sent to the user's own home (`me.home`).
 */
export async function requireOrgPortal(kind: OrgKind, portalPrefix: string): Promise<MeAuthenticated> {
  const me = await requireMe();
  if (me.scope === "owner") redirect("/owner");
  if (me.organization?.kind !== kind) redirectToHome(me, portalPrefix);
  return me;
}

/** Owner portal guard: owners sign in through their invitation link, never through /login. */
export async function requireOwnerPortal(): Promise<MeAuthenticated> {
  const me = await getMe();
  if (!me.authenticated || me.stage !== "active") redirect("/access-denied?reason=owner");
  if (me.scope !== "owner") redirectToHome(me, "/owner");
  return me;
}
