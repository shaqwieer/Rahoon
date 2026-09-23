import "server-only";
import { cookies } from "next/headers";
import { cache } from "react";
import { defaultLocale, getDictionary, isLocale, LOCALE_COOKIE, type Locale } from "./index";

/** Reads the `rahoon_locale` cookie (Arabic by default). Cached per request. */
export const getLocale = cache(async (): Promise<Locale> => {
  const value = (await cookies()).get(LOCALE_COOKIE)?.value;
  return isLocale(value) ? value : defaultLocale;
});

export async function getServerDictionary() {
  const locale = await getLocale();
  return { locale, t: getDictionary(locale) };
}
