import ar, { type Dictionary } from "./dictionaries/ar";
import type { Locale } from "./config";

export * from "./config";
export type { Dictionary };

const dictionaries: Record<Locale, Dictionary> = { ar };

/** Synchronous lookup — the dictionary is small and bundled. */
export function getDictionary(locale: Locale): Dictionary {
  return dictionaries[locale] ?? ar;
}
