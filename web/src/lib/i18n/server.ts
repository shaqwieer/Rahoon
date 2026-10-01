import "server-only";
import { cache } from "react";
import { defaultLocale, getDictionary, type Locale } from "./index";

/** The product is Arabic-only (Saudi market): every page renders in Arabic, right to left. Cached per request. */
export const getLocale = cache(async (): Promise<Locale> => defaultLocale);

export async function getServerDictionary() {
  const locale = await getLocale();
  return { locale, t: getDictionary(locale) };
}
