import "server-only";
import { redirect } from "next/navigation";
import { cache } from "react";
import { apiFetch, currentPath, getMe } from "@/lib/api/server";
import { parseResponse, type MeAuthenticated } from "@/lib/api/types";
import type { Catalog } from "./types";

/** The central field catalog (same rules as the API). Cached per request. */
export const getCatalog = cache(async (): Promise<Catalog> => parseResponse<Catalog>(await apiFetch("/market/catalog")));

/** GET for market pages: 404 → null (the page decides), other failures throw to the error boundary. */
export async function marketGet<T>(path: string): Promise<T | null> {
  const res = await apiFetch(path);
  if (res.status === 404) return null;
  return parseResponse<T>(res);
}

/** The signed-in owner/buyer, or a redirect to the mobile sign-in that returns here. */
export async function requireIndividual(): Promise<MeAuthenticated> {
  const me = await getMe();
  if (!me.authenticated || me.stage !== "active") redirect(`/signin?next=${encodeURIComponent(await currentPath())}`);
  if (me.scope !== "individual") redirect(me.scope === "organization" ? "/team" : "/");
  return me;
}

/** The signed-in individual if any (public pages adapt to it). */
export async function optionalIndividual(): Promise<MeAuthenticated | null> {
  const me = await getMe().catch(() => null);
  return me?.authenticated && me.stage === "active" && me.scope === "individual" ? me : null;
}
