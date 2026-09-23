"use client";

import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { Numerals } from "@/lib/format";
import { dirFor, getDictionary, type Dictionary, type Locale } from "./index";

interface I18nValue {
  locale: Locale;
  dir: "rtl" | "ltr";
  t: Dictionary;
  /** Digit style preference (Western by default); fed to format.ts helpers. */
  numerals: Numerals;
}

const I18nContext = createContext<I18nValue | null>(null);

/** Provided once by the root layout; the dictionary is resolved client-side from the locale string. */
export function I18nProvider({ locale, numerals = "latn", children }: { locale: Locale; numerals?: Numerals; children: ReactNode }) {
  const value = useMemo<I18nValue>(() => ({ locale, dir: dirFor(locale), t: getDictionary(locale), numerals }), [locale, numerals]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nValue {
  const ctx = useContext(I18nContext);
  if (ctx) return ctx;
  // Outside a provider (e.g. isolated tests) fall back to Arabic defaults.
  return { locale: "ar", dir: "rtl", t: getDictionary("ar"), numerals: "latn" };
}
