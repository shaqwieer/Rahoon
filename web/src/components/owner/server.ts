import "server-only";
import type { Metadata } from "next";
import { requireOwnerPortal } from "@/lib/api/guards";
import { apiGet } from "@/lib/api/server";
import type { MeAuthenticated } from "@/lib/api/types";
import type { Numerals } from "@/lib/format";
import { getLocale } from "@/lib/i18n/server";
import { ownerCopy, type OwnerCopy } from "./copy";

/** Shell data every owner page needs (the desktop header label, bell count, digit style). */
export interface OwnerShellData {
  userLabel: string;
  unread: number;
  numerals: Numerals;
  firstName: string;
  lenderName: string;
}

export function shellData(me: MeAuthenticated): OwnerShellData {
  const firstName = me.owner?.firstName ?? "";
  const lenderName = me.owner?.lenderName ?? me.organization?.name ?? "";
  return {
    userLabel: [firstName, lenderName].filter(Boolean).join(" · "),
    unread: me.unreadNotifications,
    numerals: me.user.numerals === "arab" ? "arab" : "latn",
    firstName,
    lenderName,
  };
}

/**
 * Owner guard first (cached per request), then locale + copy. Pages call this before any `apiGet`, so an
 * expired owner session is sent to the owner notice instead of racing to the institutional /login.
 */
export async function getOwnerContext() {
  const me = await requireOwnerPortal();
  const locale = await getLocale();
  const shell = shellData(me);
  return { me, locale, c: ownerCopy(locale), shell, fmt: { locale, numerals: shell.numerals } };
}

/** `GET /api/owner{path}` after the owner guard (401/403/404 handled by apiGet). */
export async function ownerGet<T>(path: string): Promise<T> {
  await requireOwnerPortal();
  return apiGet<T>(`/owner${path}`);
}

export async function ownerMetadata(pick: (c: OwnerCopy) => string): Promise<Metadata> {
  return { title: pick(ownerCopy(await getLocale())), referrer: "no-referrer" };
}

/** Riyadh calendar "today" (YYYY-MM-DD) — the browser clock/zone is not authoritative. */
export function riyadhToday(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Riyadh", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
}
