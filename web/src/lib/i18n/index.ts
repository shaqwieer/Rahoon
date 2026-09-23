import ar, { type Dictionary } from "./dictionaries/ar";
import en from "./dictionaries/en";
import type { Locale } from "./config";

export * from "./config";
export type { Dictionary };

const dictionaries: Record<Locale, Dictionary> = { ar, en };

/** Synchronous lookup — both dictionaries are small and bundled. */
export function getDictionary(locale: Locale): Dictionary {
  return dictionaries[locale] ?? ar;
}
