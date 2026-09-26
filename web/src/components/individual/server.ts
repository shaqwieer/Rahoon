import "server-only";
import type { Metadata } from "next";
import { requireIndividualPortal } from "@/lib/api/guards";
import { apiGet } from "@/lib/api/server";
import type { Numerals } from "@/lib/format";
import { getLocale } from "@/lib/i18n/server";
import { requestCopy, type RequestCopy } from "./copy";

/** Individual guard first (cached per request), then locale + copy. Pages call this before any `apiGet`. */
export async function getIndividualContext() {
  const me = await requireIndividualPortal();
  const locale = await getLocale();
  const numerals: Numerals = me.user.numerals === "arab" ? "arab" : "latn";
  return { me, locale, c: requestCopy(locale), fmt: { locale, numerals } };
}

/** `GET /api/my{path}` after the individual guard (401/403/404 handled by apiGet). */
export async function myGet<T>(path: string): Promise<T> {
  await requireIndividualPortal();
  return apiGet<T>(`/my${path}`);
}

export async function individualMetadata(pick: (c: RequestCopy) => string): Promise<Metadata> {
  return { title: pick(requestCopy(await getLocale())), referrer: "no-referrer" };
}
